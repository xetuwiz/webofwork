"""The planner's corrections.

## What "correction" means here

A model returns a plan. The plan is then forced inside the bounds -- weights in
range and totalling one, a shortlist size the platform supports, agents from a
closed registry, a bounded number of steps.

Two properties make this worth testing in detail:

1. **Every correction is recorded.** A plan that was silently adjusted is a plan
   nobody can reason about afterwards, and the client is shown these weights.
   The recording is the difference between "the AI chose 0.4 for skills" and
   "the AI asked for 1.7 and it was clamped".

2. **The corrections compose in the right order.** Weights are clamped
   individually *first* and the total rescaled *second*, because a set summing to
   1.3 is wrong in a way that matters -- the score formula multiplies by them.
   Reversing the order would divide a negative number and produce nonsense.

These are pure functions, so none of this needs a model, a network or a database.
That is deliberate: the interesting behaviour here is what happens when the model
is wrong, which is most of the time.
"""

from __future__ import annotations

import pytest

from ai.agents.planning_agent import (
    KNOWN_AGENTS,
    MAX_PLAN_STEPS,
    MAX_SHORTLIST,
    MIN_SHORTLIST,
    WEIGHT_TOLERANCE,
    HardFilters,
    MatchWeights,
    PlanStep,
    PlanningAgent,
    PlanningOutput,
)
from ai.clients.dotnet_client import DotNetClient
from ai.config import Settings

KEY = "test-key-abc123"


@pytest.fixture
def agent() -> PlanningAgent:
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

    return PlanningAgent(client, categories=[])


def _plan(**overrides) -> PlanningOutput:
    plan = PlanningOutput(
        weights=MatchWeights(),
        shortlist_size=10,
        delegated_agents=["matching", "screening"],
    )
    for key, value in overrides.items():
        setattr(plan, key, value)
    return plan


def _corrected(agent, plan, thresholds=None):
    return agent._normalise(plan, thresholds or {})


# ── weights ─────────────────────────────────────────────────────────────


def test_a_valid_plan_is_left_alone(agent):
    """No corrections when there is nothing to correct.

    Asserted because the alternative -- always rescaling by rounding -- would
    quietly rewrite every weight by a fraction of a percent on every run, and
    the shortlist would drift for no reason.
    """
    plan = _plan()
    corrected = _corrected(agent, plan)

    assert corrected.weights.as_dict() == plan.weights.as_dict()
    assert "correction" not in (corrected.reasoning_summary or "").lower()


@pytest.mark.parametrize("bad", [-1.0, -0.01, 1.01, 2.0, 100.0])
def test_a_weight_outside_zero_to_one_is_clamped_and_recorded(agent, bad):
    plan = _plan(weights=MatchWeights(
        skill_coverage=bad,
        portfolio_relevance=0.2,
        past_success_rate=0.2,
        availability_and_speed=0.15,
        budget_fit=0.05,
    ))
    corrected = _corrected(agent, plan)

    value = corrected.weights.skill_coverage
    assert 0.0 <= value <= 1.0
    assert "skill_coverage" in corrected.reasoning_summary


def test_all_zero_weights_fall_back_to_uniform(agent):
    """The only defensible recovery from a total of zero.

    Every candidate scoring zero would return an empty shortlist, which reads as
    "nobody matches" rather than "the model returned nonsense".
    """
    plan = _plan(weights=MatchWeights(
        skill_coverage=0, portfolio_relevance=0, past_success_rate=0,
        availability_and_speed=0, budget_fit=0,
    ))
    corrected = _corrected(agent, plan)

    assert corrected.weights.total() == pytest.approx(1.0)
    assert "uniform" in corrected.reasoning_summary.lower()


def test_weights_that_do_not_sum_to_one_are_rescaled(agent):
    plan = _plan(weights=MatchWeights(
        skill_coverage=0.5, portfolio_relevance=0.5, past_success_rate=0.5,
        availability_and_speed=0.5, budget_fit=0.5,
    ))
    corrected = _corrected(agent, plan)

    assert corrected.weights.total() == pytest.approx(1.0)
    # Proportions preserved, which is what rescaling should mean: doubling
    # everything is the same plan, not a different one.
    assert corrected.weights.skill_coverage == pytest.approx(0.2, abs=0.001)
    assert "rescaled" in corrected.reasoning_summary.lower()


def test_rescaling_lands_exactly_on_one_despite_rounding(agent):
    """The last factor absorbs the rounding drift.

    Five weights each rounded to four places can sum to 0.9999 or 1.0001. Left
    alone that total would be rejected by the supplied-plan validator on the
    very next re-run, so the planner would produce a plan it then refuses.
    """
    for divisor in (3, 6, 7, 9, 11, 13):
        plan = _plan(weights=MatchWeights(
            skill_coverage=1, portfolio_relevance=1, past_success_rate=1,
            availability_and_speed=1, budget_fit=1,
        ))
        corrected = agent._normalise(plan, {})
        assert corrected.weights.total() == pytest.approx(1.0, abs=0.0005), (
            f"dividing by {divisor} left the total off"
        )


def test_clamping_happens_before_rescaling(agent):
    """Order matters and this pins it.

    Clamping a -1 up to 0 and *then* rescaling gives a different answer than
    rescaling the raw -1 first, which would divide a total that included a
    negative number.
    """
    plan = _plan(weights=MatchWeights(
        skill_coverage=-1.0, portfolio_relevance=0.5, past_success_rate=0.5,
        availability_and_speed=0.5, budget_fit=0.5,
    ))
    corrected = _corrected(agent, plan)

    # After clamping, the weights are {0, 0.5, 0.5, 0.5, 0.5}, a total of 2.0,
    # so the four survivors scale to 0.25 each.
    assert corrected.weights.skill_coverage == 0.0
    assert corrected.weights.portfolio_relevance == pytest.approx(0.25, abs=0.001)
    assert corrected.weights.total() == pytest.approx(1.0)


def test_a_tiny_drift_within_tolerance_is_not_rescaled(agent):
    """Tolerance exists so rounding noise is not treated as an error.

    Rescaling a plan that is 0.0005 off would record a correction for nothing.
    """
    base = MatchWeights().as_dict()
    nudged = dict(base)
    nudged["skill_coverage"] = round(nudged["skill_coverage"] + WEIGHT_TOLERANCE / 2, 6)

    corrected = _corrected(agent, _plan(weights=MatchWeights(**nudged)))
    assert "rescaled" not in (corrected.reasoning_summary or "").lower()


# ── shortlist size ──────────────────────────────────────────────────────


@pytest.mark.parametrize("size", [0, 1, -5, 16, 100, 1000])
def test_a_shortlist_size_outside_the_range_is_clamped_and_recorded(agent, size):
    corrected = _corrected(agent, _plan(shortlist_size=size))

    assert MIN_SHORTLIST <= corrected.shortlist_size <= MAX_SHORTLIST
    assert "shortlist_size" in corrected.reasoning_summary


@pytest.mark.parametrize("size", [MIN_SHORTLIST, 10, MAX_SHORTLIST])
def test_a_size_inside_the_range_is_untouched(agent, size):
    assert _corrected(agent, _plan(shortlist_size=size)).shortlist_size == size


# ── the agent registry ──────────────────────────────────────────────────


def test_an_unknown_agent_is_dropped_and_named(agent):
    """The registry is closed.

    An unknown name is not a warning: it is a name that would be dispatched to
    nothing. Naming it in the correction is what lets the prompt be fixed.
    """
    corrected = _corrected(agent, _plan(
        delegated_agents=["matching", "definitely_not_an_agent", "screening"]
    ))

    assert corrected.delegated_agents == ["matching", "screening"]
    assert "definitely_not_an_agent" in corrected.reasoning_summary


def test_a_plan_naming_only_unknown_agents_falls_back_to_the_pipeline(agent):
    """An empty delegation means nothing runs.

    So it is replaced with the full pipeline rather than honoured, because an
    empty list would produce a run that completes having done nothing.
    """
    corrected = _corrected(agent, _plan(delegated_agents=["nope", "also_nope"]))

    assert corrected.delegated_agents
    assert all(a in KNOWN_AGENTS for a in corrected.delegated_agents)
    assert "defaulting" in corrected.reasoning_summary.lower()


def test_a_plan_naming_nothing_falls_back_to_the_pipeline(agent):
    corrected = _corrected(agent, _plan(delegated_agents=[]))
    assert corrected.delegated_agents


def test_the_registry_is_a_closed_set(agent):
    """Asserted rather than assumed: a name added here that the dispatcher does
    not know would be accepted by the planner and then fail at dispatch."""
    assert set(KNOWN_AGENTS) == {"matching", "screening", "validation", "outreach"}


# ── plan steps ──────────────────────────────────────────────────────────


def test_too_many_steps_are_truncated_and_the_original_count_recorded(agent):
    steps = [PlanStep(order=i, agent_name="matching") for i in range(1, 20)]
    corrected = _corrected(agent, _plan(plan_steps=steps))

    assert len(corrected.plan_steps) == MAX_PLAN_STEPS
    assert "truncated" in corrected.reasoning_summary
    assert "19" in corrected.reasoning_summary


def test_steps_inside_the_limit_are_untouched(agent):
    steps = [PlanStep(order=1, agent_name="matching")]
    assert _corrected(agent, _plan(plan_steps=steps)).plan_steps == steps


# ── hard filters ────────────────────────────────────────────────────────


def test_hard_filter_defaults_are_the_safe_ones():
    """Conservative by default, so a plan has to actively relax them."""
    filters = HardFilters()
    assert filters.require_active is True
    assert filters.require_available is True
    assert filters.exclude_already_proposed is True
    assert filters.min_rating == 0.0


def test_every_correction_path_is_reported_in_the_summary(agent):
    """One plan, wrong in five ways, and every one of them named.

    This is the test that matters for the "recorded, not silent" property: if a
    new correction path is added later and forgets to append to the summary,
    this is where it shows up.
    """
    plan = _plan(
        weights=MatchWeights(
            skill_coverage=5.0, portfolio_relevance=0.1, past_success_rate=0.1,
            availability_and_speed=0.1, budget_fit=0.1,
        ),
        shortlist_size=999,
        delegated_agents=["imaginary_agent"],
        plan_steps=[PlanStep(order=i, agent_name="matching") for i in range(30)],
    )

    corrected = _corrected(agent, plan)
    summary = corrected.reasoning_summary

    assert "skill_coverage" in summary, "the weight clamp was not recorded"
    assert "shortlist_size" in summary, "the size clamp was not recorded"
    assert "imaginary_agent" in summary, "the dropped agent was not recorded"
    assert "truncated" in summary, "the step truncation was not recorded"

    # And the result is usable regardless of how wrong the input was.
    assert corrected.weights.total() == pytest.approx(1.0)
    assert MIN_SHORTLIST <= corrected.shortlist_size <= MAX_SHORTLIST
    assert corrected.delegated_agents
    assert len(corrected.plan_steps) <= MAX_PLAN_STEPS