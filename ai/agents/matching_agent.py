"""The Matching Agent.

Scores the freelancer pool for a project.

The scoring formula lives here, in Python, and is not negotiable by the model.
The model's contribution is one thing it is genuinely better at than arithmetic:
summarising *why* a candidate is or is not a fit, given their profile text and the
project text. The number comes from the formula; the sentence comes from the model.

That split is the whole design. A model asked for "a score" produces a number that
correlates with nothing reproducible, and two runs over the same pool would
disagree. A model asked for "three strengths and three gaps" produces prose that
is hard to get wrong and useful to a human reading a shortlist.
"""

from __future__ import annotations

import re
import time
from typing import Any, Literal
from uuid import UUID

from pydantic import BaseModel, Field

from ..skills.ontology import coverage
from ..skills.text_overlap import portfolio_relevance
from .base import BaseAgent
from .planning_agent import HardFilters, MatchWeights

#: Sub-score bounds. Fixed here so a factor can never drift outside 0-100.
SUB_SCORE_MIN = 0.0
SUB_SCORE_MAX = 100.0

#: Below this, a candidate is not put forward at all.
MIN_USABLE_SCORE = 20.0

MAX_REASON_CHARS = 200

#: Contact details must never appear in a reason. A shortlist is rendered to a
#: client, and a reason containing a phone number is a small data leak.
_CONTACT_PATTERNS = (
    re.compile(r"[\w.+-]+@[\w-]+\.[\w.]+"),          # email
    re.compile(r"\+?\d[\d\s().-]{7,}\d"),           # phone
    re.compile(r"https?://\S+", re.IGNORECASE),      # url
)


class SkillMatchDetail(BaseModel):
    required: str
    candidate: str | None = None
    match: bool = False
    #: Which rule produced the verdict. Carried all the way to the client so a
    #: match can be explained: "C# matched .NET" is a defensible sentence, and an
    #: unexplained match is not.
    resolution: Literal[
        "exact",
        "case_insensitive",
        "alias",
        "implies",
        "weakly_implies",
        "sibling_blocked",
        "missing",
    ] = "missing"
    #: How much of the requirement this satisfies, 0..1. Below 1.0 when a
    #: candidate has a related-but-not-identical skill -- a .NET developer for a
    #: C# brief, for instance. The client sees the gap; the number says how much
    #: credit it earned.
    credit: float = 0.0


class FreelancerScore(BaseModel):
    freelancer_id: UUID
    final_score: float
    skill_score: float
    portfolio_score: float
    past_success_score: float
    availability_score: float
    budget_score: float
    skill_detail: list[SkillMatchDetail] = Field(default_factory=list)
    strengths: list[str] = Field(default_factory=list)
    gaps: list[str] = Field(default_factory=list)
    reason: str = ""


class MatchingInput(BaseModel):
    workflow_run_id: int
    project_id: int
    project: dict[str, Any] | None = None
    weights: MatchWeights = Field(default_factory=MatchWeights)
    hard_filters: HardFilters = Field(default_factory=HardFilters)
    shortlist_size: int = 10


class MatchingOutput(BaseModel):
    recommendations: list[FreelancerScore] = Field(default_factory=list)
    top_pick_id: UUID | None = None
    #: Set when the pool produced nothing usable. Explaining an empty result is
    #: more useful to a client than an empty list.
    no_match_reason: str | None = None


class MatchingAgent(BaseAgent[MatchingInput, MatchingOutput]):
    name = "matching"

    async def run(self, payload: MatchingInput) -> MatchingOutput:
        started = time.monotonic()
        tools = self._toolbox(payload.workflow_run_id)

        project = payload.project
        if project is None:
            project = await tools.call("get_project", project_id=payload.project_id)

        # Not best-effort: with no pool there is nothing to score, and an empty
        # shortlist presented as "nobody matched" would be a lie.
        pool = await tools.call(
            "search_freelancers",
            category_id=project.get("categoryId"),
            min_rating=payload.hard_filters.min_rating or None,
            available=True if payload.hard_filters.require_available else None,
            limit=200,
        )
        pool = pool or []

        required_skills: list[str] = [str(s) for s in (project.get("skills") or [])]
        ceiling = self._budget_ceiling(project)

        scored: list[FreelancerScore] = []
        excluded: list[str] = []

        for candidate in pool:
            if self._excluded_by_filters(candidate, payload.hard_filters):
                excluded.append(str(candidate.get("id", "?")))
                continue

            scored.append(
                self._score_candidate(
                    candidate, project, required_skills, ceiling, payload.weights
                )
            )

        # Sort by score, then by id, so the ordering is stable across runs. Two
        # runs returning the same shortlist in a different order makes it
        # impossible to tell whether anything changed.
        scored.sort(key=lambda s: (-s.final_score, str(s.freelancer_id)))

        usable = [s for s in scored if s.final_score >= MIN_USABLE_SCORE]
        shortlist = usable[: payload.shortlist_size]

        # The model writes the reasons *after* ranking, so it is describing a
        # decision that has already been made. It cannot promote or demote anyone.
        shortlist = await self._annotate(tools, shortlist, project, payload.workflow_run_id)

        for candidate in shortlist:
            await self._client.append_recommendation(
                payload.workflow_run_id,
                {
                    "project_id": payload.project_id,
                    "freelancer_id": str(candidate.freelancer_id),
                    "score": round(candidate.final_score, 4),
                    # `portfolio_relevance` is null when the brief carried no
                    # requirements or deliverables to compare a portfolio
                    # against. Reporting 0.0 there would assert the freelancer
                    # showed nothing relevant, which was never established, so a
                    # consumer has to be able to tell "measured and zero" from
                    # "not measured".
                    "factors": {
                        "skill_coverage": round(candidate.skill_score, 4),
                        "portfolio_relevance": (
                            None
                            if self._portfolio_is_unmeasurable(project)
                            else round(candidate.portfolio_score, 4)
                        ),
                        "past_success_rate": round(candidate.past_success_score, 4),
                        "availability_and_speed": round(candidate.availability_score, 4),
                        "budget_fit": round(candidate.budget_score, 4),
                    },
                    "reason": candidate.reason,
                    "strengths": candidate.strengths[:5],
                    "gaps": candidate.gaps[:5],
                    "portfolio_samples": [s.model_dump() for s in candidate.skill_detail[:3]],
                },
            )

        result = MatchingOutput(
            recommendations=shortlist,
            top_pick_id=shortlist[0].freelancer_id if shortlist else None,
            # The diagnosis is computed from the shortlist, not from the pool.
            # Every branch of `explain_empty_shortlist` describes an *empty*
            # result, so calling it unconditionally would produce a reason for
            # shortlists that exist -- which is the bug the guard used to cause.
            no_match_reason=(
                None if shortlist
                else explain_empty_shortlist(
                    pool=pool,
                    required=required_skills,
                    excluded=excluded,
                    payload=payload,
                    top_score=max((s.final_score for s in scored), default=0.0),
                    anyone_covered=any(s.skill_score >= 100 for s in scored),
                )
            ),
        )

        await self._record_step(
            workflow_run_id=payload.workflow_run_id,
            step_name="matching",
            input_summary={
                "project_id": payload.project_id,
                "pool_size": len(pool),
                "shortlist_size": payload.shortlist_size,
            },
            output_summary={
                "candidates_scored": len(scored),
                "shortlisted": len(shortlist),
                "excluded_by_filters": len(excluded),
                "top_score": round(shortlist[0].final_score, 2) if shortlist else None,
            },
            status="ok" if shortlist else "no_matches",
            started=started,
        )
        return result

    # ── scoring ───────────────────────────────────────────────────────────

    def _score_candidate(
        self,
        candidate: dict[str, Any],
        project: dict[str, Any],
        required_skills: list[str],
        ceiling: float | None,
        weights: MatchWeights,
    ) -> FreelancerScore:
        detail, skill_score = self._score_skills(candidate, required_skills)

        portfolio_score = self._score_portfolio(candidate, project)
        success_score = self._score_success(candidate)
        availability_score = self._score_availability(candidate)
        budget_score = self._score_budget(candidate, ceiling)

        # The formula, in one place, never in a prompt.
        #
        # A factor that could not be measured contributes nothing and its weight
        # is NOT redistributed to the others. `portfolio_relevance` returns None
        # when the brief has no requirements or deliverables to compare against,
        # and substituting a number for it -- in either direction -- would be a
        # fabrication: 0.0 would claim the freelancer showed nothing relevant,
        # and 100.0 would claim they showed everything.
        #
        # Redistributing the weight was tried and reverted. Rescaling the
        # remaining factors to fill 1.0 multiplies every candidate's score by
        # 1/0.8 whenever the brief is unfilled, which is not a neutral handling
        # of missing data -- it inflates everybody and pushed borderline
        # candidates over MIN_USABLE_SCORE, so a pool with nothing going for it
        # produced a shortlist. Letting the weight go unused keeps the threshold
        # meaning the same thing whether or not the client filled in the brief,
        # and keeps scores comparable across projects.
        w = weights.as_dict()
        factors: dict[str, float | None] = {
            "skill_coverage": skill_score / 100.0,
            "portfolio_relevance": (
                None if portfolio_score is None else portfolio_score / 100.0
            ),
            "past_success_rate": success_score / 100.0,
            "availability_and_speed": availability_score / 100.0,
            "budget_fit": budget_score / 100.0,
        }

        final = SUB_SCORE_MAX * sum(
            w[name] * value
            for name, value in factors.items()
            if value is not None
        )

        matched = [d for d in detail if d.match]
        strengths, gaps = [], []
        for entry in detail:
            if entry.match:
                strengths.append(entry.required)
            else:
                gaps.append(entry.required)

        return FreelancerScore(
            freelancer_id=UUID(str(candidate.get("id"))),
            final_score=round(_clamp(final), 4),
            skill_score=round(skill_score, 4),
            # Recorded as 0.0 when unmeasurable, because this record is what a
            # client reads and there is no honest way to say "not measured" in a
            # numeric field. It is excluded from the final score above, so the
            # zero here cannot inflate or deflate anything -- and the emitted
            # factors dict carries None, which is the field a consumer should
            # branch on.
            portfolio_score=round(
                portfolio_score if portfolio_score is not None else 0.0, 4
            ),
            past_success_score=round(success_score, 4),
            availability_score=round(availability_score, 4),
            budget_score=round(budget_score, 4),
            skill_detail=detail,
            strengths=strengths[:5],
            gaps=gaps[:5],
            # Filled in by `_annotate`. The deterministic fallback below is what a
            # reader sees if the model is unavailable, because an empty reason is
            # worse than a plain one.
            reason=_fallback_reason(skill_score, matched, gaps),
        )

    def _score_skills(
        self, candidate: dict[str, Any], required: list[str]
    ) -> tuple[list[SkillMatchDetail], float]:
        """How much of the project's skill requirement this candidate covers.

        Resolution happens in :mod:`ai.skills.ontology`: normalisation, then an
        alias table, then a typed ontology with directional implications and
        explicit vetoes. All of it deterministic, all of it offline.

        This used to be `str.strip().lower()` equality, which meant "ReactJS" did
        not match "React" and "k8s" did not match "Kubernetes" -- the same skill
        written the way people write it, silently costing a strong candidate
        most of this factor.

        Scoring is a sum of *credits* rather than a count of matches, so a
        related-but-not-identical skill earns partial credit instead of nothing.
        With the ontology absent or a pair unlisted, every credit is 1.0 or 0.0
        and this reduces to the previous count exactly.
        """
        if not required:
            # Nothing required means nothing to miss. Full marks rather than zero:
            # a project with no skills listed is the client's omission, and
            # scoring everyone zero would return an empty shortlist.
            return [], SUB_SCORE_MAX

        owned = [str(s) for s in (candidate.get("skills") or []) if s]
        ratio, resolutions = coverage(required, owned)

        detail = [
            SkillMatchDetail(
                required=resolution.required,
                candidate=resolution.matched,
                match=resolution.matched_anything,
                resolution=resolution.resolution,
                credit=resolution.credit,
            )
            for resolution in resolutions
        ]

        return detail, SUB_SCORE_MAX * ratio

    def _score_portfolio(
        self, candidate: dict[str, Any], project: dict[str, Any]
    ) -> float | None:
        """How much of this brief the candidate's portfolio actually addresses.

        ## What this used to be

        ::

            evidence = len(portfolio) * 2 + len(packages)

        reported to clients under the name ``portfolio_relevance``. That counted
        rows while claiming to measure relevance, so a freelancer with five
        unrelated projects outscored one with two that were exactly the work
        being commissioned. Nothing caught it because the number was internally
        consistent -- it was simply the wrong number, honestly labelled.

        ## What it is now

        Term overlap between the brief and the portfolio text, resolved through
        the ontology so that "ReactJS" in a portfolio entry and "React" in the
        requirements are recognised as the same term. See
        :mod:`ai.skills.text_overlap` for what is read and, more importantly,
        what is deliberately not.

        Profile bio and tagline are not read. They are marketing copy, and
        scoring them rewards listing every technology you have ever touched.
        Project description and title are not read either: they are retrieval
        signals, and mixing them in rewards a brief for sharing vocabulary with
        a portfolio entry rather than for being answered.
        """
        score, _ = portfolio_relevance(
            requirements=str(project.get("requirements") or "") or None,
            expected_deliverables=str(project.get("expectedDeliverables") or "") or None,
            portfolio=candidate.get("portfolio") or [],
            packages=candidate.get("packages") or [],
        )
        return score

    def _portfolio_is_unmeasurable(self, project: dict[str, Any]) -> bool:
        """Whether the brief carries no text to compare a portfolio against.

        Exposed for tests because the distinction matters and is easy to undo:
        "relevance could not be measured" and "relevance is zero" are different
        statements, and conflating them either empties every shortlist or fills
        it with people who have shown no work at all.
        """
        return not any(
            str(project.get(key) or "").strip()
            for key in ("requirements", "expectedDeliverables")
        )

    def _score_success(self, candidate: dict[str, Any]) -> float:
        """Rating, weighted by how many reviews it rests on.

        A 5.0 from one review is not the same as a 5.0 from fifty, so the rating is
        scaled by confidence in it. No reviews is not zero -- it is unknown, and
        unknown is treated as neutral so a new freelancer is not buried.
        """
        rating = float(candidate.get("averageRating") or 0)
        count = int(candidate.get("reviewCount") or 0)

        if count == 0:
            return 50.0

        # Saturating: 20 reviews is plenty of evidence.
        confidence = min(1.0, count / 20.0)
        return _clamp(rating / 5.0 * 100.0 * confidence + 50.0 * (1 - confidence))

    def _score_availability(self, candidate: dict[str, Any]) -> float:
        if not candidate.get("availability", True):
            return 0.0

        # A faster stated turnaround is a genuine signal, and the packages carry
        # it. Used only as a tiebreak between otherwise similar candidates.
        days = [
            int(p.get("deliveryDays", 0))
            for p in (candidate.get("packages") or [])
            if p.get("deliveryDays")
        ]
        if not days:
            return 60.0
        return _clamp(100.0 - min(90.0, min(days) * 3.0))

    def _score_budget(self, candidate: dict[str, Any], ceiling: float | None) -> float:
        """How well the candidate's prices fit the project's ceiling.

        No ceiling means the project is open-ended, so everyone fits equally.
        """
        if ceiling is None or ceiling <= 0:
            return 70.0

        prices = [float(p.get("priceUsd", 0)) for p in (candidate.get("packages") or [])]
        prices = [p for p in prices if p > 0]
        if not prices:
            # No published prices. Neutral, not zero -- many freelancers quote per
            # project and have not set up packages.
            return 50.0

        cheapest = min(prices)
        if cheapest <= ceiling:
            # Fits. Closer to the ceiling is a better use of the client's budget,
            # but not dramatically so.
            return _clamp(60.0 + 40.0 * (cheapest / ceiling))
        return _clamp(max(0.0, 60.0 - 60.0 * ((cheapest - ceiling) / ceiling)))

    @staticmethod
    def _budget_ceiling(project: dict[str, Any]) -> float | None:
        pricing = str(project.get("pricingType") or "").lower()
        value = project.get("fixedPrice") if pricing == "fixed" else project.get("maxPrice")
        return float(value) if value else None

    @staticmethod
    def _excluded_by_filters(candidate: dict[str, Any], filters: HardFilters) -> bool:
        if filters.require_active and not candidate.get("isActive", True):
            return True
        if filters.require_available and not candidate.get("availability", True):
            return True
        if filters.min_rating:
            count = int(candidate.get("reviewCount") or 0)
            # Zero reviews passes: unproven is not the same as poor.
            if count > 0 and float(candidate.get("averageRating") or 0) < filters.min_rating:
                return True
        return False

    # ── reasons ───────────────────────────────────────────────────────────

    async def _annotate(
        self,
        tools,
        shortlist: list[FreelancerScore],
        project: dict[str, Any],
        workflow_run_id: int,
    ) -> list[FreelancerScore]:
        """Have the model write one sentence per shortlisted candidate.

        Given the already-computed scores, so it is describing rather than
        deciding. A failure leaves the deterministic fallback reason in place.
        """
        if not shortlist:
            return shortlist

        lines = []
        for candidate in shortlist:
            lines.append(
                f"- {candidate.freelancer_id}: skill {candidate.skill_score:.0f}, "
                f"portfolio {candidate.portfolio_score:.0f}, success "
                f"{candidate.past_success_score:.0f}, availability "
                f"{candidate.availability_score:.0f}, budget {candidate.budget_score:.0f}. "
                f"Matched: {', '.join(candidate.strengths) or 'none listed'}. "
                f"Missing: {', '.join(candidate.gaps) or 'nothing required'}"
            )

        prompt = (
            f"{self.read_prompt('matching')}\n\n"
            f"## Project\n{project.get('title')}\n"
            f"{str(project.get('description') or '')[:800]}\n\n"
            f"## Scored candidates\n" + "\n".join(lines) + "\n"
        )

        try:
            raw = await self._ask_model(prompt, workflow_run_id=workflow_run_id)
            reasons = _parse_reasons(raw)
        except Exception as exc:  # noqa: BLE001
            # The reasons are presentation. Losing them must not lose the
            # shortlist, which is the part that took the reads.
            return shortlist

        for candidate in shortlist:
            text = reasons.get(str(candidate.freelancer_id))
            if text:
                candidate.reason = _sanitise_reason(text)

        return shortlist


def explain_empty_shortlist(
    *,
    pool: list,
    required: list[str],
    excluded: list[str],
    payload: MatchingInput,
    top_score: float = 0.0,
    anyone_covered: bool = False,
) -> str:
    """Why the shortlist is empty, in terms the client can act on.

    Only called when the shortlist *is* empty -- there is nothing to explain
    otherwise. Renamed from `shortlist_nonempty_reason`, which read as "the
    reason the shortlist is nonempty" and is the opposite of what it returns.
    That inversion is not cosmetic: the caller that used to guard on this
    function's return value tested it for truthiness, and since every branch
    returns a string, the guard was always true, so `no_match_reason` was always
    `None` and the dispatcher fell through to a generic message every time. The
    diagnosis this function exists to produce -- supply problem, filters too
    strict, required skills too broad -- never reached a client.

    So the emptiness check now lives with the shortlist, where the shortlist is,
    and this function does the one job it was written for.

    `top_score` and `anyone_covered` are passed in rather than recomputed here,
    because the ranking has already happened and this function must not be able
    to disagree with it. The distinction matters: a pool of people who *do* cover
    the skills but score 15 against the weights is a different problem from a
    pool where nobody covers them, and the two need different advice. Before
    these two arguments existed, the same message claimed nobody had the skills
    in both cases -- telling a client their skills list was too broad when the
    real answer was that they had weighted price too heavily.
    """
    if not pool:
        if payload.hard_filters.require_active:
            return (
                "No active freelancer profiles match this project's category yet. "
                "This is a supply problem, not a matching problem -- widening the "
                "category would help more than re-running this."
            )
        return "No freelancer profiles match this project's category."

    if excluded:
        return (
            f"{len(pool)} freelancer(s) matched the category but all were filtered out "
            f"by the hard filters (active, available, rating >= "
            f"{payload.hard_filters.min_rating}). Relaxing the filters would surface them."
        )

    if required and not anyone_covered:
        return (
            f"{len(pool)} freelancer(s) were considered but none covers the required "
            f"skills ({', '.join(required[:5])}). Consider whether every listed skill "
            f"is genuinely essential."
        )

    return (
        f"{len(pool)} freelancer(s) were considered and the best scored "
        f"{top_score:.1f}, below the {MIN_USABLE_SCORE:.0f} needed to be shortlisted. "
        f"{'They cover the required skills, so the weighting is the likely cause. ' if anyone_covered else ''}"
        f"Adjust the weights or relax the filters and re-run."
    )


def _clamp(value: float) -> float:
    return max(SUB_SCORE_MIN, min(SUB_SCORE_MAX, value))


def _fallback_reason(
    skill_score: float, matched: list[SkillMatchDetail], gaps: list[str]
) -> str:
    if matched:
        base = f"Covers {', '.join(m.required for m in matched)}"
    else:
        base = "No required skills matched"
    if gaps:
        base += f"; missing {', '.join(gaps)}"
    return base[:MAX_REASON_CHARS]


def _parse_reasons(raw: str) -> dict[str, str]:
    """Pull ``{id: reason}`` out of the model's reply.

    Tolerant of shape: a list of objects, a dict keyed by id, or bare
    ``id: reason`` lines. A model asked for JSON will usually comply, and
    rejecting the whole batch over a formatting difference loses every reason
    when one is malformed.
    """
    import json

    text = raw.strip()
    start, end = text.find("{"), text.rfind("}")
    if start != -1 and end > start:
        try:
            parsed = json.loads(text[start : end + 1])
            if isinstance(parsed, dict):
                if "reasons" in parsed and isinstance(parsed["reasons"], dict):
                    return {str(k): str(v) for k, v in parsed["reasons"].items()}
                return {str(k): str(v) for k, v in parsed.items() if isinstance(v, str)}
        except json.JSONDecodeError:
            pass

    # Fall back to "id: reason" lines.
    reasons: dict[str, str] = {}
    for line in text.splitlines():
        match = re.match(r"^\s*[-*]?\s*([0-9a-fA-F-]{36})\s*[:\-]\s*(.+)$", line)
        if match:
            reasons[match.group(1)] = match.group(2).strip()
    return reasons


def _sanitise_reason(text: str) -> str:
    """Strip contact details and bound the length.

    A shortlist is rendered to a client. A reason containing an email or phone
    number is a data leak, and the model has no reason to include one.
    """
    cleaned = text.strip()
    for pattern in _CONTACT_PATTERNS:
        cleaned = pattern.sub("[removed]", cleaned)
    return cleaned[:MAX_REASON_CHARS]
