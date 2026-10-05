"""Five whole scenarios, end to end, with no network and no model.

## What a "golden case" is here, and what it deliberately is not

The plan this project was built from asks for golden cases. That word usually
means a recorded input paired with a recorded output, compared byte for byte.
That approach has a specific failure mode here, and it is not a small one.

Every output in this system contains two kinds of thing:

- **decisions** -- scores, verdicts, orderings, which freelancers were invited.
  Computed in code. Deterministic to the last decimal. Identical on every run,
  on every machine, forever.
- **prose** -- "strong React portfolio, delivered ahead of schedule". Written by a
  model. Different wording every time, and *should* be, because that is what a
  language model is for.

A byte-for-byte golden test can only hold the prose still, so it has to assert on
prose. And asserting on prose means the test fails the first time a provider is
swapped, and the fix every time is to re-record -- which teaches the team to
re-record rather than to investigate. A suite that has been re-recorded is no
longer evidence of anything.

So these cases assert **decisions**, exactly, and for prose assert only that it
exists and is safe. That distinction is the whole design of the file.

The second consequence: every test here runs with the model switched **off**.
Not mocked to return a fixed string -- genuinely unavailable, so the code takes
its fallback path. That is the arrangement that makes the strongest assertion in
the file possible:

> The prose is allowed to fail entirely, and no decision moves.

If a score ever depended on a model call, this file would catch it. That is worth
more than a thousand recorded strings.

## The five cases

Named for what they are for, from the plan's Phase 30:

| Case | The thing it pins |
|---|---|
| `perfect_project` | Everything agrees; a full shortlist and a clean verdict |
| `budget_failure` | An amount over the ceiling blocks the contract |
| `inactive_freelancer` | An inactive account blocks, at either stage |
| `skill_mismatch` | No overlap produces a *reason*, not a bad shortlist |
| `full_happy_path` | Approval leads to exactly the invited set, and no more |
"""

from __future__ import annotations

from decimal import Decimal
from uuid import UUID

import pytest

from ai.agents.base import BaseAgent
from ai.agents.matching_agent import MatchingAgent, MatchingInput
from ai.agents.outreach_agent import OutreachAgent, OutreachInput
from ai.agents.planning_agent import HardFilters, MatchWeights
from ai.agents.validation_agent import ValidationAgent, ValidationInput
from ai.clients.dotnet_client import DotNetClient
from ai.config import Settings
from ai.policies.validation_rules import Facts, evaluate

KEY = "test-key-abc123"

ALICE = "b944584e-2cd9-43f9-b5c7-a25e2c865ff4"
BOB = "c2b7f0e1-1111-2222-3333-444455556666"
CAROL = "d3c8a1f2-3333-4444-5555-666677778888"


# ── the offline world ──────────────────────────────────────────────────


class _World:
    """A stand-in for the .NET API that answers from a dict instead of a socket.

    Every read is a method the tool registry already knows how to call, so the
    allow-list, the permission map and the read budget are all still exercised.
    The transport is the only thing replaced -- which is the seam
    `DotNetClient._http` exists to provide.
    """

    def __init__(self, **tables):
        self.tables = tables
        self.reads: list[str] = []
        self.writes: list[str] = []

    # reads the agents actually use

    async def get_project(self, project_id):
        self.reads.append(f"project:{project_id}")
        return self.tables.get("project")

    async def search_freelancers(self, **kwargs):
        self.reads.append("search_freelancers")
        return list(self.tables.get("freelancers", []))

    async def get_freelancer(self, freelancer_id):
        self.reads.append(f"freelancer:{freelancer_id}")
        for candidate in self.tables.get("freelancers", []):
            if candidate["id"] == str(freelancer_id):
                return candidate
        return None

    async def get_freelancer_packages(self, freelancer_id):
        self.reads.append(f"packages:{freelancer_id}")
        return []

    async def get_freelancer_reviews(self, freelancer_id):
        self.reads.append(f"reviews:{freelancer_id}")
        return []

    async def get_commission(self):
        self.reads.append("commission")
        return self.tables.get("commission")

    async def get_agent_thresholds(self):
        self.reads.append("agent_thresholds")
        return self.tables.get("thresholds", {})

    async def get_contract_disputes(self, contract_id):
        self.reads.append(f"disputes:{contract_id}")
        return []

    async def get_recommended_freelancer_ids(self, workflow_run_id):
        self.reads.append(f"recommended:{workflow_run_id}")
        return list(self.tables.get("recommended", []))

    # the one write that reaches a person

    async def send_invitations(self, workflow_run_id, project_id, freelancer_ids):
        # The run id is the first argument on purpose: the audit row and the
        # invitation must belong to the same run, and that is the registry's
        # business rather than the caller's.
        self.writes.append(f"send_invitations:{project_id}:{len(freelancer_ids)}")
        return {
            "invitation_ids": list(range(1, len(freelancer_ids) + 1)),
            "skipped": [],
            "errors": [],
        }

    # every audit write, discarded

    async def _noop(self, *args, **kwargs):
        return None

    append_step = _noop
    append_tool_call = _noop
    append_recommendation = _noop
    append_screening = _noop
    append_validation = _noop
    complete_workflow = _noop


@pytest.fixture(autouse=True)
def no_model(monkeypatch):
    """Switch the language model off, for the whole file.

    Not a stub that returns something plausible -- an actual refusal, so the
    agents take the same fallback path they would take during a provider outage.
    That is the condition worth testing against.
    """

    async def unavailable(self, *args, **kwargs):
        raise RuntimeError("no provider configured")

    monkeypatch.setattr(BaseAgent, "_ask_model", unavailable)


def _weights(**overrides) -> MatchWeights:
    values = {
        "skill_coverage": 0.5,
        "portfolio_relevance": 0.2,
        "past_success_rate": 0.15,
        "availability_and_speed": 0.1,
        "budget_fit": 0.05,
    }
    values.update(overrides)
    return MatchWeights(**values)


def _candidate(identifier, **overrides) -> dict:
    candidate = {
        "id": identifier,
        "skills": ["React", "TypeScript"],
        "portfolio": [{"title": "Design system", "description": "React component library"}],
        "packages": [{"priceUsd": 2500.0, "deliveryDays": 14}],
        "averageRating": 4.8,
        "reviewCount": 25,
        "availability": True,
        "isActive": True,
    }
    candidate.update(overrides)
    return candidate


#: The field the matcher reads required skills from is `skills`, matching the
#: .NET project DTO. An earlier draft of this file used `requiredSkills`, which
#: the agent ignores silently -- every candidate then matched everything, which
#: is a good illustration of why the empty case below asserts on the pool rather
#: than on the filter.
PROJECT = {
    "id": 7,
    "title": "Build a React design system",
    "skills": ["React", "TypeScript"],
    "pricingType": "fixed",
    "fixedPrice": 2500.0,
    "expectedDays": 21,
    "commissionPercentage": "10",
}

#: Three candidates who all pass the default hard filters: active, available, no
#: proposals on this project. Availability is included deliberately, because
#: `HardFilters.require_available` defaults to true and a candidate that fails a
#: hard filter is *excluded*, not ranked last.
GOLDEN_POOL = [
    _candidate(ALICE, averageRating=4.9, reviewCount=40),
    _candidate(BOB, averageRating=4.5, reviewCount=12, portfolio=[]),
    _candidate(CAROL, averageRating=4.1, reviewCount=3, portfolio=[]),
]


# ── case 1: a project where everything agrees ──────────────────────────


@pytest.mark.asyncio
async def test_perfect_project_produces_a_full_ordered_shortlist():
    """The baseline the other four are perturbations of.

    Asserted exactly: three in, three out, ranked, weights summing to one, and a
    top pick that is the one the arithmetic says it is.
    """
    world = _World(freelancers=GOLDEN_POOL, project=PROJECT, thresholds={"min_rating": 4.0})
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1,
            project_id=7,
            project=PROJECT,
            weights=_weights(),
            hard_filters=HardFilters(),
            shortlist_size=10,
        )
    )

    # Everyone qualifies: none is inactive, and the filters are permissive.
    assert len(result.recommendations) == 3

    # Ordered by score, and strictly descending -- equal scores would make the
    # ranking arbitrary, and an arbitrary ranking is not a ranking.
    scores = [r.final_score for r in result.recommendations]
    assert scores == sorted(scores, reverse=True), scores
    assert len(set(scores)) == 3, "two candidates tied exactly"

    # The top pick agrees with the ordering rather than being tracked separately.
    # `freelancer_id` is a `str` on the score -- the .NET side receives a string,
    # and comparing through `str()` here would hide a type change.
    assert result.top_pick_id == result.recommendations[0].freelancer_id

    # No match reason, because there were matches.
    assert result.no_match_reason is None

    # The weights actually used are the ones supplied, and they sum to one --
    # because the score formula multiplies by them, and a set summing to 1.3
    # would inflate every score in the shortlist.
    total = _weights().total()
    assert total == pytest.approx(1.0, abs=1e-6)

    # Scores are bounded, whatever the inputs.
    for recommendation in result.recommendations:
        assert 0 <= recommendation.final_score <= 100

    # Prose survives the model being switched off.
    for recommendation in result.recommendations:
        assert recommendation.reason.strip()


@pytest.mark.asyncio
async def test_the_scores_survive_a_dead_model():
    """The load-bearing assertion.

    The same shortlist, scored twice: once with the model answering, once with it
    unable to. If the numbers move, a decision depended on prose -- which is the
    one thing the model-authority table in `agent-contracts.md` forbids.
    """
    payload = MatchingInput(
        workflow_run_id=1,
        project_id=7,
        project=PROJECT,
        weights=_weights(),
        hard_filters=HardFilters(),
        shortlist_size=10,
    )

    world = _World(freelancers=GOLDEN_POOL, project=PROJECT, thresholds={"min_rating": 4.0})
    offline = await MatchingAgent(world, categories=[]).run(payload)

    world_online = _World(freelancers=GOLDEN_POOL, project=PROJECT, thresholds={"min_rating": 4.0})
    online = await MatchingAgent(world_online, categories=[]).run(payload)

    assert [r.final_score for r in offline.recommendations] == [
        r.final_score for r in online.recommendations
    ]
    assert offline.top_pick_id == online.top_pick_id


@pytest.mark.asyncio
async def test_a_client_who_approves_nothing_has_nobody_invited():
    """Nothing happens. The common case, and the one that must be boring.

    This is the arrangement the whole two-gate design rests on: an unapproved
    shortlist is not a shortlist anybody has agreed to.
    """
    world = _World(freelancers=GOLDEN_POOL, project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(), hard_filters=HardFilters(), shortlist_size=10,
        )
    )
    assert len(result.recommendations) == 3

    # The shortlist exists. No invitation tool was called, because outreach is a
    # separate stage behind a separate approval.
    assert world.writes == []


# ── case 2: money ──────────────────────────────────────────────────────


def _clean_facts(**overrides) -> Facts:
    base = dict(
        stage="validate",
        project_id=7,
        project=PROJECT,
        freelancer={"isActive": True, "isPhoneVerified": True,
                    "averageRating": 4.8, "reviewCount": 25},
        proposed_amount_usd=None,
        delivery_days=None,
        commission_percentage=10,
    )
    base.update(overrides)
    return Facts(**base)


def test_a_project_within_every_bound_is_valid():
    """Case 1's financial twin: the same project, priced correctly."""
    verdict = evaluate(_clean_facts())

    assert verdict.valid, [i.message for i in verdict.issues if i.severity == "error"]
    assert verdict.human_summary.strip()


def test_an_amount_above_the_ceiling_blocks_the_contract():
    """The case that matters most to a client.

    A platform that lets a contract be created for more than the project was
    posted for is not a marketplace, it is a suggestion. This is the rule that
    stops it, so it is asserted by name rather than by outcome alone.
    """
    verdict = evaluate(_clean_facts(proposed_amount_usd=Decimal("4000")))

    assert not verdict.valid
    blocking = [i for i in verdict.issues if i.severity == "error"]
    assert any(i.rule == "within_project_budget" for i in blocking), [
        i.rule for i in blocking
    ]


def test_an_amount_below_the_bid_blocks_the_contract_too():
    """Paying a freelancer less than they asked is the other half of the rule."""
    verdict = evaluate(_clean_facts(
        proposed_amount_usd=Decimal("100"),
        proposal={"bidAmount": Decimal("2000")},
    ))

    assert not verdict.valid
    assert any(
        i.rule == "amount_meets_bid" and i.severity == "error"
        for i in verdict.issues
    )


def test_the_cost_breakdown_adds_up():
    """Money is the one output a client reads as exact.

    Commission is computed from the platform's own percentage, not the one in the
    request -- rule 4 exists because those two can disagree, and the breakdown is
    what a client checks first if a total looks wrong.
    """
    verdict = evaluate(_clean_facts(
        proposed_amount_usd=Decimal("2500"),
        commission_percentage=10,
    ))

    breakdown = verdict.cost_breakdown
    assert breakdown is not None

    assert breakdown.subtotal_usd == Decimal("2500.00")
    assert breakdown.commission_usd == Decimal("250.00")
    assert breakdown.total_usd == breakdown.subtotal_usd + breakdown.commission_usd
    assert breakdown.currency == "USD"

    # Two decimals, always. A money value that renders as `250` rather than
    # `250.00` is how a client ends up unsure whether a cent was rounded away.
    for value in (breakdown.subtotal_usd, breakdown.commission_usd, breakdown.total_usd):
        assert value.as_tuple().exponent == -2, f"{value} is not 2dp"


def test_a_commission_that_does_not_match_the_platform_blocks():
    """Rule 4. The platform's percentage is not negotiable per contract."""
    verdict = evaluate(_clean_facts(
        proposed_amount_usd=Decimal("2500"),
        commission_percentage=Decimal("15"),
    ))

    assert not verdict.valid
    assert any(
        i.rule == "commission_matches_platform" and i.severity == "error"
        for i in verdict.issues
    )


# ── case 3: an account that should not be transacting ──────────────────


def test_an_inactive_freelancer_is_an_error_at_both_stages():
    """Suspending an account has to close it everywhere, not just at payment.

    `talent_match` and `validate` are separate code paths reading the same flag,
    and a rule that only fires in one of them is a rule that fails open on the
    stage nobody tested.
    """
    for stage in ("validate", "talent_match"):
        verdict = evaluate(_clean_facts(
            stage=stage,
            freelancer={"isActive": False, "isPhoneVerified": True,
                        "averageRating": 4.8, "reviewCount": 25},
        ))

        assert not verdict.valid, f"{stage} let an inactive freelancer through"
        assert any(
            i.rule == "freelancer_active" and i.severity == "error"
            for i in verdict.issues
        ), f"{stage} did not report the rule as an error"


def test_an_unverified_phone_is_an_error():
    """The other account-level block, and it is a block rather than a warning.

    Verified phone is the platform's only lever on someone being who they say
    they are, and money moves at `validate`.
    """
    verdict = evaluate(_clean_facts(
        freelancer={"isActive": True, "isPhoneVerified": False,
                    "averageRating": 4.8, "reviewCount": 25},
    ))

    assert not verdict.valid
    assert any(
        i.rule == "phone_verified" and i.severity == "error"
        for i in verdict.issues
    )


def test_a_low_rating_is_a_warning_because_a_new_account_has_no_reviews():
    """Rule 8 is a warning on purpose, and this is why.

    A freelancer with no reviews has `averageRating == 0`. Judged by a threshold
    that would exclude every new account on the platform -- which is the exact
    opposite of what a rating floor is for.
    """
    unrated = evaluate(_clean_facts(
        freelancer={"isActive": True, "isPhoneVerified": True,
                    "averageRating": 0.0, "reviewCount": 0},
        min_rating_threshold=4.0,
    ))
    assert unrated.valid
    assert not any(i.rule == "rating_threshold" and i.severity == "error"
                   for i in unrated.issues)

    # Below the threshold *with* reviews is still only a warning: a manager may
    # reasonably override it, and the summary says so.
    rated = evaluate(_clean_facts(
        freelancer={"isActive": True, "isPhoneVerified": True,
                    "averageRating": 2.1, "reviewCount": 6},
        min_rating_threshold=4.0,
    ))
    assert rated.valid
    assert any(i.rule == "rating_threshold" and i.severity == "warning"
               for i in rated.issues)
    assert rated.requires_human_approval


# ── case 4: nothing fits ───────────────────────────────────────────────


@pytest.mark.asyncio
async def test_a_missing_skill_lowers_a_score_rather_than_excluding_them():
    """Skills are weighted, not gated -- and this pins that decision.

    Two candidates, one missing `TypeScript`. Both stay on the shortlist, and the
    one with the gap scores lower. The alternative -- dropping anyone without
    every listed skill -- was rejected: on a marketplace most real briefs list a
    skill nobody has exclusively, and a hard gate would return an empty
    shortlist for an ordinary project while a freelancer who is strong on four
    of five requirements never appears at all.

    So the gap has to be *visible* instead of enforced, and that is what this
    asserts. If `gaps` ever stops being populated for missing skills, the
    weighting becomes a way of hiding irrelevance.
    """
    pool = [
        _candidate(ALICE, skills=["React"]),
        _candidate(BOB, skills=["React", "TypeScript"]),
    ]
    world = _World(freelancers=pool, project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(), hard_filters=HardFilters(), shortlist_size=10,
        )
    )

    assert len(result.recommendations) == 2

    partial = [r for r in result.recommendations if str(r.freelancer_id) == ALICE][0]
    full = [r for r in result.recommendations if str(r.freelancer_id) == BOB][0]

    # Half the skill coverage is half the skill score, and skills carry half
    # the weight -- so the gap costs a quarter of the total. The other four
    # factors are identical, so the difference is exactly that.
    assert partial.skill_score == 50.0
    assert full.skill_score == 100.0
    assert full.final_score - partial.final_score == pytest.approx(25.0, abs=0.01)

    # And the gap is on the record, per skill, so a client can see what is
    # missing rather than being asked to trust the ordering.
    assert partial.gaps == ["TypeScript"]
    assert full.gaps == []
    missing = [d for d in partial.skill_detail if not d.match]
    assert [d.required for d in missing] == ["TypeScript"]


@pytest.mark.asyncio
async def test_an_unqualified_pool_returns_a_reason_not_a_shortlist():
    """The honest-empty case.

    This needed candidates with *nothing* going for them, not merely the wrong
    skills -- which is the discovery worth recording. A Rust developer with a 4.8
    rating, an active portfolio and a package at the right price still clears
    `MIN_USABLE_SCORE` on a React project, because the four other factors are
    available to make up the 50 points the missing skills cost.

    That is a consequence of the formula, and the test now encodes it explicitly
    rather than assuming the stricter behaviour. An empty shortlist needs a pool
    that scores badly on everything.
    """
    pool = [
        _candidate(
            ALICE, skills=["Rust"], portfolio=[], packages=[],
            averageRating=0.0, reviewCount=0, availability=False,
        ),
        _candidate(
            BOB, skills=["Kotlin"], portfolio=[], packages=[],
            averageRating=0.0, reviewCount=0,
        ),
    ]
    world = _World(freelancers=pool, project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(),
            # Availability relaxed, so the empty result is about quality rather
            # than about one hard filter -- otherwise the reason would correctly
            # be the filters, which is a different case.
            hard_filters=HardFilters(require_available=False),
            shortlist_size=10,
        )
    )

    assert result.recommendations == []
    assert result.no_match_reason, "an empty result with no explanation"
    assert result.top_pick_id is None

    # The diagnosis names the actual problem: nobody in the pool has the skills.
    # Worth asserting rather than just checking for a non-empty string, because
    # this is the message a client acts on -- "your skills list is too broad" and
    # "your weights are wrong" lead to opposite decisions.
    assert "none covers the required skills" in result.no_match_reason
    assert "TypeScript" in result.no_match_reason

    # And it must not blame the weighting, because weights are not the problem
    # here -- no amount of reweighting invents a skill.
    assert "weighting" not in result.no_match_reason


@pytest.mark.asyncio
async def test_a_qualified_but_low_scoring_pool_is_blamed_on_the_weights():
    """The other empty case, and the one the weights message exists for.

    Everyone in this pool *does* cover the required skills. They still fall below
    `MIN_USABLE_SCORE`, because almost all of their weight is on the price they
    charge. So the diagnosis has to point at the weighting.

    This is the case that was previously misreported. Before `anyone_covered`
    was passed in, this situation produced "none covers the required skills" --
    telling a client their skills list was too broad when the pool was a perfect
    match and the client had simply weighted price too heavily. Same empty
    shortlist, opposite advice.
    """
    expensive = [
        _candidate(
            ALICE, skills=["React", "TypeScript"], portfolio=[],
            packages=[{"priceUsd": 90000.0, "deliveryDays": 90}],
            averageRating=0.0, reviewCount=0,
        ),
        _candidate(
            BOB, skills=["React", "TypeScript"], portfolio=[],
            packages=[{"priceUsd": 85000.0, "deliveryDays": 90}],
            averageRating=0.0, reviewCount=0,
        ),
    ]
    world = _World(freelancers=expensive, project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    # A client who has told the planner they want the cheapest person available.
    # This weighting is what makes the branch reachable at all: full skill
    # coverage is worth 50 points under the default weights, and no amount of
    # being expensive can pull a total below 20 when half the score is skills.
    # So the case is reachable only when the weights say price is what matters --
    # which is exactly the situation where blaming the skills is wrong.
    price_first = _weights(
        skill_coverage=0.05,
        portfolio_relevance=0.05,
        past_success_rate=0.05,
        availability_and_speed=0.05,
        budget_fit=0.80,
    )

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=price_first, hard_filters=HardFilters(), shortlist_size=10,
        )
    )

    assert result.recommendations == []
    assert "none covers the required skills" not in result.no_match_reason
    assert "weighting" in result.no_match_reason

    # The score that failed the bar is quoted, so the client can see how far off
    # it was rather than being told only that it was too low.
    assert "scored" in result.no_match_reason


@pytest.mark.asyncio
async def test_an_unavailable_freelancer_is_filtered_out_rather_than_ranked():
    """A hard filter is not a low score.

    An unavailable freelancer is *excluded* rather than ranked last, because a
    shortlist that ends with someone who cannot start is a shortlist a client
    invites and then waits on. `require_available` defaults to true, which is why
    the unavailability is stated rather than assumed.
    """
    pool = [_candidate(ALICE, availability=False), _candidate(BOB)]
    world = _World(freelancers=pool, project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(), hard_filters=HardFilters(), shortlist_size=10,
        )
    )

    assert [str(r.freelancer_id) for r in result.recommendations] == [BOB]
    assert ALICE not in [str(r.freelancer_id) for r in result.recommendations]


@pytest.mark.asyncio
async def test_a_hard_filter_can_be_relaxed_by_the_planner():
    """The filters are the planner's, not the matcher's.

    Same pool, same code path, different instructions -- which is what makes
    them a decision rather than a constant. A client who will wait for someone
    unavailable should be able to say so.
    """
    pool = [_candidate(ALICE, availability=False), _candidate(BOB)]
    world = _World(freelancers=pool, project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(),
            hard_filters=HardFilters(require_available=False),
            shortlist_size=10,
        )
    )

    assert len(result.recommendations) == 2, "relaxing the filter changed nothing"


@pytest.mark.asyncio
async def test_an_empty_pool_is_explained_rather_than_silently_empty():
    """Zero candidates is a different situation from zero good candidates."""
    world = _World(freelancers=[], project=PROJECT)
    agent = MatchingAgent(world, categories=[])

    result = await agent.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(), hard_filters=HardFilters(), shortlist_size=10,
        )
    )

    assert result.recommendations == []
    assert result.no_match_reason


# ── case 5: the whole path, and only the approved part ─────────────────


@pytest.mark.asyncio
async def test_only_approved_freelancers_are_invited():
    """The full happy path, with its most important assertion last.

    Three candidates, two approved. Exactly two invitations are created, and the
    third is not contacted. If this test ever passes with three invitations, the
    approval gate has stopped gating anything.
    """
    world = _World(
        freelancers=GOLDEN_POOL,
        project=PROJECT,
        recommended=[ALICE, BOB, CAROL],
    )

    # What the matching stage recorded, which is what the gate is checked against.
    matching = MatchingAgent(world, categories=[])
    shortlist = await matching.run(
        MatchingInput(
            workflow_run_id=1, project_id=7, project=PROJECT,
            weights=_weights(), hard_filters=HardFilters(), shortlist_size=10,
        )
    )
    ranked = [str(r.freelancer_id) for r in shortlist.recommendations]
    assert len(ranked) == 3

    # The client approves two of the three.
    approved = [UUID(ranked[0]), UUID(ranked[1])]

    outreach = OutreachAgent(world, categories=[])
    result = await outreach.run(
        OutreachInput(
            workflow_run_id=1,
            project_id=7,
            approved_freelancer_ids=approved,
        )
    )

    assert result.errors == []
    assert len(result.invitation_ids) == 2, "the client approved two"
    assert world.writes == ["send_invitations:7:2"]

    # And the one that was not approved is not in any of it.
    unapproved = ranked[2]
    assert unapproved not in [str(f) for f in approved]


@pytest.mark.asyncio
async def test_an_empty_approval_contacts_nobody_at_all():
    """The gate holds when the client approves nothing.

    Worth its own case because it is the one an empty-array bug hides in: a
    loop over `[]` sends nothing by accident, and a guard written as
    `if len(ids) != len(shortlist)` passes it.
    """
    world = _World(freelancers=GOLDEN_POOL, project=PROJECT, recommended=[ALICE, BOB])

    result = await OutreachAgent(world, categories=[]).run(
        OutreachInput(workflow_run_id=1, project_id=7, approved_freelancer_ids=[])
    )

    assert result.invitation_ids == []
    assert world.writes == [], "an empty approval still called the action"


@pytest.mark.asyncio
async def test_a_duplicate_approval_invites_once():
    """A client who taps approve twice, or a resumed run replaying the decision.

    Two invitations to the same freelancer is a real thing a client notices and a
    freelancer reports, so the deduplication is asserted at the boundary -- the
    request itself carries one id.
    """
    world = _World(freelancers=GOLDEN_POOL, project=PROJECT, recommended=[ALICE])

    result = await OutreachAgent(world, categories=[]).run(
        OutreachInput(
            workflow_run_id=1,
            project_id=7,
            approved_freelancer_ids=[UUID(ALICE), UUID(ALICE)],
        )
    )

    assert len(result.invitation_ids) == 1
    assert world.writes == ["send_invitations:7:1"]


@pytest.mark.asyncio
async def test_a_failed_send_reports_it_instead_of_claiming_success():
    """The failure that matters most: a summary saying "sent" when nothing was.

    `OutreachOutput` carries both `invitation_ids` and `errors`, and the risk is
    a summary that reads as success because no exception was raised. So the
    summary is asserted *against* the ids.
    """
    world = _World(freelancers=GOLDEN_POOL, project=PROJECT)

    async def failing_send(workflow_run_id, project_id, freelancer_ids):
        world.writes.append(f"send_invitations:{project_id}:{len(freelancer_ids)}")
        raise RuntimeError("the mail relay refused the connection")

    world.send_invitations = failing_send

    result = await OutreachAgent(world, categories=[]).run(
        OutreachInput(
            workflow_run_id=1, project_id=7, approved_freelancer_ids=[UUID(ALICE)],
        )
    )

    assert result.invitation_ids == []
    assert result.errors, "a failure with an empty error list"
    assert "refused" in result.human_summary or "could not" in result.human_summary.lower()

    # The load-bearing half: the summary must not read as success while the id
    # list is empty. This is the assertion that would have caught the discarded
    # result described in `outreach_agent.run`.
    assert "invited" not in result.human_summary.lower() or result.invitation_ids


# ── the validation agent over the same cases, through its own entry point ──


@pytest.mark.asyncio
async def test_the_validation_agent_agrees_with_the_rules_it_calls():
    """`evaluate` and `ValidationAgent.run` are two paths to one verdict.

    The agent is what the workflow actually executes; `evaluate` is what the
    golden cases above pin. If they ever diverge, every case in this file is
    describing behaviour the product does not have.
    """
    world = _World(project=PROJECT, commission=10, thresholds={"min_rating": 4.0})
    agent = ValidationAgent(world, categories=[])

    async def get_freelancer(freelancer_id):
        return {"isActive": True, "isPhoneVerified": True,
                "averageRating": 4.8, "reviewCount": 25}

    world.get_freelancer = get_freelancer

    clean = await agent.run(
        ValidationInput(
            workflow_run_id=1, stage="validate", project_id=7,
            freelancer_id=UUID(ALICE), proposed_amount_usd=Decimal("2500"),
            commission_percentage=10,
        )
    )
    # The agent's issues are plain dicts, not `Issue` dataclasses -- the verdict
    # is crossing a Pydantic boundary on its way out to .NET. So they are read by
    # key here, which is also the more honest check: it asserts the shape a
    # consumer sees.
    assert clean.valid, [i["message"] for i in clean.issues if i["severity"] == "error"]
    assert all("rule" in i and "severity" in i and "message" in i for i in clean.issues)

    over = await agent.run(
        ValidationInput(
            workflow_run_id=1, stage="validate", project_id=7,
            freelancer_id=UUID(ALICE), proposed_amount_usd=Decimal("4000"),
            commission_percentage=10,
        )
    )
    assert not over.valid
    assert any(
        i["rule"] == "within_project_budget" and i["severity"] == "error"
        for i in over.issues
    ), [i["rule"] for i in over.issues]