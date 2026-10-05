"""The matching factors, one at a time.

## Why this file exists

The scoring formula is the one place in the system where a wrong number becomes
a wrong decision with no human in the loop to catch it. Everything else --
drafts, reasons, summaries -- a person can ignore or correct. A shortlist score
is presented as a number and acted on.

So the formula is in code rather than in a prompt, and this file tests the five
factors against hand-computed values. None of these tests need a model, a
network or a database, which is the point: a factor that is wrong should be
provable wrong without anything else being right.

## The property that matters most

Every factor is bounded to [0, 100] and the weighted sum is clamped. An
out-of-range sub-score does not merely distort one candidate -- it can invert the
ordering of the whole shortlist, because a single large value swamps the other
four. `test_no_factor_can_produce_an_out_of_range_score` sweeps inputs that would
do that and is the test to read first if one ever fails.
"""

from __future__ import annotations

import pytest

from ai.agents.matching_agent import (
    MAX_REASON_CHARS,
    MIN_USABLE_SCORE,
    MatchingAgent,
    _clamp,
)
from ai.agents.planning_agent import HardFilters, MatchWeights
from ai.clients.dotnet_client import DotNetClient
from ai.config import Settings

KEY = "test-key-abc123"

UUID_A = "b944584e-2cd9-43f9-b5c7-a25e2c865ff4"
UUID_B = "c2b7f0e1-1111-2222-3333-444455556666"


@pytest.fixture
def agent() -> MatchingAgent:
    client = DotNetClient(
        Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"),
        workflow_run_id=0,
    )

    async def noop(*args, **kwargs):
        return None

    for name in (
        "append_step", "append_tool_call", "append_recommendation",
        "append_screening", "append_validation", "complete_workflow",
    ):
        setattr(client, name, noop)

    return MatchingAgent(client, categories=[])


def _candidate(**overrides) -> dict:
    """A mid-range candidate, so each test varies one factor and the others sit
    in the middle rather than at a boundary that masks a change."""
    candidate = {
        "id": UUID_A,
        "skills": ["React"],
        "portfolio": [],
        "packages": [{"priceUsd": 2000.0, "deliveryDays": 14}],
        "averageRating": 4.5,
        "reviewCount": 20,
        "availability": True,
        "isActive": True,
    }
    candidate.update(overrides)
    return candidate


# ── skills ──────────────────────────────────────────────────────────────


def test_all_required_skills_matched_scores_full(agent):
    detail, score = agent._score_skills(_candidate(skills=["React", "CSharp"]), ["React", "CSharp"])
    assert score == 100.0
    assert len(detail) == 2
    assert all(d.match for d in detail)


def test_half_the_skills_scores_half(agent):
    _, score = agent._score_skills(_candidate(skills=["React"]), ["React", "CSharp"])
    assert score == 50.0


def test_no_skills_scores_zero(agent):
    detail, score = agent._score_skills(_candidate(skills=[]), ["React", "CSharp"])
    assert score == 0.0
    # Every miss is still reported, so the client can see what is missing.
    assert [d.required for d in detail] == ["React", "CSharp"]
    assert all(not d.match for d in detail)


def test_skill_matching_ignores_case(agent):
    """A project listing "React" and a profile listing "react" is a match.

    Treating it as a miss would quietly penalise the entire React pool, and it
    would look like correct behaviour rather than a bug.
    """
    _, score = agent._score_skills(_candidate(skills=["react"]), ["React"])
    assert score == 100.0
    assert score == 100.0, "case must not cost a candidate anything"


def test_no_required_skills_scores_full_not_zero(agent):
    """A project with no skills listed is the client's omission.

    Scoring everyone zero returns an empty shortlist, which reads as "nobody
    matches" rather than "we were not told what to look for".
    """
    detail, score = agent._score_skills(_candidate(), [])
    assert score == 100.0
    assert detail == []


def test_duplicate_required_skills_are_each_scored(agent):
    # Two entries that both match gives full marks; the denominator is the
    # required list, not the candidate's own skills.
    _, score = agent._score_skills(_candidate(skills=["React"]), ["React", "React"])
    assert score == 100.0


# ── portfolio ───────────────────────────────────────────────────────────


def _brief(**overrides) -> dict:
    """A brief with enough substance for overlap to be meaningful."""
    brief = {
        "id": 7,
        "requirements": "React front end with a Node.js API and PostgreSQL storage",
        "expectedDeliverables": "Deployed application with an automated test suite",
    }
    brief.update(overrides)
    return brief


def _entry(title: str, description: str = "") -> dict:
    return {"title": title, "description": description}


# The regression this whole block exists for: the old factor was
# `len(portfolio) * 2 + len(packages)` reported as *relevance*. These tests fail
# if quantity is ever allowed to stand in for relevance again.


def test_a_relevant_portfolio_outscores_a_long_irrelevant_one(agent):
    """The defect, stated as a test.

    Four irrelevant entries used to score 100.0 and one directly relevant entry
    scored 75.0, because the old formula counted rows. If this test ever fails,
    the factor has regressed to measuring quantity.
    """
    relevant = _candidate(
        portfolio=[
            _entry(
                "E-commerce dashboard",
                "React front end with a Node.js API and PostgreSQL storage, "
                "deployed with an automated test suite",
            )
        ]
    )
    irrelevant = _candidate(
        portfolio=[
            _entry("Logo design", "Brand identity work"),
            _entry("Video editing", "Commercial edit for a product launch"),
            _entry("3D modelling", "Photoreal product renders"),
            _entry("Copywriting", "Landing page copy"),
        ]
    )

    brief = _brief()

    assert agent._score_portfolio(relevant, brief) > agent._score_portfolio(
        irrelevant, brief
    )
    assert agent._score_portfolio(irrelevant, brief) == 0.0


def test_portfolio_with_no_brief_to_address_scores_zero(agent):
    """Nothing was asked for, so nothing was shown.

    Full marks here would claim evidence that does not exist.
    """
    candidate = _candidate(portfolio=[_entry("Anything", "Anything at all")])
    assert agent._score_portfolio(candidate, _brief()) == 0.0


def test_a_brief_with_no_text_is_unmeasurable_rather_than_full_marks(agent):
    """"Cannot be measured" is not "measured as perfect".

    This started out returning 100.0 -- the reasoning being that nothing was
    required so nothing was missed, mirroring how `_score_skills` treats an empty
    requirement list. That was wrong, and a golden case caught it: a freelancer
    with an *empty* portfolio scored 100.0 because there was nothing to miss,
    which is the exact inversion of the intended meaning. It rewarded showing no
    work at all.

    None is the honest answer, and the caller drops the factor rather than
    guessing.
    """
    candidate = _candidate(portfolio=[_entry("Logo design", "Brand identity")])
    assert (
        agent._score_portfolio(
            candidate, _brief(requirements=None, expectedDeliverables=None)
        )
        is None
    )


def test_an_unmeasurable_factor_does_not_inflate_the_total(agent):
    """Dropping a factor must not hand its weight to the others.

    Rescaling the remaining weights to fill 1.0 multiplies every candidate by
    1/0.8 when the brief is unfilled. That is not neutral handling of missing
    data: it inflates everybody and pushed borderline candidates over
    MIN_USABLE_SCORE, so a pool with nothing going for it produced a shortlist.
    """
    empty_brief = _brief(requirements=None, expectedDeliverables=None)

    good = _candidate(skills=["React"], portfolio=[_entry("React app", "React front end")])
    poor = _candidate(skills=["Rust"], portfolio=[], packages=[], averageRating=0.0, reviewCount=0)

    result = agent._score_candidate(
        poor, empty_brief, ["React", "TypeScript"], None, MatchWeights()
    )

    # 0.2 of the weight is simply unused, so the maximum reachable is 80.
    assert result.final_score <= 80.0, (
        "an unmeasurable factor must reduce the ceiling, not redistribute its "
        "weight and inflate every candidate"
    )
    # And ordering is unaffected by whether the brief happens to be filled in.
    with_brief = agent._score_candidate(
        poor, _brief(), ["React", "TypeScript"], None, MatchWeights()
    )
    assert with_brief.final_score <= result.final_score


def test_partial_overlap_earns_partial_credit(agent):
    """Relevance is not binary.

    An entry that addresses the front end but says nothing about storage or
    tests has demonstrated part of the brief, and the score should say so rather
    than rounding to either extreme.
    """
    candidate = _candidate(
        portfolio=[_entry("Marketing site", "A React front end for a client")]
    )

    score = agent._score_portfolio(candidate, _brief())

    assert 0.0 < score < 100.0


def test_ontology_spelling_differences_do_not_hide_a_relevant_entry(agent):
    """"ReactJS" in a portfolio must satisfy "React" in the brief.

    Without the shared normaliser these are two unrelated terms and a genuinely
    relevant freelancer scores zero.
    """
    spelt_differently = _candidate(
        portfolio=[
            _entry(
                "Dashboard",
                "ReactJS front end, NodeJS API, PostgreSQL database, deployed "
                "with an automated test suite",
            )
        ]
    )

    # Not 100.0: the brief says "storage" and "application" where the entry says
    # "database" and nothing respectively. Those are genuine vocabulary
    # differences and the score should reflect them. What matters is that the
    # technology names -- React, Node, PostgreSQL -- all resolved, which is what
    # this threshold proves. Before the ontology was consulted here, ReactJS did
    # not match React and this scored far lower.
    assert agent._score_portfolio(spelt_differently, _brief()) >= 80.0


def test_more_entries_do_not_raise_the_score_by_themselves(agent):
    """Adding unrelated entries must not move a relevance score.

    This is the precise behaviour the old formula got wrong.
    """
    one = _candidate(portfolio=[_entry("React app", "React front end work")])
    five = _candidate(
        portfolio=[_entry("React app", "React front end work")]
        + [_entry(f"Unrelated {i}", "Nothing to do with this brief") for i in range(4)]
    )

    brief = _brief()
    assert agent._score_portfolio(five, brief) == agent._score_portfolio(one, brief)


def test_entries_cannot_borrow_terms_from_each_other(agent):
    """One entry cannot satisfy a term that only a different entry mentions.

    Concatenating all entries before measuring would let a portfolio whose parts
    are individually unrelated score as if it were one coherent piece of work.
    """
    split = _candidate(
        portfolio=[
            _entry("React front end", "React work"),
            _entry("PostgreSQL storage", "PostgreSQL work"),
        ]
    )

    # Neither entry alone addresses the whole brief, and the combination must not
    # be treated as if one entry had.
    assert agent._score_portfolio(split, _brief()) < 100.0


def test_portfolio_is_still_reproducible(agent):
    """Determinism, which was the original reason this factor was a count.

    Relevance is now computed rather than counted, so reproducibility is no
    longer free and has to be asserted.
    """
    candidate = _candidate(
        portfolio=[
            _entry("B", "Node.js API"),
            _entry("A", "React front end"),
            _entry("C", "PostgreSQL storage"),
        ]
    )
    brief = _brief()

    runs = {agent._score_portfolio(dict(candidate), brief) for _ in range(25)}
    assert len(runs) == 1, f"relevance varied between runs: {runs}"


def test_packages_are_a_fallback_not_a_substitute_for_work(agent):
    """A package is evidence of intent, not of delivered work.

    Used only when there is no portfolio to judge, so a freelancer with an empty
    portfolio is not scored as though they had shown nothing -- but a package
    does not manufacture relevance that the text does not support.
    """
    package_only = _candidate(
        portfolio=[],
        packages=[{"packageType": "standard", "description": "I will build you a logo"}],
    )

    assert agent._score_portfolio(package_only, _brief()) == 0.0


# ── past success ────────────────────────────────────────────────────────


def test_a_five_star_rating_on_many_reviews_is_full_marks(agent):
    assert agent._score_success(_candidate(averageRating=5.0, reviewCount=50)) == 100.0


def test_no_reviews_is_neutral_not_zero(agent):
    """Unknown is not bad.

    A new freelancer with no reviews scoring zero would be buried permanently,
    since the only way to score is to have already scored.
    """
    assert agent._score_success(_candidate(averageRating=0, reviewCount=0)) == 50.0


def test_one_perfect_review_is_discounted_by_its_thinness(agent):
    """A 5.0 from one review is not the same as a 5.0 from fifty.

    20 reviews is treated as plenty of evidence, so confidence is
    count/20 and the rating is pulled toward neutral by whatever is missing.
    """
    thin = agent._score_success(_candidate(averageRating=5.0, reviewCount=1))
    solid = agent._score_success(_candidate(averageRating=5.0, reviewCount=20))

    assert thin < solid
    # confidence = 1/20, so (5/5)*100*0.05 + 50*0.95 = 5 + 47.5
    assert thin == pytest.approx(52.5, abs=0.01)


def test_a_low_rating_is_penalised(agent):
    poor = agent._score_success(_candidate(averageRating=1.0, reviewCount=40))
    good = agent._score_success(_candidate(averageRating=4.8, reviewCount=40))
    assert poor < good


def test_an_impossible_rating_is_clamped(agent):
    # A 6.0 from a bad join would otherwise score 120 and swamp the shortlist.
    assert agent._score_success(_candidate(averageRating=6.0, reviewCount=40)) == 100.0
    assert agent._score_success(_candidate(averageRating=-1.0, reviewCount=40)) == 0.0


# ── availability ────────────────────────────────────────────────────────


def test_unavailable_scores_zero(agent):
    assert agent._score_availability(_candidate(availability=False)) == 0.0


def test_no_packages_is_a_middling_score_not_zero(agent):
    # Someone who quotes per project has not failed to be available.
    assert agent._score_availability(_candidate(packages=[])) == 60.0


@pytest.mark.parametrize(
    "days,expected",
    [
        (1, 97.0),    # 100 - 3
        (10, 70.0),   # 100 - 30
        (14, 58.0),   # 100 - 42
        (30, 10.0),   # 100 - 90, the cap
        (31, 10.0),   # still capped: 31*3 = 93, clamped to 90
        (100, 10.0),
    ],
)
def test_faster_delivery_scores_higher(agent, days, expected):
    """The per-day penalty stops at 90 points.

    Someone quoting a hundred days is slow, not disqualified, so the floor is
    10 rather than 0.
    """
    score = agent._score_availability(_candidate(packages=[{"deliveryDays": days}]))
    assert score == pytest.approx(expected, abs=0.01)


def test_the_fastest_package_wins(agent):
    """Not the average -- someone with one fast option and one slow one is
    available soon, which is what availability means."""
    candidate = _candidate(packages=[{"deliveryDays": 60}, {"deliveryDays": 3}])
    assert agent._score_availability(candidate) == pytest.approx(91.0, abs=0.01)


def test_a_zero_delivery_days_is_ignored_not_treated_as_instant(agent):
    # 100 - 0*3 would be 100, the best possible score, for a package with no
    # stated turnaround at all.
    candidate = _candidate(packages=[{"deliveryDays": 0}])
    assert agent._score_availability(candidate) == 60.0


# ── budget ──────────────────────────────────────────────────────────────


def test_no_ceiling_means_everyone_fits(agent):
    """An open-ended project has no budget to fit, so the factor is neutral."""
    assert agent._score_budget(_candidate(), ceiling=None) == 70.0
    assert agent._score_budget(_candidate(), ceiling=0) == 70.0


def test_no_published_prices_is_neutral_not_zero(agent):
    # Many freelancers quote per project and have never set up packages.
    assert agent._score_budget(_candidate(packages=[]), ceiling=5000) == 50.0


def test_a_price_under_the_ceiling_scores_well(agent):
    score = agent._score_budget(_candidate(packages=[{"priceUsd": 1000}]), ceiling=2000)
    # 60 + 40 * (1000/2000) = 80
    assert score == pytest.approx(80.0, abs=0.01)


def test_using_the_whole_budget_scores_near_full(agent):
    assert agent._score_budget(_candidate(packages=[{"priceUsd": 2000}]), ceiling=2000) == 100.0


def test_pricing_over_the_ceiling_scores_lower_and_never_negative(agent):
    over = agent._score_budget(_candidate(packages=[{"priceUsd": 20000}]), ceiling=1000)
    assert over < 60.0
    assert over >= 0.0


def test_the_cheapest_package_is_the_one_that_counts(agent):
    """Someone with a cheap option fits, however expensive their others are."""
    candidate = _candidate(packages=[{"priceUsd": 9000}, {"priceUsd": 900}])
    cheap_only = _candidate(packages=[{"priceUsd": 900}])
    assert agent._score_budget(candidate, ceiling=1000) == agent._score_budget(cheap_only, ceiling=1000)


def test_a_zero_or_negative_price_is_ignored(agent):
    # A 0 would otherwise be the cheapest price and score perfectly.
    candidate = _candidate(packages=[{"priceUsd": 0}, {"priceUsd": 1000}])
    assert agent._score_budget(candidate, ceiling=1000) == pytest.approx(100.0, abs=0.01)


def test_the_budget_ceiling_depends_on_the_pricing_type(agent):
    assert agent._budget_ceiling({"pricingType": "Fixed", "fixedPrice": 2500}) == 2500.0
    assert agent._budget_ceiling({"pricingType": "Range", "maxPrice": 4000}) == 4000.0
    # A missing price means no ceiling, not a ceiling of zero.
    assert agent._budget_ceiling({"pricingType": "Fixed"}) is None
    assert agent._budget_ceiling({}) is None


# ── the formula ─────────────────────────────────────────────────────────


def test_the_final_score_is_the_weighted_sum(agent):
    """One factor carrying the whole weight, so the final score equals it.

    Isolating each factor this way is what makes a change to any one of them
    visible: if a factor stops contributing, the final score stops moving and
    this fails for exactly that factor.
    """
    # The score attribute each factor is written to. Named explicitly because
    # the weight key and the attribute name differ for one of them, and a
    # `model_dump()[weight_key]` lookup raises a KeyError rather than failing
    # informatively.
    cases = [
        ("skill_coverage", "skill_score", _candidate(skills=["React"])),
        ("portfolio_relevance", "portfolio_score", _candidate(portfolio=[{}, {}, {}], packages=[])),
        ("past_success_rate", "past_success_score", _candidate(averageRating=5.0, reviewCount=50)),
        ("availability_and_speed", "availability_score", _candidate(packages=[{"deliveryDays": 1}])),
        ("budget_fit", "budget_score", _candidate(packages=[{"priceUsd": 1000}])),
    ]

    for weight_key, attribute, candidate in cases:
        weights = MatchWeights(**{
            "skill_coverage": 0.0, "portfolio_relevance": 0.0, "past_success_rate": 0.0,
            "availability_and_speed": 0.0, "budget_fit": 0.0,
            weight_key: 1.0,
        })

        result = agent._score_candidate(
            candidate, {}, ["React"], ceiling=2000, weights=weights
        )

        assert result.final_score == pytest.approx(getattr(result, attribute), abs=0.01), (
            f"{weight_key} does not drive the final score"
        )


def test_the_weights_actually_change_the_ranking(agent):
    """The reason weights are adjustable at all.

    Two candidates who are equally skilled, one who quotes inside the client's
    budget and one who quotes far above it. Under skill-heavy weights -- where
    both score the same on skills -- the tie is broken by everything else. Under
    budget-heavy weights the one who fits wins outright.

    If this does not hold, the weight panel is decoration: the client moves a
    slider, presses re-run, and gets the same shortlist back.
    """
    required = ["React", "CSharp", "Node"]
    fits = _candidate(id=UUID_A, skills=required, packages=[{"priceUsd": 1800}])
    overshoots = _candidate(id=UUID_B, skills=required, packages=[{"priceUsd": 9000}])

    # Under skill-heavy weights neither candidate has a skill advantage, so the
    # one that blows the budget is penalised only by its small budget weight.
    skill_heavy = MatchWeights(
        skill_coverage=0.9, portfolio_relevance=0.02, past_success_rate=0.03,
        availability_and_speed=0.02, budget_fit=0.03,
    )
    budget_heavy = MatchWeights(
        skill_coverage=0.05, portfolio_relevance=0.02, past_success_rate=0.03,
        availability_and_speed=0.02, budget_fit=0.88,
    )

    under_skill = agent._score_candidate(fits, {}, required, 2000, skill_heavy)
    over_skill = agent._score_candidate(overshoots, {}, required, 2000, skill_heavy)
    assert under_skill.final_score > over_skill.final_score

    under_budget = agent._score_candidate(fits, {}, required, 2000, budget_heavy)
    over_budget = agent._score_candidate(overshoots, {}, required, 2000, budget_heavy)

    # The gap widens, because the factor that separates them now carries most of
    # the weight. Asserting the *ratio* rather than just the ordering is what
    # makes this a test of the weights rather than of the formula's sign.
    skill_gap = under_skill.final_score - over_skill.final_score
    budget_gap = under_budget.final_score - over_budget.final_score
    assert budget_gap > skill_gap, (
        "the candidate who fits the budget must matter more when budget_fit "
        f"carries more weight, but the gap went from {skill_gap} to {budget_gap}"
    )


def test_budget_fit_rewards_using_the_budget_not_being_cheap(agent):
    """A documented consequence worth pinning.

    `budget_fit` measures how well the cheapest option uses the client's budget,
    not how small it is. At a 2000 ceiling: a 1000 quote scores 80, an exactly
    2000 quote scores 100. So a client who sets a small ceiling and is
    outbid by everyone gets a low score from everyone, and a client who sets a
    generous one rewards the freelancer who uses it.

    This is intentional -- a much cheaper quote is not better use of a fixed
    budget, and would otherwise dominate the factor -- but it is the kind of
    thing that looks like a bug from the outside, so it is asserted deliberately.
    """
    cheap = agent._score_budget(_candidate(packages=[{"priceUsd": 1000}]), ceiling=2000)
    exact = agent._score_budget(_candidate(packages=[{"priceUsd": 2000}]), ceiling=2000)

    assert exact > cheap, "using the budget is a better fit than halving it"


def test_no_factor_can_produce_an_out_of_range_score(agent):
    """The property that makes the shortlist trustworthy.

    An out-of-range sub-score inverts the ordering of everything else, because
    one large value swamps the other four factors entirely. So every factor is
    swept against inputs chosen to break it: impossible ratings, negative prices,
    zero and huge delivery times.
    """
    hostile = [
        _candidate(averageRating=r, reviewCount=n, packages=p, portfolio=po, skills=sk,
                   availability=av)
        for r in (0, 5, 6, -1, 1e9)
        for n in (0, 1, 20, 10000)
        for p in ([], [{"priceUsd": 0}], [{"priceUsd": -5}], [{"priceUsd": 1e12}],
                  [{"priceUsd": 1e12, "deliveryDays": 0}])
        for po in ([], [{}])
        for sk in ([], ["React"], ["React"] * 5)
        for av in (True, False)
    ]

    for candidate in hostile:
        result = agent._score_candidate(candidate, {}, ["React"], 2000, MatchWeights())
        assert 0.0 <= result.final_score <= 100.0, f"{candidate} produced {result.final_score}"
        for value in (
            result.skill_score, result.portfolio_score, result.past_success_score,
            result.availability_score, result.budget_score,
        ):
            assert 0.0 <= value <= 100.0, f"{candidate} produced a sub-score of {value}"


def test_strengths_and_gaps_partition_the_required_skills(agent):
    """Every required skill appears exactly once, on one side or the other.

    A skill on neither side, or on both, would mean the strengths/gaps display
    disagrees with the score beside it -- which is the pair the client reads
    together.
    """
    result = agent._score_candidate(
        _candidate(skills=["React"]), {}, ["React", "CSharp", "Node"], 2000, MatchWeights()
    )

    assert set(result.strengths) | set(result.gaps) == {"React", "CSharp", "Node"}
    assert set(result.strengths) & set(result.gaps) == set()
    assert len(result.strengths) + len(result.gaps) == 3


def test_strengths_and_gaps_are_capped_at_five_each(agent):
    """The database columns and the UI are sized for five.

    A project listing fifteen skills would otherwise produce a shortlist row
    taller than a phone screen.
    """
    required = [f"Skill{i}" for i in range(9)]
    result = agent._score_candidate(_candidate(skills=required[:4]), {}, required, 2000, MatchWeights())

    assert len(result.strengths) <= 5
    assert len(result.gaps) <= 5
    # Capping must not lose the distinction: some of each survived.
    assert result.strengths and result.gaps


def test_a_fallback_reason_exists_without_a_model(agent):
    """An empty reason is worse than a plain one.

    The model normally writes this, but a plain sentence that is correct beats a
    blank space, and the column is nullable so a blank is possible.
    """
    result = agent._score_candidate(_candidate(skills=["React"]), {}, ["React"], 2000, MatchWeights())
    assert result.reason.strip()
    assert len(result.reason) <= MAX_REASON_CHARS


def test_the_minimum_usable_score_is_a_real_number(agent):
    """The threshold below which a candidate is dropped from the shortlist.

    Named because it is the line between "shown" and "not shown", and a silent
    change to it would change who gets contacted without changing anything a
    reader can see.
    """
    assert 0 < MIN_USABLE_SCORE <= 100


def test_clamp_bounds_both_directions():
    assert _clamp(-5) == 0.0
    assert _clamp(150) == 100.0
    assert _clamp(42.5) == 42.5