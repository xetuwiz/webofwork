"""HTTP client for the WebOfWork .NET API.

Two directions:

* **Reads** -- ``GET /internal/read/*``. These are cheap, but they are also the
  only way an agent can pull the whole freelancer pool, so they are budgeted.
* **Writes** -- ``PUT /internal/workflows/*`` and ``POST /internal/actions/*``.
  Every artefact an agent produces is written back through here rather than
  through the database, so the .NET side stays the single writer of its own
  schema and every write lands in the audit tables it already has.
"""

from __future__ import annotations

import json
import logging
from typing import Any
from urllib.parse import urlencode

import httpx

from ..config import Settings, get_settings
from .normalize import normalise

log = logging.getLogger(__name__)


class BudgetExceededError(RuntimeError):
    """Raised when a workflow exceeds its internal-read allowance.

    A distinct type rather than a generic ``RuntimeError`` so the agent layer can
    turn it into a specific, reported failure instead of a generic "something went
    wrong" that hides the cause.
    """


class DotNetUnavailableError(RuntimeError):
    """The .NET API could not be reached, or answered with an error."""


def _as_json_string(value: object) -> str | None:
    """Serialise a summary for a .NET ``string?`` field bound to a jsonb column.

    The .NET write DTOs declare their jsonb fields as strings, not objects --
    ``SerializeJsonbString`` on the other side parses them. Sending an object
    fails model binding, and the failure is a 400 naming the field while the
    artefact is simply not written.

    Decimals go through ``str`` so a commission is not reformatted into a float
    on the way to a money column.
    """
    if value is None:
        return None
    if isinstance(value, str):
        return value
    return json.dumps(value, default=str, separators=(",", ":"))


def _clean(body: dict[str, Any]) -> dict[str, Any]:
    """Strip ``None`` values.

    The .NET DTOs mix nullable and non-nullable members, and sending an explicit
    ``null`` for a non-nullable one (``proposal_id: int``) fails model binding
    for the whole request. Omitting the key lets the C# default apply, which is
    the intended way to say "not supplied".
    """
    return {k: v for k, v in body.items() if v is not None}


def _camel_case_query(params: dict[str, Any]) -> dict[str, Any]:
    """Rename ``snake_case`` query keys to the ``camelCase`` .NET binds.

    Not used. Kept as a single obvious place to look when a query filter comes
    back "unfiltered" for no visible reason, and as executable documentation of
    the trap: query-string binding is case-insensitive but *not*
    underscore-insensitive, so ``category_id`` does not bind to a ``categoryId``
    parameter. It is dropped, and the endpoint applies no filter while reporting
    success.

    The internal read endpoints accept both spellings instead, following
    ``GetPriceBands``. If you add a query parameter there, do the same -- a filter
    that silently does nothing is worse than no filter, because the caller cannot
    tell the difference between "nobody matched" and "the filter was ignored".
    """
    renamed: dict[str, Any] = {}
    for key, value in params.items():
        head, *tail = key.split("_")
        renamed[head + "".join(part.title() for part in tail)] = value
    return renamed


class DotNetClient:
    """Client for the internal surface of the .NET API.

    One instance per workflow run, which is what makes ``reads`` a per-workflow
    count rather than a process-wide one. A shared client would let one expensive
    workflow exhaust the budget for every other run in flight.
    """

    def __init__(self, settings: Settings | None = None, workflow_run_id: int | None = None) -> None:
        self._settings = settings or get_settings()
        self.workflow_run_id = workflow_run_id

        #: Reads consumed so far. Not a ``ContextVar`` or a global: a plain
        #: attribute on a per-run instance, so there is nowhere for it to leak to.
        self.reads = 0

        self.read_budget = self._settings.max_internal_reads_per_workflow

        self._headers = {
            "X-Internal-Api-Key": self._settings.internal_api_key,
            "Accept": "application/json",
        }

    # ── reads ─────────────────────────────────────────────────────────────

    def remaining_reads(self) -> int:
        return max(0, self.read_budget - self.reads)

    def _ssl(self) -> Any:
        """The ``verify`` argument for httpx.

        A trust decision, made in one place. ``False`` is only reachable when
        ``DOTNET_VERIFY_TLS`` is off, which :class:`Settings` refuses in
        production -- so the downgrade cannot happen by accident in a deployed
        environment.
        """
        if self._settings.dotnet_ca_bundle:
            # A private CA is the right answer for a self-signed certificate, and
            # it keeps verification on.
            return self._settings.dotnet_ca_bundle

        if not self._settings.dotnet_verify_tls:
            log.warning(
                "TLS verification is DISABLED for calls to %s. The shared API key "
                "is sent in a header on every call and can be read by anything on "
                "this network path. Development only.",
                self._settings.dotnet_base_url,
            )
            return False

        return True

    def _http(self, *, timeout: float) -> httpx.AsyncClient:
        return httpx.AsyncClient(timeout=timeout, verify=self._ssl())

    async def _get(self, path: str, **params: Any) -> Any:
        """GET an internal read endpoint, spending one unit of budget.

        The budget is checked *before* the request, not after. Checking afterwards
        would allow the request that breaches the limit to still execute, which
        defeats the point of having one.
        """
        if self.reads >= self.read_budget:
            raise BudgetExceededError(
                f"Workflow {self.workflow_run_id} exhausted its read budget of "
                f"{self.read_budget}. Raise AgenticAiOptions."
                f"MaxInternalReadsPerWorkflow, or narrow what this stage asks for."
            )

        url = f"{self._settings.dotnet_base_url}{path}"
        if params:
            # Drop ``None`` so an unset filter is absent rather than the literal
            # string "None", which the .NET enum parser would reject with a 400.
            clean = {k: v for k, v in params.items() if v is not None}
            if clean:
                # Keys are sent as written. The internal read endpoints accept
                # both spellings, so snake_case binds -- see
                # `_camel_case_query` for why that is worth knowing.
                url = f"{url}?{urlencode(clean)}"

        async with self._http(timeout=self._settings.dotnet_read_timeout_seconds) as client:
            try:
                response = await client.get(url, headers=self._headers)
            except httpx.HTTPError as exc:
                raise DotNetUnavailableError(f"GET {path} failed: {exc}") from exc

            if response.status_code >= 400:
                raise DotNetUnavailableError(
                    f"GET {path} returned {response.status_code}: {response.text[:300]}"
                )

            # Counted only on success. A request that failed returned nothing, so
            # charging for it would let a flaky endpoint exhaust the budget and
            # starve the stage.
            self.reads += 1

            # Normalised at the boundary. The .NET side serialises snake_case keys
            # and money as strings; agents read camelCase and numbers. Done here
            # once so no agent has to know, and no agent can get it wrong.
            return normalise(response.json())

    async def get_project(self, project_id: int) -> Any:
        return await self._get(f"/internal/read/projects/{project_id}")

    async def get_similar_projects(
        self,
        *,
        category_id: int,
        limit: int = 10,
        exclude_project_id: int | None = None,
    ) -> Any:
        """Recently posted projects in a category.

        Backs the planner's `get_similar_projects` tool. It used to be a stub
        that returned `[]`, which is worse than having no tool at all: the planner
        is told to sanity-check a budget against comparables, calls this, gets an
        empty list, and can conclude the category has no comparable history. That
        is a claim about the business, manufactured by a stub.

        `exclude_project_id` should be passed whenever the caller knows which
        project it is planning, so that project is not one of its own baselines.
        """
        return await self._get(
            f"/internal/read/projects/by-category/{category_id}",
            limit=limit,
            exclude_project_id=exclude_project_id,
        )

    async def get_category_skills(
        self, category_id: int, *, search: str | None = None
    ) -> Any:
        """The real skills in one category, optionally filtered by name.

        Backs the drafter's `list_skills_by_category`. That tool used to filter the
        injected category list instead of asking the platform, so it answered a
        question about skills with a category row -- and, once the caller stopped
        nesting skills into that list, answered `[]` for every category. The model
        then called it once per category, trying to get skills out of it.

        `search` is not a convenience. Development alone is 199 skills and about
        12KB, and the tool loop cuts results at 4000 characters, so the tail never
        reaches the model. A category with no way to narrow is a category whose
        second half does not exist as far as the agent is concerned.
        """
        return await self._get(
            f"/internal/read/categories/{category_id}/skills", search=search
        )

    async def search_freelancers(
        self,
        *,
        query: str | None = None,
        category_id: int | None = None,
        min_rating: float | None = None,
        available: bool | None = None,
        limit: int = 200,
    ) -> Any:
        return await self._get(
            "/internal/read/freelancers",
            query=query,
            category_id=category_id,
            min_rating=min_rating,
            available=available,
            limit=limit,
        )

    async def get_freelancer(self, freelancer_id: str) -> Any:
        return await self._get(f"/internal/read/freelancers/{freelancer_id}")

    async def get_freelancer_packages(self, freelancer_id: str) -> Any:
        return await self._get(f"/internal/read/freelancers/{freelancer_id}/packages")

    async def get_freelancer_reviews(self, freelancer_id: str) -> Any:
        return await self._get(f"/internal/read/freelancers/{freelancer_id}/reviews")

    async def get_commission(self) -> Any:
        return await self._get("/internal/read/platform/commission")

    async def get_price_bands(
        self,
        *,
        category_id: int | None = None,
        search: str | None = None,
    ) -> Any:
        """Advertised reference price bands, for the drafter's price suggestion.

        Backs the draft agent's `get_price_bands` tool. It exists because the
        drafter had no way to answer "what does this usually cost?" and so
        declined to: a client who posted a brief with no budget got "no price is
        suggested" and a shrug, which is the least useful thing a drafting
        assistant can do.

        The rows are *bands*, not quotes, and `source` travels with every one of
        them so the agent can attribute a number instead of asserting it. Observed
        freelancer rates on the source marketplace run far outside these bands at
        both ends, so the agent is told to present them as a starting range.

        `category_id` is optional on purpose. The agent matches the deliverable by
        wording, so it needs the whole vocabulary to know what exists before it
        can judge relevance -- narrowing is an optimisation for a caller that
        already knows the answer, not the normal path.
        """
        return await self._get(
            "/internal/read/platform/price-bands",
            category_id=category_id,
            search=search,
        )

    async def get_agent_thresholds(self) -> Any:
        return await self._get("/internal/read/platform/agent-thresholds")

    async def get_contract_disputes(self, contract_id: int) -> Any:
        return await self._get(f"/internal/read/contracts/{contract_id}/disputes")

    async def get_brief(self, brief_id: int) -> Any:
        return await self._get(f"/internal/read/briefs/{brief_id}")

    async def get_recommended_freelancer_ids(self, workflow_run_id: int) -> list[str]:
        """Freelancer ids this run recommended, per the validation agent's rule 10.

        This one is a read like any other and is budgeted, because an agent that
        could list recommendations for free would have a way to enumerate a run's
        whole shortlist without spending.
        """
        payload = await self._get(f"/internal/actions/workflows/{workflow_run_id}/recommendations")
        if isinstance(payload, dict):
            return [str(f) for f in payload.get("freelancerIds", payload.get("freelancer_ids", []))]
        return []

    # ── writes ────────────────────────────────────────────────────────────

    async def _write(self, method: str, path: str, body: dict[str, Any]) -> Any:
        """PUT or POST a workflow artefact back to .NET.

        Args:
            method: HTTP verb.
            path: Path below the .NET base URL.
            body: The payload. ``None`` values are stripped before sending --
                see :func:`_clean`.

        Returns:
            The decoded response, or ``None`` when the write failed.

        Notes:
            A failure is logged and swallowed, because an agent that produced a
            good result should not be reported as failed because an audit row did
            not save. The one exception is ``send_invitations``, whose result *is*
            the work.
        """
        url = f"{self._settings.dotnet_base_url}{path}"
        async with self._http(timeout=self._settings.dotnet_call_timeout_seconds) as client:
            try:
                response = await client.request(
                    method, url, headers=self._headers, json=_clean(body)
                )
            except httpx.HTTPError as exc:
                log.warning("write %s %s failed: %s", method, path, exc)
                return None

            if response.status_code >= 400:
                # Logged at warning with the body, because the .NET side answers
                # with a precise model-binding error and a silent 400 here is
                # indistinguishable from a workflow that simply wrote nothing.
                log.warning(
                    "write %s %s returned %s: %s",
                    method, path, response.status_code, response.text[:400],
                )
                return None

            if not response.content:
                return None
            return response.json()

    async def append_step(
        self,
        workflow_run_id: int,
        step_name: str,
        agent_name: str | None,
        input_summary: dict[str, Any] | None,
        output_summary: dict[str, Any] | None,
        status: str,
        duration_ms: int,
    ) -> Any:
        return await self._write(
            "PUT",
            f"/internal/workflows/{workflow_run_id}/step-complete",
            {
                "step_name": step_name,
                "agent_name": agent_name,
                # Serialised to a JSON *string*, because the .NET DTO declares
                # these as `string?` and stores them in a jsonb column. Sending an
                # object fails model binding with a 400 that names the field, and
                # the step is silently not written.
                #
                # Structured summaries only, never a raw prompt or completion:
                # they are unbounded, may contain client text that should not be
                # retained, and tell a later reader nothing. What someone needs is
                # what the agent decided.
                "input": _as_json_string(input_summary),
                "output": _as_json_string(output_summary),
                "status": status,
                "duration_ms": duration_ms,
            },
        )

    async def append_tool_call(
        self,
        workflow_run_id: int,
        tool_name: str,
        input_summary: dict[str, Any] | None,
        output_summary: dict[str, Any] | None,
        success: bool,
        duration_ms: int,
    ) -> Any:
        return await self._write(
            "PUT",
            f"/internal/workflows/{workflow_run_id}/tool-call",
            {
                "tool_name": tool_name,
                "input": _as_json_string(input_summary),
                "output": _as_json_string(output_summary),
                "success": success,
                "duration_ms": duration_ms,
            },
        )

    async def append_recommendation(self, workflow_run_id: int, recommendation: dict[str, Any]) -> Any:
        return await self._write(
            "PUT",
            f"/internal/workflows/{workflow_run_id}/recommendation",
            {
                "project_id": recommendation.get("project_id", 0),
                "freelancer_id": recommendation.get("freelancer_id"),
                "score": recommendation.get("score", 0),
                "factors_json": _as_json_string(recommendation.get("factors")),
                "reason": recommendation.get("reason"),
                # The matching agent produces these and they were being dropped
                # here as well as server-side: the request body did not name
                # them, so `RecommendationFactors.Merge` had nothing to fold in
                # and both the client's shortlist and the freelancer's
                # invitation showed a score with no evidence behind it.
                "strengths": recommendation.get("strengths") or [],
                "gaps": recommendation.get("gaps") or [],
                "portfolio_samples_json": _as_json_string(recommendation.get("portfolio_samples")),
            },
        )

    async def append_screening(self, workflow_run_id: int, screening: dict[str, Any]) -> Any:
        return await self._write(
            "PUT",
            f"/internal/workflows/{workflow_run_id}/screening",
            {
                "proposal_id": screening.get("proposal_id", 0),
                "fit_score": screening.get("fit_score", 0),
                "strengths_json": _as_json_string(screening.get("strengths")),
                "concerns_json": _as_json_string(screening.get("concerns")),
                "recommendation": screening.get("recommendation", ""),
                "rationale": screening.get("rationale"),
            },
        )

    async def append_validation(self, workflow_run_id: int, validation: dict[str, Any]) -> Any:
        return await self._write(
            "PUT",
            f"/internal/workflows/{workflow_run_id}/validation",
            {
                "project_id": validation.get("project_id", 0),
                # Non-nullable on the .NET side, so an absent value is sent as 0
                # rather than null -- null fails model binding and the whole write
                # is rejected with a 400.
                "proposal_id": validation.get("proposal_id") or 0,
                "freelancer_id": validation.get("freelancer_id")
                or "00000000-0000-0000-0000-000000000000",
                "valid": validation.get("valid", False),
                "issues_json": _as_json_string(validation.get("issues")),
                "cost_breakdown_json": _as_json_string(validation.get("cost_breakdown")),
                "requires_human_approval": validation.get("requires_human_approval", True),
                "human_summary": validation.get("human_summary"),
            },
        )

    async def complete_workflow(
        self,
        workflow_run_id: int,
        output: dict[str, Any] | None,
        status: str = "Completed",
    ) -> Any:
        """Record a stage's output and land the run in ``status``.

        Args:
            workflow_run_id: The run to write to.
            output: The payload. Serialised to a JSON string; see below.
            status: Where the run should end up. ``"PendingClientApproval"`` for
                a stage that stops for a person, ``"Completed"`` for one that
                does not.

        Notes:
            The status is not decoration. ``AiTalentMatchController.Start``
            de-duplicates on ``status IN (0, 1, 9)`` -- Pending, Running,
            PendingClientApproval -- so a run parked at ``Pending`` is handed back
            to the client on every subsequent press instead of being re-dispatched,
            and the shortlist page renders "pending" over a run that is really
            waiting for approval. Sending ``"Completed"`` for a paused stage has
            the mirror problem: the approve control is enabled only for
            ``PendingClientApproval``, so it would never light up.

            ``CompleteWorkflowAsync`` ignores a status it cannot parse rather than
            refusing the write, so a typo here costs the status, not the output.
        """
        return await self._write(
            "PUT",
            f"/internal/workflows/{workflow_run_id}/complete",
            {
                # The DTO is `CompleteWorkflowStepRequest`; both `Output` and
                # `Status` are read. Output goes as a JSON string for the same
                # reason as the other jsonb columns.
                "output": _as_json_string(output),
                "step_name": "complete",
                "status": status,
                "duration_ms": 0,
            },
        )

    async def send_invitations(
        self, workflow_run_id: int, project_id: int, freelancer_ids: list[str]
    ) -> dict[str, Any]:
        """Ask .NET to send invitations. The one write whose result matters.

        Unlike the audit writes, a failure here is raised. The caller has nothing
        else to fall back on, and reporting success while no invitation was sent
        would be the worst possible outcome -- the client believes freelancers were
        contacted and nobody was.
        """
        url = f"{self._settings.dotnet_base_url}/internal/actions/send-invitations"
        body = {
            "workflow_run_id": workflow_run_id,
            "project_id": project_id,
            "freelancer_ids": freelancer_ids,
        }

        async with self._http(timeout=self._settings.dotnet_call_timeout_seconds) as client:
            try:
                response = await client.post(url, headers=self._headers, json=body)
            except httpx.HTTPError as exc:
                raise DotNetUnavailableError(f"send-invitations failed: {exc}") from exc

        # 409 is the "nothing could be invited" case, which the caller needs to
        # report as a failed run rather than an error page.
        if response.status_code >= 400:
            detail: Any = response.text[:500]
            try:
                detail = response.json()
            except ValueError:
                pass
            raise DotNetUnavailableError(
                f"send-invitations returned {response.status_code}: {detail}"
            )

        return response.json()
