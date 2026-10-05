"""Stage dispatch: the one place a request becomes agent work.

## Why this service holds no checkpoints

The plan this is built from specifies LangGraph with ``interrupt_after`` and a
``PostgresSaver``, so the graph owns "where is this run paused".

This service does the opposite, on purpose. ``WorkflowRun.Status`` on the .NET
side already *is* the pause state -- ``PendingClientApproval`` and
``PendingManagerApproval`` are distinct values, set by the stage, and consumed by
``ResumeWorkflowAsync``. A second source of truth would mean two components
disagreeing about whether a run is waiting on a human, with no principled way to
resolve it and no obvious symptom when it happens.

So a stage here is a **pure function of (stage, context)**, and pausing is .NET's
business. That makes the whole service horizontally scalable with no shared state,
removes a class of bug rather than adding one, and costs the ability to resume
mid-node -- which the pipeline does not need, because the only pause in it is
between validation and outreach, and that is a node boundary anyway.
"""

from __future__ import annotations

import json
import logging
import time
from typing import Any

from pydantic import BaseModel, Field, ValidationError

from ..agents.draft_agent import DraftAgent, DraftInput
from ..agents.matching_agent import MatchingAgent, MatchingInput
from ..agents.outreach_agent import OutreachAgent, OutreachInput
from ..agents.planning_agent import (
    MatchWeights,
    PlanningAgent,
    PlanningInput,
    PlanningOutput,
)
from ..agents.screening_agent import ProposalIn, ScreeningAgent, ScreeningInput
from ..agents.validation_agent import ValidationAgent, ValidationInput
from ..clients.dotnet_client import DotNetClient, DotNetUnavailableError
from ..clients.normalize import normalise
from ..config import get_settings
from ..tools.registry import BudgetExceededError, ToolNotPermittedError, UnknownToolError

log = logging.getLogger(__name__)

#: The stage vocabulary, mirroring ``WorkflowStages`` on the .NET side. The .NET
#: side validates before it calls, so an unknown value here means the two have
#: drifted -- worth failing loudly rather than defaulting.
STAGES = frozenset({"draft", "talent_match", "screen", "validate", "outreach"})


class AgentChatRequest(BaseModel):
    """The body .NET posts to ``/agent/chat``."""

    message: str = ""
    #: Recorded by .NET so a run can be reproduced from fixtures. Informational --
    #: nothing in this service branches on it, because branching on how a call
    #: arrived rather than what it asks for is how replay and live paths diverge.
    execution_mode: str = "recorded_replay"
    workflow_run_id: int
    stage: str
    project_id: int | None = None
    context: dict[str, Any] = Field(default_factory=dict)

    #: A plan supplied by the caller, overriding the Planning Agent.
    #:
    #: This exists so "adjust the weights and re-run" in the client is a real
    #: control rather than a decoration. A client that moved a slider and pressed
    #: re-run, and got a shortlist the planner had chosen anyway, would have been
    #: lied to -- and nothing would have said so, because the run would have
    #: succeeded and produced a plausible shortlist.
    #:
    #: Validated on arrival, not trusted. An out-of-range or non-normalised weight
    #: is rejected here rather than silently clamped, so a bad value produces an
    #: error the client can show instead of a shortlist scored on numbers nobody
    #: chose. Passing nothing is always valid and means "plan it yourself".
    plan: dict[str, Any] | None = None


class AgentChatResponse(BaseModel):
    #: ``ok`` | ``needs_clarification`` | ``paused`` | ``failed``
    status: str
    stage: str
    summary: str = ""
    payload: dict[str, Any] | None = None
    #: Present when ``status == "failed"``.
    error: str | None = None
    duration_ms: int = 0
    reads_used: int = 0
    reads_budget: int = 0


class PlanRejected(ValueError):
    """A caller-supplied plan was sent and is not usable.

    Distinct from "no plan was sent", which means the planner runs normally. The
    distinction is the whole point: a bad plan must fail loudly rather than
    quietly falling back to the planner, because a fallback produces a plausible
    shortlist scored on numbers the client believes it chose.
    """


def _resume_was_supplied(ctx: dict[str, Any]) -> bool:
    """Was a decision present at all?

    Separate from whether it could be read, because the two mean different things:
    nothing to resume from is a caller error the service rejects upstream, while a
    decision that is present and unreadable is corruption. Reporting both as the
    same failure would make the run's error text useless for telling them apart.
    """
    return bool(ctx.get("resume_value") or ctx.get("resumeValue"))


def approved_ids_from(ctx: dict[str, Any]) -> list[str]:
    """The freelancer ids the client approved, from a run's context.

    Reads ``resume_value``, which is the decision .NET recorded. It is deliberately
    not read from the request: taking the approved set from the request would let
    any caller nominate who gets contacted.

    Accepts the decision as an object *or* as a JSON string, because the column it
    comes from is jsonb stored as text and at least one producer forwarded the
    text rather than the value. The original reader guarded with ``isinstance(dict)``
    and fell through to an empty list on a string, so an approved shortlist sent
    zero invitations and the stage still reported success.

    Returns ``[]`` for anything unreadable; ``_outreach`` turns that into a failure
    rather than a silent approval of nobody.
    """
    resume = ctx.get("resume_value") or ctx.get("resumeValue")

    if isinstance(resume, str):
        try:
            resume = json.loads(resume)
        except (TypeError, ValueError):
            return []

    if not isinstance(resume, dict):
        return []

    raw = resume.get("freelancerIds") or resume.get("freelancer_ids") or []
    return [str(f) for f in raw]


class StageDispatcher:
    """Runs the agents for one stage."""

    def _supplied_plan(self) -> PlanningOutput | None:
        """The caller's plan, if one was sent and it is sound.

        Returns ``None`` when nothing was sent, so the caller plans normally. A
        plan that was sent but is *invalid* raises instead, because silently
        falling back to the planner there would produce a shortlist scored on
        numbers the client believes it set -- the exact failure this control was
        added to prevent.
        """
        raw = self.request.plan
        if not raw:
            return None

        # Unknown keys are rejected, not ignored.
        #
        # Pydantic's default is to drop them, so a client that sent
        # `{"skill_coverge": 0.9}` -- one missing "a" -- would get the *default*
        # weights, a successful run, and a shortlist scored on numbers it never
        # chose, with nothing anywhere reporting a problem. For a control whose
        # entire purpose is to let a person set these numbers, that is the worst
        # possible failure: silent and successful.
        known = set(PlanningOutput.model_fields)
        unknown = sorted(set(raw) - known)
        if unknown:
            raise PlanRejected(
                f"unknown plan field(s): {', '.join(unknown)}. "
                f"Expected any of: {', '.join(sorted(known))}."
            )

        weights_raw = raw.get("weights")
        if isinstance(weights_raw, dict):
            known_weights = set(MatchWeights.model_fields)
            bad_weights = sorted(set(weights_raw) - known_weights)
            if bad_weights:
                raise PlanRejected(
                    f"unknown weight(s): {', '.join(bad_weights)}. "
                    f"Expected any of: {', '.join(sorted(known_weights))}."
                )

        try:
            plan = PlanningOutput.model_validate(raw)
        except ValidationError as exc:
            raise PlanRejected(
                "The supplied plan is not valid: "
                + "; ".join(
                    f"{'.'.join(str(p) for p in e['loc'])}: {e['msg']}" for e in exc.errors()
                )
            ) from exc

        # Weights are the number the client moves a slider to change, so they are
        # the ones that must be sound. Checked here rather than left to
        # `MatchWeights`, which only asserts the sum -- a plan of
        # `{-1, 0, 0, 0, 2}` sums to 1 and inverts the score.
        weights = plan.weights.model_dump()
        for name, value in weights.items():
            if not 0.0 <= value <= 1.0:
                raise PlanRejected(
                    f"weight '{name}' is {value}; every weight must be between 0 and 1"
                )

        total = plan.weights.total()
        if abs(total - 1.0) > 0.001:
            raise PlanRejected(
                f"weights sum to {total:.4f}, not 1.0. They are relative, so a total "
                "of 1.3 would silently over-score every candidate."
            )

        if not 5 <= plan.shortlist_size <= 15:
            raise PlanRejected(
                f"shortlist_size is {plan.shortlist_size}; it must be between 5 and 15"
            )

        # A clarification question on a supplied plan is a contradiction: the
        # client is answering a question, not asking one.
        if plan.clarification_question:
            raise PlanRejected(
                "a supplied plan cannot carry a clarification_question -- that is "
                "the planner asking something, and the client has already answered"
            )

        plan.reasoning_summary = plan.reasoning_summary or (
            "Weights set by the client rather than chosen by the planner."
        )
        return plan

    def __init__(self, request: AgentChatRequest) -> None:
        self.request = request
        self.settings = get_settings()
        self.client = DotNetClient(self.settings, workflow_run_id=request.workflow_run_id)
        self._categories: list[dict] = []

    async def run(self) -> AgentChatResponse:
        started = time.monotonic()
        req = self.request

        if req.stage not in STAGES:
            # Not defaulted. A run dispatched to a graph that does not exist would
            # appear to succeed and write nothing.
            return await self._failed(
                f"Unknown stage {req.stage!r}. Expected one of: {', '.join(sorted(STAGES))}."
            )

        # Categories arrive with the request, so grounding costs no read budget.
        #
        # Normalised here because this payload does not come from `DotNetClient`.
        # Every other .NET payload crosses that client and is camelCased at its
        # HTTP boundary; these categories ride in on the request body instead,
        # so they arrive in whatever casing ASP.NET serialised -- PascalCase --
        # while every reader in the subsystem looks for `"id"` and `"name"`.
        #
        # The symptom was silent and total. `list_skills_by_category` filters the
        # category list on `c.get("id")`, so it returned `[]` for all seven
        # categories -- a live draft had the model call it seven times in a row
        # before giving up. Grounding built `category_ids` with `"id" in c`, so it
        # came up empty and rejected the model's own correct answer: "Category id
        # 1 is not a real category and was ignored", on the category the request
        # had just listed.
        raw_categories = req.context.get("categories")
        if isinstance(raw_categories, list):
            normalised = normalise(raw_categories)
            self._categories = [c for c in normalised if isinstance(c, dict)]

        try:
            status, summary, payload = await self._dispatch()
        except PlanRejected as exc:
            # The caller's own input was wrong. Named separately from a stage
            # failure so the client can say "your weights are not valid" rather
            # than "the AI could not complete this", which sends them looking in
            # the wrong place.
            log.info("run %s supplied an unusable plan: %s", req.workflow_run_id, exc)
            return await self._failed(str(exc))
        except BudgetExceededError as exc:
            return await self._failed(str(exc))
        except (ToolNotPermittedError, UnknownToolError) as exc:
            # A permission failure is a programming error, not a workflow outcome,
            # and is logged at error level so it cannot pass unnoticed.
            log.error("agent tool gate rejected a call: %s", exc)
            return await self._failed(f"Tool gate: {exc}")
        except DotNetUnavailableError as exc:
            return await self._failed(f"Could not reach the WebOfWork API: {exc}")
        except Exception as exc:  # noqa: BLE001
            log.exception("stage %s failed for run %s", req.stage, req.workflow_run_id)
            return await self._failed(f"{type(exc).__name__}: {exc}")

        return AgentChatResponse(
            status=status,
            stage=req.stage,
            summary=summary,
            payload=payload,
            duration_ms=int((time.monotonic() - started) * 1000),
            reads_used=self.client.reads,
            reads_budget=self.client.read_budget,
        )

    async def _dispatch(self) -> tuple[str, str, dict[str, Any] | None]:
        handler = {
            "draft": self._draft,
            "talent_match": self._talent_match,
            "screen": self._screen,
            "validate": self._validate,
            "outreach": self._outreach,
        }[self.request.stage]
        return await handler()

    # ── stages ────────────────────────────────────────────────────────────

    async def _draft(self) -> tuple[str, str, dict[str, Any]]:
        agent = DraftAgent(self.client, categories=self._categories)
        ctx = self.request.context

        out = await agent.run(
            DraftInput(
                workflow_run_id=self.request.workflow_run_id,
                partial_text=str(ctx.get("partial_text") or self.request.message or ""),
                title_hint=ctx.get("title_hint"),
                category_hint=ctx.get("category_hint"),
                budget_hint_usd=ctx.get("budget_hint_usd"),
                delivery_days_hint=ctx.get("delivery_days_hint"),
            )
        )

        payload = out.model_dump(mode="json")
        await self.client.complete_workflow(self.request.workflow_run_id, payload)

        if out.clarification_question:
            return (
                "needs_clarification",
                out.clarification_question,
                payload,
            )
        return "ok", f"Drafted {out.title!r} with {len(out.skill_ids)} skill(s).", payload

    async def _talent_match(self) -> tuple[str, str, dict[str, Any] | None]:
        """Plan, match, validate -- then stop for the client.

        Outreach is deliberately *not* run here. The client approves first, and
        ``/api/ai/talent-match/{id}/resume`` is what continues the graph
        afterwards. Running outreach in the same pass would contact people the
        client never saw.
        """
        ctx = self.request.context
        project_id = self.request.project_id or ctx.get("project_id")
        if project_id is None:
            return "failed", "A project id is required to match talent.", None

        # A caller-supplied plan wins over the Planning Agent, and the distinction
        # is recorded in the payload so the client can say which happened rather
        # than presenting an unchosen plan as if the user had reviewed it.
        supplied = self._supplied_plan()

        if supplied is not None:
            plan = supplied
            plan_source = "supplied"
        else:
            planner = PlanningAgent(self.client, categories=self._categories)
            plan = await planner.run(
                PlanningInput(workflow_run_id=self.request.workflow_run_id, project_id=project_id)
            )

            if plan.clarification_question:
                return (
                    "needs_clarification",
                    plan.clarification_question,
                    plan.model_dump(mode="json"),
                )

            plan_source = "planner"

        matcher = MatchingAgent(self.client, categories=self._categories)
        matched = await matcher.run(
            MatchingInput(
                workflow_run_id=self.request.workflow_run_id,
                project_id=project_id,
                weights=plan.weights,
                hard_filters=plan.hard_filters,
                shortlist_size=plan.shortlist_size,
            )
        )

        # Validation over the proposed shortlist, before anyone is contacted.
        validator = ValidationAgent(self.client, categories=self._categories)
        verdict = await validator.run(
            ValidationInput(
                workflow_run_id=self.request.workflow_run_id,
                stage="talent_match",
                project_id=project_id,
                shortlist=[s.freelancer_id for s in matched.recommendations],
            )
        )

        payload = {
            "plan": plan.model_dump(mode="json"),
            # `"planner"` or `"supplied"`. The client shows the weights and offers
            # a re-run, so it has to know whether the numbers on screen are ones
            # the planner chose or ones a person set. Showing the first as if it
            # were the second is the specific dishonesty this avoids.
            "plan_source": plan_source,
            "recommendations": [s.model_dump(mode="json") for s in matched.recommendations],
            "top_pick_id": str(matched.top_pick_id) if matched.top_pick_id else None,
            "no_match_reason": matched.no_match_reason,
            "validation": verdict.model_dump(mode="json"),
        }

        if not matched.recommendations:
            return (
                "ok",
                matched.no_match_reason or "No freelancer matched this project.",
                payload,
            )

        if not verdict.valid:
            # Stop rather than present a shortlist the gate rejected. The client
            # is told why, which is more useful than a list they cannot act on.
            return await self._paused_at_gate(
                verdict.human_summary, payload, gate_rejected=True
            )

        return await self._paused_at_gate(
            f"Shortlist of {len(matched.recommendations)} ready for approval. "
            + verdict.human_summary,
            payload,
        )

    async def _paused_at_gate(
        self,
        summary: str,
        payload: dict[str, Any],
        gate_rejected: bool = False,
    ) -> tuple[str, str, dict[str, Any]]:
        """Record a run that is stopping for a person, then report it.

        The persistence is the point, and it was missing.

        ``_screen`` and ``_validate`` both call ``complete_workflow``; this stage
        did not, and it is the only one that *ends* paused. So a talent-match run
        that reached the client-approval gate -- the ordinary successful outcome --
        left ``workflow_runs.final_output`` null and ``status`` at ``Pending``.

        Three things broke from that one omission:

        * ``final_output`` is where the planner's weights are recorded. With it
          null, the shortlist page had no weights to show and fell back to the
          panel defaults while telling the client "The AI has not chosen its
          weights yet".
        * ``status`` stayed ``Pending`` (0), which is inside ``Start``'s
          de-duplication set ``(0, 1, 9)``, so every later press of "Find best
          talent" handed the finished run back instead of re-running it.
        * The page rendered "pending" over a run that was actually waiting for
          the client, so the gate looked like it had never been reached.

        Observed live on run 24.

        ``gate_rejected`` lands on ``PendingApproval`` rather than
        ``PendingClientApproval``: when validation refuses the shortlist there is
        nothing for the client to approve, and enabling the approve control over a
        list the gate just rejected would be the wrong affordance. The client
        re-runs with different weights instead.
        """
        log.info(
            "run %s pausing: %s",
            self.request.workflow_run_id,
            "validation rejected the shortlist"
            if gate_rejected
            else "shortlist ready for client approval",
        )

        # Best-effort, and deliberately so -- the report below is the more
        # important half. Losing it because the bookkeeping failed would replace
        # one useful message with a less useful one. But log loudly: without the
        # write the run reads as still-running and Start will keep returning it.
        try:
            await self.client.complete_workflow(
                self.request.workflow_run_id,
                payload,
                status="PendingApproval" if gate_rejected else "PendingClientApproval",
            )
        except Exception as exc:  # noqa: BLE001
            log.error(
                "could not record the pause for run %s, so it will read as Pending "
                "and Start will keep returning it: %s: %s",
                self.request.workflow_run_id, type(exc).__name__, exc,
            )

        return "paused", summary, payload

    async def _screen(self) -> tuple[str, str, dict[str, Any]]:
        agent = ScreeningAgent(self.client, categories=self._categories)
        ctx = self.request.context

        proposals = [
            ProposalIn(
                proposal_id=int(p["proposal_id"]),
                freelancer_id=p["freelancer_id"],
                bid_amount_usd=p.get("bid_amount_usd"),
                delivery_days=p.get("delivery_days"),
                cover_letter=str(p.get("cover_letter") or ""),
            )
            for p in (ctx.get("proposals") or [])
            if isinstance(p, dict) and p.get("proposal_id") and p.get("freelancer_id")
        ]

        if not proposals:
            return "ok", "No proposals were supplied to screen.", None

        out = await agent.run(
            ScreeningInput(
                workflow_run_id=self.request.workflow_run_id,
                project_id=self.request.project_id or ctx.get("project_id") or 0,
                # The project is read here, by the dispatcher, and handed in.
                #
                # It used to be omitted, so the agent fell back to calling
                # `get_project` itself -- and `screening` is not permitted that
                # tool, so every screening run raised `ToolNotPermittedError` and
                # wrote nothing. The stage could not succeed at all. A permission
                # map is only useful if every caller respects it, and this one had
                # a caller that did not.
                project=await self.client.get_project(
                    self.request.project_id or ctx.get("project_id") or 0
                ),
                proposals=proposals,
            )
        )

        payload = out.model_dump(mode="json")
        await self.client.complete_workflow(self.request.workflow_run_id, payload)

        strong = sum(1 for s in out.screenings if s.recommendation == "strong_match")
        return (
            "ok",
            f"Screened {len(out.screenings)} proposal(s); {strong} strong match(es).",
            payload,
        )

    async def _validate(self) -> tuple[str, str, dict[str, Any]]:
        agent = ValidationAgent(self.client, categories=self._categories)
        ctx = self.request.context

        out = await agent.run(
            ValidationInput(
                workflow_run_id=self.request.workflow_run_id,
                stage="validate",
                project_id=self.request.project_id or ctx.get("project_id"),
                proposal_id=ctx.get("proposal_id"),
                freelancer_id=ctx.get("freelancer_id"),
                proposed_amount_usd=ctx.get("proposed_amount_usd"),
                delivery_days=ctx.get("delivery_days"),
                match_score=ctx.get("match_score"),
            )
        )

        payload = out.model_dump(mode="json")
        await self.client.complete_workflow(self.request.workflow_run_id, payload)

        return ("ok" if out.valid else "paused"), out.human_summary, payload

    async def _outreach(self) -> tuple[str, str, dict[str, Any]]:
        """Send invitations, from the client's recorded decision.

        The approved ids come from ``resume_value`` on the run, because that is
        the client's decision as .NET recorded it. Taking them from the request
        would let any caller nominate who gets contacted.
        """
        ctx = self.request.context
        approved = approved_ids_from(ctx)

        # A decision that was present and yielded nothing is an error, not an
        # empty approval. Inviting nobody and reporting success is
        # indistinguishable, from the client's side, from a successful approval
        # of an empty shortlist -- and it is the more damaging of the two, because
        # the client has already been told these people were invited.
        if _resume_was_supplied(ctx) and not approved:
            return (
                "failed",
                "The client's approval was recorded but could not be read, so "
                "nobody was contacted. Re-approving the shortlist will retry it.",
                None,
            )

        project_id = self.request.project_id or ctx.get("project_id")
        if project_id is None:
            return "failed", "A project id is required to send invitations.", None

        agent = OutreachAgent(self.client, categories=self._categories)
        out = await agent.run(
            OutreachInput(
                workflow_run_id=self.request.workflow_run_id,
                project_id=int(project_id),
                approved_freelancer_ids=approved,
            )
        )

        payload = out.model_dump(mode="json")
        status = "ok" if out.invitation_ids else "failed"
        if out.errors:
            status = "failed"

        return status, out.human_summary, payload

    async def _failed(self, message: str) -> AgentChatResponse:
        """Record a stage failure against the run, then report it.

        The persistence is the point, and it was missing.

        Every success path calls ``complete_workflow``, which is what writes the
        run's status. This did not, so a stage that failed left
        ``workflow_runs.status`` at ``Pending`` -- and the run looked alive
        forever.

        That is not cosmetic. ``AiTalentMatchController.Start`` de-duplicates on

            WHERE project_id = @id AND stage = 'talent_match'
              AND status IN (0, 1, 9)          -- Pending, Running, PendingClientApproval

        so a run stuck at ``Pending`` is handed back to the client instead of being
        re-dispatched. Observed live: run 23 died on the tool gate, and every
        subsequent press of "Find best talent" returned the same dead run in
        539ms without calling a single model. The feature was unrecoverable
        without editing the database by hand.

        Sending the status through ``append_step`` rather than adding a new
        endpoint is deliberate: ``CompleteStep`` already applies
        ``ApplyStepStatus``, which parses the status string, so ``Failed`` was
        always persistable. Nothing needed extending -- it needed calling.
        """
        log.warning(
            "stage %s failed for run %s: %s",
            self.request.stage, self.request.workflow_run_id, message,
        )

        try:
            await self.client.append_step(
                self.request.workflow_run_id,
                step_name=self.request.stage,
                agent_name=self.request.stage,
                input_summary=None,
                output_summary={"error": message},
                status="Failed",
                duration_ms=0,
            )
        except Exception as exc:  # noqa: BLE001
            # Best-effort, and deliberately so. The failure being reported is the
            # important half; failing to *record* it must not replace that report
            # with a second, less useful one. Logged loudly because the run is now
            # stuck at Pending again and needs the manual nudge.
            log.error(
                "could not record the failure for run %s, so it will read as Pending "
                "and Start will keep returning it: %s: %s",
                self.request.workflow_run_id, type(exc).__name__, exc,
            )

        return AgentChatResponse(
            status="failed",
            stage=self.request.stage,
            error=message,
            duration_ms=0,
            reads_used=self.client.reads,
            reads_budget=self.client.read_budget,
        )
