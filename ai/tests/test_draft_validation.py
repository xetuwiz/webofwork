"""The draft agent's bounds.

## Why this file exists

`_validate` is the last thing between a model's output and a posted project. A
project with a 400-day delivery time or a negative price is visible to every
freelancer who searches it, and nothing downstream would complain -- the client
would simply publish something unusable.

So the corrections and the refusals are pinned here. The policy is **clamp where
clamping is obviously right, refuse only where it is not**:

- a delivery time of 400 days is a slip, so it is clamped and noted;
- a short title with no hint to fall back on is a refusal, because there is
  nothing to put in its place.

Both halves matter. Clamping everything would publish hollow projects; refusing
everything would fail a draft over a number the client can trivially fix.
"""

from __future__ import annotations

from decimal import Decimal

import pytest

from ai.agents.draft_agent import (
    MAX_DELIVERY_DAYS,
    MIN_DELIVERY_DAYS,
    MIN_DESCRIPTION_LENGTH,
    MIN_TITLE_LENGTH,
    DraftAgent,
    DraftInput,
    DraftOutput,
)
from ai.agents.base import AgentError
from ai.clients.dotnet_client import DotNetClient
from ai.config import Settings

KEY = "test-key-abc123"

CATEGORIES = [{"id": 1, "name": "Web Development"}, {"id": 2, "name": "Design"}]


@pytest.fixture
def agent() -> DraftAgent:
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

    return DraftAgent(client, categories=CATEGORIES)


def _draft(**overrides) -> DraftOutput:
    """A draft that passes everything, so each test breaks exactly one thing."""
    draft = DraftOutput(
        title="A customer portal with Stripe billing",
        description="A logged-in portal where customers see invoices and pay by card. " * 2,
        requirements="Stripe checkout and invoice history.",
        expected_deliverables="Deployed app and an admin view.",
        category_id=1,
        category_name="Web Development",
        skill_ids=[1],
        skill_names=["React"],
        pricing_type="fixed",
        fixed_price_usd=Decimal("2500.00"),
        min_budget_usd=None,
        max_budget_usd=None,
        delivery_days=30,
        confidence=0.8,
    )
    for key, value in overrides.items():
        setattr(draft, key, value)
    return draft


def _input(**overrides) -> DraftInput:
    base = {
        "workflow_run_id": 1,
        "partial_text": "We need a portal where people pay invoices by card.",
    }
    base.update(overrides)
    return DraftInput(**base)


def _validate(agent, draft, payload=None):
    agent._validate(draft, payload or _input())
    return draft


# ── the title ───────────────────────────────────────────────────────────


def test_a_good_title_is_left_alone(agent):
    before = "A customer portal with Stripe billing"
    draft = _validate(agent, _draft(title=before))
    assert draft.title == before


@pytest.mark.parametrize("short", ["x", "ab", "abc", "abcd"])
def test_a_short_title_with_no_hint_is_refused(agent, short):
    """There is nothing to put in its place, so this is the one hard refusal.

    The title is the only field a client sees in a search result, and a
    placeholder would publish a project nobody clicks.
    """
    with pytest.raises(AgentError) as exc:
        agent._validate(_draft(title=short), _input())

    assert "title" in str(exc.value).lower()
    assert str(MIN_TITLE_LENGTH) in str(exc.value)


def test_a_short_title_falls_back_to_the_clients_own_hint(agent):
    """With a hint, this is repairable rather than fatal.

    The hint is the client's own words, so using it is not the AI inventing a
    title on their behalf.
    """
    draft = _draft(title="abc")
    _validate(agent, draft, _input(title_hint="A proper project title from the client"))

    assert draft.title == "A proper project title from the client"
    assert any("too short" in note for note in draft.notes)


def test_a_hint_is_truncated_to_the_columns_width(agent):
    draft = _draft(title="abc")
    _validate(agent, draft, _input(title_hint="x" * 500))

    assert len(draft.title) <= 200


# ── the description ─────────────────────────────────────────────────────


def test_a_short_description_is_reported_but_not_padded(agent):
    """Reported, not padded.

    Padding would post a hollow project -- and the description is the first
    thing a freelancer reads, so a wall of filler is worse than a short one.
    """
    draft = _validate(agent, _draft(description="Too short."))

    assert draft.description == "Too short."
    assert any("Description is under" in note for note in draft.notes)
    assert any(str(MIN_DESCRIPTION_LENGTH) in note for note in draft.notes)


# ── delivery days ───────────────────────────────────────────────────────


@pytest.mark.parametrize("days", [-1, 0, 400, 10000])
def test_a_delivery_time_outside_the_range_is_clamped_and_noted(agent, days):
    draft = _validate(agent, _draft(delivery_days=days))

    assert MIN_DELIVERY_DAYS <= draft.delivery_days <= MAX_DELIVERY_DAYS
    assert any(str(days) in note for note in draft.notes)


@pytest.mark.parametrize("days", [MIN_DELIVERY_DAYS, 30, MAX_DELIVERY_DAYS])
def test_a_delivery_time_inside_the_range_is_untouched(agent, days):
    assert _validate(agent, _draft(delivery_days=days)).delivery_days == days


def test_a_negative_delivery_time_becomes_the_minimum_not_zero(agent):
    """Zero would fail the form's own validation, so the clamp goes to 1."""
    draft = _validate(agent, _draft(delivery_days=-5))
    assert draft.delivery_days == MIN_DELIVERY_DAYS
    assert draft.delivery_days >= 1


# ── pricing ─────────────────────────────────────────────────────────────


def test_a_fixed_project_with_no_price_falls_back_to_the_clients_budget(agent):
    draft = _validate(
        agent,
        _draft(fixed_price_usd=None),
        _input(budget_hint_usd=Decimal("1800.00")),
    )

    assert draft.fixed_price_usd == Decimal("1800.00")
    assert any("budget the client gave" in note for note in draft.notes)


def test_a_fixed_project_with_no_price_and_no_budget_is_reported(agent):
    """Not invented. A guessed price is a commitment nobody agreed to.

    The draft is still returned so the client can fill it in -- the form shows
    them what is missing rather than a confident wrong number.
    """
    draft = _validate(agent, _draft(fixed_price_usd=None))

    assert draft.fixed_price_usd is None
    assert any("Set one before posting" in note for note in draft.notes)


@pytest.mark.parametrize("price", [Decimal("0"), Decimal("-100")])
def test_a_zero_or_negative_fixed_price_is_not_accepted(agent, price):
    draft = _validate(agent, _draft(fixed_price_usd=price))

    assert draft.fixed_price_usd is None, "a non-positive price must not reach the form"


def test_range_values_on_a_fixed_project_are_cleared(agent):
    """A category error, not a default the client would notice.

    A "fixed" project with a budget range still displays both boxes, and the
    mismatch is not something the form validates.
    """
    draft = _validate(agent, _draft(
        pricing_type="fixed",
        min_budget_usd=Decimal("1000.00"),
        max_budget_usd=Decimal("2000.00"),
    ))

    assert draft.min_budget_usd is None
    assert draft.max_budget_usd is None


def test_a_range_project_with_no_minimum_is_reported(agent):
    draft = _validate(agent, _draft(
        pricing_type="range",
        min_budget_usd=None,
        max_budget_usd=Decimal("2000.00"),
    ))

    assert any("needs a minimum" in note for note in draft.notes)


def test_a_range_whose_max_is_below_its_min_has_its_maximum_cleared(agent):
    """Cleared, not set equal to the minimum.

    The form's schema is `min_budget < max_budget`, so equal values are rejected
    exactly as an inverted pair is. Setting `max = min` was a "correction" that
    produced a still-invalid value, which is worse than none because it looks
    handled.
    """
    draft = _validate(agent, _draft(
        pricing_type="range",
        min_budget_usd=Decimal("3000.00"),
        max_budget_usd=Decimal("2000.00"),
    ))

    assert draft.max_budget_usd is None
    # The minimum survives, so the client has one real number to work from.
    assert draft.min_budget_usd == Decimal("3000.00")
    assert any("maximum" in note.lower() for note in draft.notes), draft.notes


def test_an_inverted_range_does_not_survive_as_equal_values(agent):
    """The specific regression.

    Asserted separately because the failure is subtle: the draft still *has*
    both fields populated, and the only symptom is a 400 from the form.
    """
    draft = _validate(agent, _draft(
        pricing_type="range",
        min_budget_usd=Decimal("3000.00"),
        max_budget_usd=Decimal("3000.00"),
    ))

    if draft.max_budget_usd is not None:
        assert draft.max_budget_usd > draft.min_budget_usd, (
            "equal min and max is rejected by the form"
        )


def test_a_valid_range_survives_untouched(agent):
    draft = _validate(agent, _draft(
        pricing_type="range",
        fixed_price_usd=None,
        min_budget_usd=Decimal("1000.00"),
        max_budget_usd=Decimal("3000.00"),
    ))

    assert draft.min_budget_usd == Decimal("1000.00")
    assert draft.max_budget_usd == Decimal("3000.00")


# ── the contract as a whole ─────────────────────────────────────────────


def test_no_correction_is_silent(agent):
    """Every change produces a note.

    A draft that was quietly altered is a draft nobody can reason about
    afterwards, and the client sees these notes next to the fields they describe.
    """
    draft = _validate(agent, _draft(
        title="abc",
        delivery_days=900,
        fixed_price_usd=None,
        description="short",
    ), _input(title_hint="A usable title"))

    # Three corrections were made: the title, the delivery time and the price.
    # The short description is reported rather than corrected, and that is a
    # fourth note.
    assert len(draft.notes) >= 4


def test_a_well_formed_draft_produces_no_notes_at_all(agent):
    """The negative case, so the test above means something.

    Without it, an implementation that appended a note unconditionally would
    pass "every correction is recorded".
    """
    draft = _validate(agent, _draft())

    assert draft.notes == []


@pytest.mark.parametrize("pricing_type", ["fixed", "range"])
def test_validation_never_leaves_a_price_the_form_would_reject(agent, pricing_type):
    """The composition that matters.

    The React form validates `fixed_price > 0` for fixed and
    `min_budget < max_budget` for range. Whatever the model returned and whatever
    was corrected, the draft must be postable -- or the client gets a 400 they
    have no way to act on.

    Swept over inputs chosen to be un-repairable, because the earlier version of
    this test passed by only checking the values it happened to think of.
    """
    hostile_prices = [
        None, Decimal("-1"), Decimal("0"), Decimal("0.01"),
        Decimal("999999999"), Decimal("1"),
    ]
    hostile_ranges = [
        (None, None),
        (Decimal("5000"), Decimal("1000")),   # inverted
        (Decimal("3000"), Decimal("3000")),   # equal
        (Decimal("-100"), Decimal("-50")),     # both negative
        (Decimal("0"), Decimal("1000")),       # zero minimum
        (Decimal("1000"), None),               # no maximum
    ]

    for price in hostile_prices:
        draft = _validate(agent, _draft(
            pricing_type="fixed",
            fixed_price_usd=price,
            delivery_days=-3,
        ))

        assert draft.fixed_price_usd is None or draft.fixed_price_usd > 0, (
            f"a fixed price of {price} would be rejected by the form"
        )
        assert draft.min_budget_usd is None
        assert draft.max_budget_usd is None
        assert draft.delivery_days >= 1

    for low, high in hostile_ranges:
        draft = _validate(agent, _draft(
            pricing_type="range",
            min_budget_usd=low,
            max_budget_usd=high,
            delivery_days=-3,
        ))

        assert draft.fixed_price_usd is None
        assert draft.delivery_days >= 1
        if draft.max_budget_usd is not None and draft.min_budget_usd is not None:
            assert draft.max_budget_usd > draft.min_budget_usd, (
                f"range {draft.min_budget_usd}-{draft.max_budget_usd} would be "
                "rejected by the form"
            )