"""The Screening Agent.

Ranks the proposals that came in on a project.

The same split as matching: the band comes from a formula, the wording comes from
the model. A model asked directly for "strong / possible / weak" will assign bands
that contradict its own scores, and a client sorting by a label that disagrees
with the number above it has no way to tell which to trust.

The bands are therefore *derived* from the fit score, so they cannot disagree:
>= 0.75 strong, 0.50-0.74 possible, < 0.50 weak.
"""

from __future__ import annotations

import re
import time
from decimal import Decimal
from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, Field

from ..skills.ontology import coverage
from .base import BaseAgent

Recommendation = Literal["strong_match", "possible_match", "weak_match"]

STRONG_THRESHOLD = 0.75
POSSIBLE_THRESHOLD = 0.50

MIN_STRENGTHS = 2
MAX_STRENGTHS = 5
MAX_RATIONALE_CHARS = 300

_CONTACT_PATTERNS = (
    re.compile(r"[\w.+-]+@[\w-]+\.[\w.]+"),
    re.compile(r"\+?\d[\d\s().-]{7,}\d"),
    re.compile(r"https?://\S+", re.IGNORECASE),
)


class MissingProjectError(RuntimeError):
    """Screening was dispatched without the project snapshot it needs.

    A caller bug, not a workflow outcome. Distinct from "these proposals scored
    badly", which is what an empty screening would otherwise look like -- and a
    client acting on that would reject good proposals.
    """


def band_for(fit_score: float) -> Recommendation:
    """The band a score falls into.

    Separate from the agent so the thresholds are testable on their own and
    cannot drift between the scoring code and the label code.
    """
    if fit_score >= STRONG_THRESHOLD:
        return "strong_match"
    if fit_score >= POSSIBLE_THRESHOLD:
        return "possible_match"
    return "weak_match"


class ProposalIn(BaseModel):
    proposal_id: int
    freelancer_id: UUID
    bid_amount_usd: Decimal | None = None
    delivery_days: int | None = None
    cover_letter: str = ""


class ScreeningInput(BaseModel):
    workflow_run_id: int
    project_id: int
    project: dict[str, Any] | None = None
    proposals: list[ProposalIn] = Field(default_factory=list)


class ProposalScreening(BaseModel):
    proposal_id: int
    fit_score: float
    strengths: list[str] = Field(default_factory=list)
    concerns: list[str] = Field(default_factory=list)
    recommendation: Recommendation = "weak_match"
    rationale: str = ""


class ScreeningOutput(BaseModel):
    screenings: list[ProposalScreening] = Field(default_factory=list)
    #: Proposal ids present in the input but missing from the output. Always empty
    #: in practice, and asserted by a test -- a silently dropped proposal is a
    #: client believing fewer people applied than did.
    missing_proposal_ids: list[int] = Field(default_factory=list)


class ScreeningAgent(BaseAgent[ScreeningInput, ScreeningOutput]):
    name = "screening"

    async def run(self, payload: ScreeningInput) -> ScreeningOutput:
        started = time.monotonic()
        tools = self._toolbox(payload.workflow_run_id)

        # The project arrives in the payload. It used to be re-read here with
        # `tools.call("get_project", ...)` when absent, and that call could only
        # ever fail: `screening` is deliberately not permitted `get_project`, so
        # the branch raised `ToolNotPermittedError` rather than doing anything.
        #
        # Not widened to allow it. Reading the project is the dispatcher's job,
        # and giving this agent the ability to pull arbitrary project data on
        # demand would widen the blast radius of a hostile cover letter for no
        # gain -- the project is a single known record, not a search.
        #
        # A missing project is therefore a caller bug, and it is reported as one
        # rather than as an empty screening: the latter looks like "these
        # proposals scored badly", which is a different and wrong conclusion.
        project = payload.project
        if project is None:
            raise MissingProjectError(
                "screening was dispatched without a project snapshot; the "
                "dispatcher reads the project and passes it in"
            )

        required = [str(s) for s in (project.get("skills") or [])]
        ceiling = _ceiling(project)
        days_allowed = int(project.get("expectedDays") or 0) or None

        scored: list[ProposalScreening] = []
        profiles: dict[str, dict] = {}

        for proposal in payload.proposals:
            key = str(proposal.freelancer_id)

            # A profile read per proposal, and it costs budget. A project with
            # forty proposals would blow the allowance, so the read is skipped
            # once the pool is large enough that the fit score can be produced
            # from the proposal alone.
            profile: dict = {}
            if len(payload.proposals) <= MAX_PROFILE_READS:
                try:
                    # Best-effort: the fit score degrades to the proposal and
                    # project alone, which is still better than dropping the proposal.
                    profile = await tools.call("get_freelancer", freelancer_id=key, best_effort=True) or {}
                except Exception:  # noqa: BLE001
                    profile = {}
            profiles[key] = profile

            fit, strengths, concerns = self._fit(proposal, profile, required, ceiling, days_allowed)

            scored.append(
                ProposalScreening(
                    proposal_id=proposal.proposal_id,
                    fit_score=round(_clamp(fit), 4),
                    strengths=strengths[:MAX_STRENGTHS],
                    concerns=concerns[:MAX_STRENGTHS],
                    recommendation=band_for(_clamp(fit)),
                    rationale=_fallback_rationale(strengths, concerns),
                )
            )

        scored.sort(key=lambda s: (-s.fit_score, s.proposal_id))

        scored = await self._annotate(scored, profiles, project, payload.workflow_run_id)

        # A proposal present in the input but absent from the output would be a
        # client looking at a shortlist that is quietly missing people.
        returned = {s.proposal_id for s in scored}
        missing = [p.proposal_id for p in payload.proposals if p.proposal_id not in returned]

        for screening in scored:
            await self._client.append_screening(
                payload.workflow_run_id,
                {
                    "proposal_id": screening.proposal_id,
                    "fit_score": screening.fit_score,
                    "strengths": screening.strengths,
                    "concerns": screening.concerns,
                    "recommendation": screening.recommendation,
                    "rationale": screening.rationale,
                },
            )

        result = ScreeningOutput(screenings=scored, missing_proposal_ids=missing)

        await self._record_step(
            workflow_run_id=payload.workflow_run_id,
            step_name="screening",
            input_summary={"project_id": payload.project_id, "proposals": len(payload.proposals)},
            output_summary={
                "screened": len(scored),
                "strong": sum(1 for s in scored if s.recommendation == "strong_match"),
                "possible": sum(1 for s in scored if s.recommendation == "possible_match"),
                "weak": sum(1 for s in scored if s.recommendation == "weak_match"),
                "missing": missing,
            },
            status="ok",
            started=started,
        )
        return result

    # ── scoring ───────────────────────────────────────────────────────────

    def _fit(
        self,
        proposal: ProposalIn,
        profile: dict[str, Any],
        required: list[str],
        ceiling: float | None,
        days_allowed: int | None,
    ) -> tuple[float, list[str], list[str]]:
        """Fit in 0..1, plus the reasons for it.

        Weighted rather than a mean of pass/fail checks, so a proposal that nails
        the skills but asks for three times the budget does not score the same as
        one that is merely adequate everywhere.
        """
        strengths: list[str] = []
        concerns: list[str] = []

        # Skill coverage, 0.45. The heaviest single factor: this is what the client
        # is actually buying.
        #
        # Resolved through the ontology rather than string equality, which is what
        # makes "ReactJS" count for a project asking for "React" and "C#" count
        # towards a .NET brief. This matters more here than in the matching agent
        # because 0.45 is the heaviest weight in the formula -- an applicant
        # written as `Node.js` used to lose almost half their fit score to
        # spelling.
        #
        # Partial credit is preserved: a .NET applicant for a C# brief earns 0.6
        # of the factor and has the gap recorded, rather than being treated as
        # having no relevant skill at all.
        if required:
            owned = [str(s) for s in (profile.get("skills") or []) if s]
            skill_score, resolutions = coverage(required, owned)

            covered = [r.required for r in resolutions if r.credit >= 1.0]
            partial = [r for r in resolutions if 0.0 < r.credit < 1.0]
            missing = [r.required for r in resolutions if r.credit == 0.0]

            if covered:
                strengths.append("Covers " + ", ".join(covered))

            if partial:
                # Named explicitly, because "partially covers C#" is information a
                # client acts on and "does not cover C#" is not the same claim.
                for resolution in partial:
                    strengths.append(
                        f"{resolution.matched or resolution.required} partially covers "
                        f"{resolution.required}"
                    )

            if missing:
                concerns.append("No evidence of " + ", ".join(missing))
        else:
            skill_score = 0.6  # unknown, not absent
            concerns.append("Project lists no required skills, so fit is weakly determined")

        # Reputation, 0.2. Neutral when there are no reviews.
        count = int(profile.get("reviewCount") or 0)
        if count == 0:
            success_score = 0.5
        else:
            success_score = min(1.0, float(profile.get("averageRating") or 0) / 5.0)
            if success_score >= 0.8:
                strengths.append(f"{profile.get('averageRating')} from {count} review(s)")
            elif success_score < 0.6:
                concerns.append(f"Average rating {profile.get('averageRating')} across {count} review(s)")

        # Budget, 0.2.
        if proposal.bid_amount_usd is not None and ceiling:
            ratio = float(proposal.bid_amount_usd) / ceiling
            if ratio <= 1.0:
                budget_score = min(1.0, 0.7 + 0.3 * ratio)
                if ratio <= 0.8:
                    strengths.append(f"Bid {proposal.bid_amount_usd} is inside the budget")
            else:
                budget_score = max(0.0, 1.0 - (ratio - 1.0))
                concerns.append(f"Bid {proposal.bid_amount_usd} exceeds the project ceiling {ceiling}")
        else:
            budget_score = 0.5

        # Delivery, 0.1.
        if proposal.delivery_days and days_allowed:
            if proposal.delivery_days <= days_allowed:
                delivery_score = 1.0
                strengths.append(f"{proposal.delivery_days} day delivery is within the window")
            else:
                delivery_score = max(0.0, days_allowed / proposal.delivery_days)
                concerns.append(f"{proposal.delivery_days} days exceeds the {days_allowed} asked")
        else:
            delivery_score = 0.5

        # Cover letter, 0.05. Barely weighted, and deliberately: a well-written
        # letter is weak evidence of ability, and over-weighting it is how a
        # confident writer beats a better engineer.
        letter = (proposal.cover_letter or "").strip()

        # Existence, not quality. This used to be `min(1.0, len(letter) / 400)`,
        # which scored a 400-character ramble at 100% and a precise 60-character
        # answer at 15% -- and then told the client the letter "addresses the
        # brief specifically" on the strength of its length. That is a claim the
        # code cannot support, shown to the person making a hiring decision.
        #
        # Length is kept only as a floor for *having written something*. Judging
        # whether it addresses the brief needs reading it, and until that is done
        # honestly the honest score is "there is one".
        letter_score = 0.5 if letter else 0.0
        if letter and len(letter) >= 120:
            strengths.append("Submitted a substantial cover letter")
        elif not letter:
            concerns.append("No cover letter")

        total = (
            0.45 * skill_score
            + 0.20 * success_score
            + 0.20 * budget_score
            + 0.10 * delivery_score
            + 0.05 * letter_score
        )

        if not strengths:
            strengths.append("Submitted a proposal")
        return total, strengths, concerns

    # ── reasons ───────────────────────────────────────────────────────────

    async def _annotate(
        self,
        screenings: list[ProposalScreening],
        profiles: dict[str, dict],
        project: dict[str, Any],
        workflow_run_id: int,
    ) -> list[ProposalScreening]:
        """Model-written rationales for an already-decided ranking."""
        if not screenings:
            return screenings

        lines = [
            f"- proposal {s.proposal_id}: fit {s.fit_score:.2f} "
            f"[{', '.join(s.strengths)}] concerns [{', '.join(s.concerns)}]"
            for s in screenings
        ]

        prompt = (
            f"{self.read_prompt('screening')}\n\n"
            f"## Project\n{project.get('title')}\n"
            f"{str(project.get('description') or '')[:800]}\n\n"
            f"## Screened proposals\n" + "\n".join(lines) + "\n"
        )

        try:
            raw = await self._ask_model(prompt, workflow_run_id=workflow_run_id)
            rationales = _parse_rationales(raw)
        except Exception:  # noqa: BLE001
            return screenings

        for screening in screenings:
            text = rationales.get(str(screening.proposal_id))
            if text:
                screening.rationale = _sanitise(text)
        return screenings

    # ── reads ─────────────────────────────────────────────────────────────

    @staticmethod
    def _max_profile_reads() -> int:
        """Cap on profile reads, so a busy project cannot exhaust the budget."""
        return MAX_PROFILE_READS


#: Beyond this many proposals, profiles are not read and the fit score is
#: computed from the proposal and project alone. Chosen so a typical project
#: (under a dozen proposals) gets full profile evidence, while a project with
#: hundreds does not fail with a budget error.
MAX_PROFILE_READS = 25


def _ceiling(project: dict[str, Any]) -> float | None:
    pricing = str(project.get("pricingType") or "").lower()
    value = project.get("fixedPrice") if pricing == "fixed" else project.get("maxPrice")
    return float(value) if value else None


def _clamp(value: float) -> float:
    return max(0.0, min(1.0, value))


def _fallback_rationale(strengths: list[str], concerns: list[str]) -> str:
    parts = []
    if strengths:
        parts.append("; ".join(strengths[:2]))
    if concerns:
        parts.append("but " + "; ".join(concerns[:2]))
    return (". ".join(parts) or "No distinguishing detail.")[:MAX_RATIONALE_CHARS]


def _parse_rationales(raw: str) -> dict[str, str]:
    import json

    text = raw.strip()
    start, end = text.find("{"), text.rfind("}")
    if start != -1 and end > start:
        try:
            parsed = json.loads(text[start : end + 1])
            if isinstance(parsed, dict):
                inner = parsed.get("rationales", parsed)
                if isinstance(inner, dict):
                    return {str(k): str(v) for k, v in inner.items()}
        except json.JSONDecodeError:
            pass

    out: dict[str, str] = {}
    for line in text.splitlines():
        match = re.match(r"^\s*[-*]?\s*(\d+)\s*[:\)\.]\s*(.+)$", line)
        if match:
            out[match.group(1)] = match.group(2).strip()
    return out


def _sanitise(text: str) -> str:
    cleaned = text.strip()
    for pattern in _CONTACT_PATTERNS:
        cleaned = pattern.sub("[removed]", cleaned)
    return cleaned[:MAX_RATIONALE_CHARS]
