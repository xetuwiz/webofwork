"""The tool registry: what an agent may do, and what it may read.

This is the security boundary for the whole agent system, so it is deliberately
small and explicit. An agent is a component whose inputs are model output; the
registry is what stops "the model asked" from being treated as authority.

Two independent gates, both checked on every call:

1. **Allow-list** -- the tool must exist at all.
2. **Per-agent permission** -- *this* agent may use *this* tool.

Plus a per-workflow read budget, so a loop cannot pull the whole database even
with a legitimate tool.
"""

from __future__ import annotations

import logging
import time
from dataclasses import dataclass
from typing import Awaitable, Callable

from ..clients.dotnet_client import BudgetExceededError, DotNetClient

log = logging.getLogger(__name__)

#: Every tool that exists. A tool not named here cannot be called, whatever any
#: agent's permission set says.
TOOL_ALLOWLIST: frozenset[str] = frozenset({
    "list_categories",
    "list_skills_by_category",
    "get_project",
    "get_similar_projects",
    "search_freelancers",
    "get_freelancer",
    "get_freelancer_packages",
    "get_freelancer_reviews",
    "get_platform_commission",
    "get_agent_thresholds",
    "get_contract_disputes",
    "get_brief",
    "get_recommended_freelancer_ids",
    "send_invitations",
    "get_price_bands",
})

#: What each agent may call.
#:
#: Narrow on purpose. The outreach agent can only send invitations; it cannot
#: read the freelancer pool. The screening agent can read a freelancer but not
#: the project brief. A wider set would be convenient and would mean one
#: compromised prompt reaches tools the agent has no reason to touch.
AGENT_TOOL_PERMISSIONS: dict[str, frozenset[str]] = {
    "draft": frozenset({
        "list_categories",
        "list_skills_by_category",
        "get_platform_commission",
        # Read-only reference data. Added because without it the drafter had no
        # way to answer "what does this usually cost?", and a client who posted a
        # brief with no budget got "no price is suggested" -- which is the whole
        # value of a drafting assistant, declined.
        "get_price_bands",
        "get_project",
    }),
    # The planner is the one agent that runs a model-directed tool loop, so it is
    # the one whose advertised schemas and permitted tools must agree. They did
    # not, for a while:
    #
    #   advertised: get_project, get_agent_thresholds, get_similar_projects,
    #               list_categories, search_freelancers
    #   permitted:  get_project, get_agent_thresholds, get_similar_projects
    #
    # so the model was shown two tools it could never use, and every attempt came
    # back REFUSED. It did not break the run -- the loop converts a refusal into
    # an observation -- which is exactly why it went unnoticed: the planner still
    # produced a plan, just a worse-informed one, because it could not ground a
    # skill name in the catalogue or size the pool before choosing a shortlist.
    #
    # Found by comparing the advertised schema names against the allow-list, not
    # by running the loop: a probe with a stub toolbox permitted everything and so
    # agreed with the model rather than with production.
    #
    # `matching` gained `get_project` for a different reason, and it was not the
    # model. `MatchingAgent.run` calls it *directly*, outside the LLM loop:
    #
    #     project = await tools.call("get_project", project_id=payload.project_id)
    #
    # so the gate refused our own code and the whole `talent_match` stage died in
    # 0ms with "Agent 'matching' may not use 'get_project'". Not a degraded run --
    # a dead one, and it was never anything else, because that call is on the only
    # path through `run`. The stage could not have worked even once.
    #
    # The distinction matters for how to look for these: a refusal raised inside the
    # loop becomes `REFUSED: ...` in the trajectory and the run continues, which is
    # why the planner's earlier mismatches were quiet. A refusal raised by a direct
    # `toolbox.call` propagates and kills the stage. So the check that matters is
    # not "is every advertised tool permitted" but "does every agent's own direct
    # call fall inside its allow-list" -- see `test_direct_calls_are_permitted`.
    #
    # All the additions are reads. The gate exists to stop an agent reaching
    # *outside* its remit -- an outreach agent sending invitations, a matching
    # agent reading a contract dispute -- not to stop an agent reading its own
    # subject.
    "planning": frozenset({
        "get_project",
        "get_agent_thresholds",
        "get_similar_projects",
        "list_categories",
        "search_freelancers",
    }),
    "matching": frozenset({
        "get_project",
        "search_freelancers",
        "get_freelancer",
        "get_freelancer_packages",
        "get_freelancer_reviews",
    }),
    "screening": frozenset({
        "get_freelancer",
        "get_freelancer_reviews",
    }),
    "validation": frozenset({
        "get_project",
        "get_freelancer",
        "get_platform_commission",
        "get_agent_thresholds",
        "get_contract_disputes",
        "get_recommended_freelancer_ids",
    }),
    "outreach": frozenset({
        "send_invitations",
    }),
}

#: Tools that spend read budget. Actions do not, so a workflow is not starved by
#: its own write-back.
READ_TOOLS: frozenset[str] = frozenset({
    "get_project",
    "get_similar_projects",
    "search_freelancers",
    "get_freelancer",
    "get_freelancer_packages",
    "get_freelancer_reviews",
    "get_platform_commission",
    "get_agent_thresholds",
    "get_contract_disputes",
    "get_brief",
    "get_recommended_freelancer_ids",
    "list_categories",
    "list_skills_by_category",
    "get_price_bands",
})


class ToolNotPermittedError(PermissionError):
    """The agent asked for a tool it may not use."""


class UnknownToolError(KeyError):
    """The tool does not exist."""


@dataclass
class ToolCall:
    """One recorded tool call."""

    tool: str
    agent: str
    success: bool
    duration_ms: int
    input_summary: dict
    output_summary: dict
    error: str | None = None


class ToolBox:
    """Executes tools on an agent's behalf, enforcing the registry.

    One instance per (workflow, agent). Every call is checked, timed, counted and
    optionally written to the .NET audit table, so an agent cannot bypass the
    registry by holding a ``DotNetClient`` directly -- agents are given a
    ``ToolBox`` and nothing else.
    """

    def __init__(
        self,
        agent: str,
        workflow_run_id: int,
        client: DotNetClient,
        *,
        categories: list[dict] | None = None,
    ) -> None:
        self.agent = agent
        self.workflow_run_id = workflow_run_id
        self._client = client
        self._allowed = AGENT_TOOL_PERMISSIONS.get(agent)

        if self._allowed is None:
            # An agent with no entry is a programming error, not a runtime
            # condition. Failing here means it cannot reach the database at all.
            raise ValueError(f"Unknown agent {agent!r}; no permission set defined.")

        #: Categories the caller pre-loaded. Injected rather than fetched so the
        #: draft agent's category grounding costs no read budget -- the .NET side
        #: already sends them with the request.
        self._categories = categories or []

        self.calls: list[ToolCall] = []

    def permitted(self) -> frozenset[str]:
        return self._allowed

    def permits(self, tool: str) -> bool:
        return tool in self._allowed

    async def call(self, tool: str, *, best_effort: bool = False, **kwargs) -> object:
        """Run a tool after checking both gates.

        Args:
            tool: Name from the allow-list.
            best_effort: When true, a tool failure is logged and ``None`` is
                returned instead of raising. Use only where the caller genuinely
                can proceed without the answer.
            **kwargs: Passed to the tool implementation.

        Raises:
            UnknownToolError: not in the allow-list at all.
            ToolNotPermittedError: this agent may not use it.
            BudgetExceededError: the workflow is out of read budget.
            Exception: whatever the tool raised, unless ``best_effort``.

        Notes:
            This used to swallow tool failures and return ``None``. That was
            actively dangerous: the validation agent wrapped its reads in
            try/except expecting to record a note when one failed, and because
            the exception never escaped, the except was dead code and the run
            reported ``valid: true`` having checked nothing at all. A gate that
            reports success when it did not run is worse than one that is
            missing, because it is trusted.

            So a failure propagates, and the agents that can genuinely carry on
            without an answer say so with ``best_effort=True``.
        """
        if tool not in TOOL_ALLOWLIST:
            raise UnknownToolError(f"Unknown tool {tool!r}")
        if tool not in self._allowed:
            raise ToolNotPermittedError(
                f"Agent {self.agent!r} may not use {tool!r}. "
                f"It may use: {', '.join(sorted(self._allowed))}."
            )

        handler = _HANDLERS.get(tool)
        if handler is None:
            # Reachable only if a tool is allow-listed without a handler, which
            # a test asserts against. Failing loudly beats returning None.
            raise UnknownToolError(f"Tool {tool!r} is allow-listed but has no implementation.")

        started = time.monotonic()
        summary_in = _summarise(kwargs)

        try:
            result = await handler(self, **kwargs)
            success = True
            error = None
        except (BudgetExceededError, ToolNotPermittedError, UnknownToolError):
            # A permission or budget failure is the gate working. Re-raised, and
            # not logged as a tool error, so it is not confused with the tool
            # itself misbehaving.
            raise
        except Exception as exc:  # noqa: BLE001
            result = None
            success = False
            error = f"{type(exc).__name__}: {exc}"
            log.warning("tool %s failed for %s: %s", tool, self.agent, error)

            await self._record(tool, summary_in, result, success, started, error)

            if best_effort:
                return None
            # Propagated. The call is already recorded, so the failure is
            # auditable even though the caller is about to unwind.
            raise

        await self._record(tool, summary_in, result, success, started, error)
        return result

    async def _record(
        self,
        tool: str,
        summary_in: dict,
        result: object,
        success: bool,
        started: float,
        error: str | None,
    ) -> None:
        """Record a call in the audit trail, successes and failures alike."""
        duration_ms = int((time.monotonic() - started) * 1000)
        call = ToolCall(
            tool=tool,
            agent=self.agent,
            success=success,
            duration_ms=duration_ms,
            input_summary=summary_in,
            output_summary=_summarise_result(result),
            error=error,
        )
        self.calls.append(call)

        # Written to .NET as it happens rather than in a batch at the end, so a
        # crash mid-stage still leaves a record of what had been attempted.
        # Awaited: without it the coroutine is created and discarded, and the
        # audit trail silently stops being written.
        await self._client.append_tool_call(
            self.workflow_run_id,
            tool,
            call.input_summary,
            call.output_summary,
            success,
            duration_ms,
        )

    # ── the tools ─────────────────────────────────────────────────────────

    async def _list_categories(self) -> list[dict]:
        return self._categories

    async def _list_skills_by_category(
        self, category_id: int, search: str | None = None
    ) -> list[dict]:
        """Ask the platform for one category's real skills.

        This filtered the injected category list and returned the category row,
        which is a category row answering a question about skills. It looked like
        a lookup and satisfied no one: the model called it once per category in a
        live run, looking for skills that were never there.

        The category list cannot answer this. It is sent as bare `{id, name}`
        rows precisely because nesting all 654 skills overflowed the provider's
        request limit, so there is no skill data in it to find.

        `search` exists because a whole category can still be too much. Development
        is 199 skills, ~12KB, and the loop cuts tool results at 4000 characters --
        which lands mid-list alphabetically, so JavaScript, React and TypeScript
        were simply absent. Told the list was truncated and given no way to narrow
        it, the model could only pick from the head of the alphabet.
        """
        rows = await self._client.get_category_skills(category_id, search=search)
        return rows if isinstance(rows, list) else []

    async def _get_project(self, project_id: int) -> object:
        return await self._client.get_project(project_id)

    async def _get_price_bands(
        self,
        search: str | None = None,
        category_id: int | None = None,
    ) -> object:
        return await self._client.get_price_bands(
            search=search,
            category_id=category_id,
        )

    async def _search_freelancers(
        self,
        query: str | None = None,
        category_id: int | None = None,
        min_rating: float | None = None,
        available: bool | None = None,
        limit: int = 200,
    ) -> object:
        return await self._client.search_freelancers(
            query=query,
            category_id=category_id,
            min_rating=min_rating,
            available=available,
            limit=limit,
        )

    async def _get_freelancer(self, freelancer_id: str) -> object:
        return await self._client.get_freelancer(freelancer_id)

    async def _get_freelancer_packages(self, freelancer_id: str) -> object:
        return await self._client.get_freelancer_packages(freelancer_id)

    async def _get_freelancer_reviews(self, freelancer_id: str) -> object:
        return await self._client.get_freelancer_reviews(freelancer_id)

    async def _get_platform_commission(self) -> object:
        return await self._client.get_commission()

    async def _get_agent_thresholds(self) -> object:
        return await self._client.get_agent_thresholds()

    async def _get_contract_disputes(self, contract_id: int) -> object:
        return await self._client.get_contract_disputes(contract_id)

    async def _get_brief(self, brief_id: int) -> object:
        return await self._client.get_brief(brief_id)

    async def _get_recommended_freelancer_ids(self, workflow_run_id: int) -> object:
        """Freelancer ids this run recommended.

        The basis for validation rule 10. Read rather than assumed: the rule
        exists to check that an approved shortlist only contains people the run
        put forward, and taking the shortlist's own word for it would make the
        rule a tautology.
        """
        return await self._client.get_recommended_freelancer_ids(workflow_run_id)

    async def _get_similar_projects(
        self,
        category_id: int,
        limit: int = 10,
        exclude_project_id: int | None = None,
    ) -> object:
        """Recently posted projects in a category, for a budget sanity check.

        This returned `[]` unconditionally while the planner advertised it as the
        way to judge what a normal budget and delivery window look like. A tool
        that lies is worse than an absent one: the model is told to cross-check
        against comparables, gets silence, and reasonably reads that as "there are
        none", which is a statement about the business that nothing in the
        database supports.

        `exclude_project_id` is passed through so the project being planned is
        not one of its own comparables.
        """
        return await self._client.get_similar_projects(
            category_id=category_id,
            limit=limit,
            exclude_project_id=exclude_project_id,
        )

    async def _send_invitations(
        self, project_id: int, freelancer_ids: list[str]
    ) -> object:
        return await self._client.send_invitations(
            self.workflow_run_id, project_id, freelancer_ids
        )


_HANDLERS: dict[str, Callable[..., Awaitable[object]]] = {
    "list_categories": ToolBox._list_categories,
    "list_skills_by_category": ToolBox._list_skills_by_category,
    "get_project": ToolBox._get_project,
    "get_similar_projects": ToolBox._get_similar_projects,
    "search_freelancers": ToolBox._search_freelancers,
    "get_freelancer": ToolBox._get_freelancer,
    "get_freelancer_packages": ToolBox._get_freelancer_packages,
    "get_freelancer_reviews": ToolBox._get_freelancer_reviews,
    "get_platform_commission": ToolBox._get_platform_commission,
    "get_price_bands": ToolBox._get_price_bands,
    "get_agent_thresholds": ToolBox._get_agent_thresholds,
    "get_contract_disputes": ToolBox._get_contract_disputes,
    "get_brief": ToolBox._get_brief,
    "get_recommended_freelancer_ids": ToolBox._get_recommended_freelancer_ids,
    "send_invitations": ToolBox._send_invitations,
}


# ── summarisation ────────────────────────────────────────────────────────
#
# Everything sent to .NET passes through here. The rule from the plan is that raw
# prompts and completions never reach the database; this is where that is
# enforced, and it applies to tool input and output as well as model I/O.

#: Longest string kept in a summary. Free-text fields are truncated rather than
#: dropped, so a reader can still tell what was asked.
MAX_SUMMARY_STRING = 300


def _summarise(value: object, *, depth: int = 0) -> dict:
    """Reduce a value to a small, safe, JSON-shaped summary.

    Lists are counted, not copied: a 200-item freelancer list summarised in full
    is a large row in an audit table, and the count is what a reader wants.
    """
    if depth > 2:
        return {"_truncated": "depth"}

    if value is None or isinstance(value, (bool, int, float)):
        return {"value": value}

    if isinstance(value, str):
        if len(value) > MAX_SUMMARY_STRING:
            return {
                "value": value[:MAX_SUMMARY_STRING] + f"... [{len(value)} chars total]",
                "_truncated": True,
            }
        return {"value": value}

    if isinstance(value, dict):
        return {str(k): _summarise(v, depth=depth + 1) for k, v in list(value.items())[:25]}

    if isinstance(value, (list, tuple)):
        summary: dict = {"count": len(value)}
        if value:
            summary["first"] = _summarise(value[0], depth=depth + 1)
        return summary

    return {"value": str(value)[:MAX_SUMMARY_STRING]}


def _summarise_result(result: object) -> dict:
    """Summarise a tool result, with a dedicated shape for the big ones."""
    if isinstance(result, list) and len(result) > 3:
        return {
            "count": len(result),
            "sample": [_summarise_result(item) for item in result[:2]],
        }
    if isinstance(result, list):
        return {"count": len(result), "items": [_summarise_result(item) for item in result]}
    if isinstance(result, dict):
        # Shallow: these are projections, and truncating them loses the fields a
        # reader needs.
        return {k: _summarise_result(v) for k, v in list(result.items())[:25]}
    return _summarise(result)


def assert_registry_is_coherent() -> None:
    """Check the registry against itself.

    Called at import. A tool that is allow-listed but has no handler would only
    fail when an agent called it; a permission that names a tool not on the
    allow-list would silently do nothing. Both are the kind of mistake that is
    invisible until a workflow quietly misbehaves.
    """
    missing = TOOL_ALLOWLIST - set(_HANDLERS)
    if missing:
        raise RuntimeError(f"Allow-listed tools with no handler: {sorted(missing)}")

    unknown: dict[str, set[str]] = {}
    for agent, permitted in AGENT_TOOL_PERMISSIONS.items():
        bad = permitted - TOOL_ALLOWLIST
        if bad:
            unknown[agent] = set(bad)

    if unknown:
        raise RuntimeError(
            "Permission sets name tools that are not allow-listed: "
            + "; ".join(f"{a}: {sorted(b)}" for a, b in unknown.items())
        )


assert_registry_is_coherent()
