"""The validation gate.

Ten rules, evaluated in Python, in a fixed order, producing a verdict the model
has no ability to influence. The model may write ``human_summary`` -- a sentence
for a person to read -- and nothing else. If it disagrees with a rule, the rule
wins, because a model that could talk its way past a rule would be the rule not
doing its job.

Rule 9 (the output must satisfy the schema) is enforced by pydantic at parse
time, before these run. It is listed here for completeness against the plan's
numbering rather than reimplemented.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from decimal import Decimal
from typing import Any, Literal

Severity = Literal["error", "warning"]


@dataclass
class Issue:
    """One rule outcome."""

    rule: str
    severity: Severity
    message: str


@dataclass
class CostBreakdown:
    subtotal_usd: Decimal
    commission_usd: Decimal
    total_usd: Decimal
    currency: str = "USD"

    def as_dict(self) -> dict[str, Any]:
        return {
            "subtotal_usd": str(self.subtotal_usd),
            "commission_usd": str(self.commission_usd),
            "total_usd": str(self.total_usd),
            "currency": self.currency,
        }


@dataclass
class Facts:
    """Everything the rules are allowed to reason about.

    A flat bag of already-fetched values rather than live clients, so the rule
    set is a pure function and can be tested without HTTP. The agent fetches;
    the rules judge.
    """

    stage: str = "validate"
    project_id: int | None = None
    project: dict[str, Any] | None = None
    freelancer: dict[str, Any] | None = None
    proposal: dict[str, Any] | None = None
    shortlist: list[str] = field(default_factory=list)
    #: Freelancer ids the run actually recommended, for rule 10.
    recommended_ids: set[str] = field(default_factory=set)
    proposed_amount_usd: Decimal | None = None
    delivery_days: int | None = None
    match_score: float | None = None
    commission_percentage: Decimal | None = None
    min_rating_threshold: float = 0.0
    has_existing_contract: bool = False
    #: Problems encountered while *gathering* facts -- a read that failed, a
    #: check that could not run. Reported as issues rather than swallowed,
    #: because a gate that silently skips a rule is not a gate.
    #:
    #: Keyed by ``rule: message``. Severity is decided in
    #: :func:`_note_severity` rather than assumed, because "we could not check"
    #: is not equally serious in every place: not being able to read the
    #: *thresholds* is an inconvenience, while not being able to read the
    #: *freelancer* means the identity and phone rules never ran, and a contract
    #: would proceed against someone who may have been deactivated.
    notes: dict[str, str] = field(default_factory=dict)


@dataclass
class Verdict:
    valid: bool
    issues: list[Issue]
    cost_breakdown: CostBreakdown | None
    requires_human_approval: bool
    human_summary: str

    @property
    def errors(self) -> list[Issue]:
        return [i for i in self.issues if i.severity == "error"]

    @property
    def warnings(self) -> list[Issue]:
        return [i for i in self.issues if i.severity == "warning"]


def _money(value: Decimal) -> Decimal:
    return value.quantize(Decimal("0.01"))


def rule_1_amount_meets_bid(facts: Facts) -> Issue | None:
    """A client must not lowball a freelancer below what they asked for."""
    if facts.stage != "validate":
        return None
    if facts.proposed_amount_usd is None or not facts.proposal:
        return None

    bid = facts.proposal.get("bidAmount")
    if bid is None:
        return None

    if facts.proposed_amount_usd < Decimal(str(bid)):
        return Issue(
            "amount_meets_bid", "error",
            f"Proposed amount {facts.proposed_amount_usd} is below the freelancer's "
            f"bid of {bid}.",
        )
    return None


def rule_2_within_project_budget(facts: Facts) -> Issue | None:
    """The amount must fit inside the budget the client published.

    Reads ``fixedPrice`` for fixed-price projects and ``maxPrice`` for range
    projects. The plan calls these ``fixed_price`` and ``max_budget``; the actual
    projection field names are what matters, and they differ from the plan.
    """
    if facts.stage != "validate":
        return None
    if facts.proposed_amount_usd is None or not facts.project:
        return None

    pricing_type = (facts.project.get("pricingType") or "").lower()
    cap = (
        facts.project.get("fixedPrice")
        if pricing_type == "fixed"
        else facts.project.get("maxPrice")
    )

    if cap is None:
        return Issue(
            "within_project_budget", "error",
            "The project has no price ceiling, so there is nothing to validate against.",
        )

    if facts.proposed_amount_usd > Decimal(str(cap)):
        return Issue(
            "within_project_budget", "error",
            f"Proposed amount {facts.proposed_amount_usd} exceeds the project's "
            f"ceiling of {cap}.",
        )
    return None


def rule_3_delivery_within_window(facts: Facts) -> Issue | None:
    """The delivery window must fit the one the client published."""
    if facts.stage != "validate":
        return None
    if facts.delivery_days is None or not facts.project:
        return None

    expected = facts.project.get("expectedDays")
    if expected is None:
        return None

    if facts.delivery_days > int(expected):
        return Issue(
            "delivery_within_window", "error",
            f"Delivery of {facts.delivery_days} days exceeds the project's "
            f"{expected} days.",
        )
    return None


def rule_4_commission_matches_platform(facts: Facts) -> Issue | None:
    """The commission applied must equal the platform's current setting.

    A mismatch means a stale cached rate is being used, which is how a payout
    ends up disagreeing with the admin transaction report.
    """
    if facts.stage != "validate":
        return None
    if facts.commission_percentage is None:
        return None

    # The platform value is carried on the project read as the source of truth;
    # when absent this rule has nothing to compare and is skipped rather than
    # guessed at.
    expected = facts.project.get("commissionPercentage") if facts.project else None
    if expected is None:
        return None

    if facts.commission_percentage != Decimal(str(expected)):
        return Issue(
            "commission_matches_platform", "error",
            f"Commission {facts.commission_percentage}% does not match the platform "
            f"setting of {expected}%.",
        )
    return None


def rule_5_freelancer_active(facts: Facts) -> Issue | None:
    """An inactive freelancer cannot be hired.

    Runs at both stages: a deactivated account should drop out of matching, and
    must never reach a contract.
    """
    if not facts.freelancer:
        return None

    if not facts.freelancer.get("isActive", True):
        return Issue(
            "freelancer_active", "error",
            "This freelancer's profile is not active.",
        )
    return None


def rule_6_phone_verified(facts: Facts) -> Issue | None:
    """Payouts go by SMS, so an unverified number must block a contract.

    ``validate`` only. During matching, flagging it as an error would exclude
    every new freelancer from the shortlist before they have had a chance to
    verify -- which is the whole point of onboarding them.
    """
    if facts.stage != "validate":
        return None
    if not facts.freelancer:
        return None

    if not facts.freelancer.get("isPhoneVerified", False):
        return Issue(
            "phone_verified", "error",
            "The freelancer has not verified a phone number, so a payout cannot be sent.",
        )
    return None


def rule_7_no_existing_contract(facts: Facts) -> Issue | None:
    """A project can only have one contract."""
    if facts.stage != "validate" or facts.project_id is None:
        return None

    if facts.has_existing_contract:
        return Issue(
            "no_existing_contract", "error",
            "This project already has a contract.",
        )
    return None


def rule_8_rating_threshold(facts: Facts) -> Issue | None:
    """Low rating is a warning, not a bar.

    ``review_count == 0`` passes: a freelancer with no reviews is new, not
    unproven, and excluding everyone without history means the shortlist is only
    ever people who have already been paid by somebody.
    """
    if not facts.freelancer:
        return None

    review_count = int(facts.freelancer.get("reviewCount", 0) or 0)
    if review_count == 0:
        return None

    rating = float(facts.freelancer.get("averageRating", 0) or 0)
    if rating < facts.min_rating_threshold:
        return Issue(
            "rating_threshold", "warning",
            f"Average rating {rating} is below the platform threshold of "
            f"{facts.min_rating_threshold}.",
        )
    return None


def rule_9_schema_valid(_facts: Facts) -> Issue | None:
    """Always satisfied here.

    The plan lists this as rule 9. It is enforced by pydantic when the agent's
    output is parsed, which happens before this function is reached -- so by the
    time a verdict is produced, rule 9 holds by construction. Re-implementing it
    would mean a second, weaker definition of "valid".
    """
    return None


def rule_10_shortlist_was_recommended(facts: Facts) -> Issue | None:
    """Only people the run actually shortlisted may be invited.

    This is the rule that makes "the client approved a shortlist" mean something.
    Without it, a model that emitted an arbitrary uuid would reach the outreach
    path, and the .NET side would have no way to tell the difference.
    """
    if facts.stage != "talent_match" or not facts.shortlist:
        return None

    unknown = [fid for fid in facts.shortlist if fid not in facts.recommended_ids]
    if unknown:
        return Issue(
            "shortlist_was_recommended", "error",
            f"{len(unknown)} approved freelancer(s) were not in this workflow's "
            f"recommendations: {', '.join(unknown[:3])}"
            + ("..." if len(unknown) > 3 else ""),
        )
    return None


#: Evaluated in order. Order matters: the summary reads the first few, and a
#: caller that stops at the first error should stop at the most important one.
RULES = (
    rule_1_amount_meets_bid,
    rule_2_within_project_budget,
    rule_3_delivery_within_window,
    rule_4_commission_matches_platform,
    rule_5_freelancer_active,
    rule_6_phone_verified,
    rule_7_no_existing_contract,
    rule_8_rating_threshold,
    rule_9_schema_valid,
    rule_10_shortlist_was_recommended,
)


#: Read failures that must BLOCK rather than warn.
#:
#: Keyed by note name. These are the reads the safety verdict itself rests on: if
#: the project cannot be read, rules 1-3 and 7 did not run; if the freelancer
#: cannot be read, rules 5, 6 and 8 did not run. At the `validate` stage that is
#: the difference between "this contract is safe" and "we do not know", and only
#: one of those may be reported as a pass.
#:
#: At `talent_match` the same read is only a warning. Blocking a shortlist because
#: a profile lookup hiccuped would empty the list for a reason that has nothing to
#: do with the candidates.
_BLOCKING_READ_FAILURES = frozenset({"project_read_failed", "freelancer_read_failed"})


def _note_severity(rule_name: str, stage: str) -> Severity:
    if rule_name in _BLOCKING_READ_FAILURES and stage == "validate":
        return "error"
    return "warning"


def evaluate(facts: Facts) -> Verdict:
    """Run every rule and produce a verdict.

    All rules run, even after one fails. Stopping at the first would hide the rest
    of the problems, and a client who fixes one field and resubmits would
    discover the next problem on each attempt.
    """
    issues: list[Issue] = []
    for rule in RULES:
        issue = rule(facts)
        if issue is not None:
            issues.append(issue)

    # A rule that could not run is reported, at a severity that depends on what
    # could not be read. "We checked and could not check" is materially different
    # from "we checked and it was fine", and a reader shown the second when it is
    # really the first is how a bad contract ships.
    for rule_name, message in facts.notes.items():
        issues.append(Issue(rule_name, _note_severity(rule_name, facts.stage), message))

    breakdown = None
    if facts.proposed_amount_usd is not None and facts.commission_percentage is not None:
        subtotal = _money(Decimal(facts.proposed_amount_usd))
        commission = _money(subtotal * Decimal(facts.commission_percentage) / Decimal(100))
        breakdown = CostBreakdown(
            subtotal_usd=subtotal,
            commission_usd=commission,
            # The client pays subtotal + commission. The freelancer receives the
            # subtotal; the difference is the platform's.
            total_usd=_money(subtotal + commission),
        )

    errors = [i for i in issues if i.severity == "error"]
    warnings = [i for i in issues if i.severity == "warning"]

    return Verdict(
        # A warning never blocks. Only an error does -- otherwise a freelancer
        # with a 3.9 rating could never be hired, which is not what the threshold
        # is for.
        valid=not errors,
        issues=issues,
        cost_breakdown=breakdown,
        # A human should look whenever *anything* was flagged, including a warning.
        #
        # This previously counted errors and the amount only, so a run whose every
        # read had failed came back `requires_human_approval: false` -- the read
        # failures are warnings, and the verdict said nothing needed checking. That
        # is the exact shape of a gate that cannot be trusted: it reports "fine"
        # having run none of its rules.
        #
        # A warning is not a veto, so `valid` is unaffected. It is a request for
        # eyes on the result before money moves.
        requires_human_approval=(
            bool(errors) or bool(warnings) or facts.proposed_amount_usd is not None
        ),
        human_summary=_summarise(errors, warnings, breakdown),
    )


def _summarise(
    errors: list[Issue], warnings: list[Issue], breakdown: CostBreakdown | None
) -> str:
    """A sentence a person can act on.

    Generated here rather than by the model so it is never empty and never
    contradicts the verdict. The model may replace it with something more
    readable, but the authoritative version always exists.
    """
    if errors:
        return f"{len(errors)} blocking issue(s): " + "; ".join(e.message for e in errors[:3])
    if warnings:
        return f"No blocking issues, {len(warnings)} warning(s): " + "; ".join(
            w.message for w in warnings[:2]
        )
    if breakdown:
        return (
            f"No issues. Client pays {breakdown.total_usd} "
            f"({breakdown.subtotal_usd} plus {breakdown.commission_usd} commission)."
        )
    return "No issues found."
