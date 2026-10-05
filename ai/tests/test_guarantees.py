"""Tests that do not need a model, a network, or a database.

The point of this file is that the *guarantees* are checked even when no LLM is
reachable. "The validation agent is deterministic" and "a hallucinated id is
dropped" and "an agent cannot call a tool it was not granted" are properties of
the Python, and they must hold with the model switched off.
"""

import json
import uuid

import httpx
import pytest
from fastapi.testclient import TestClient
from pydantic import ValidationError

from ai.config import Settings
from ai.main import app
from ai.policies.validation_rules import Facts, evaluate
from ai.tools.registry import (
    AGENT_TOOL_PERMISSIONS,
    TOOL_ALLOWLIST,
    ToolBox,
    ToolNotPermittedError,
    UnknownToolError,
)
from ai.agents.screening_agent import band_for
from ai.agents.validation_agent import ValidationInput, ValidationAgent


KEY = "test-key-abc123"


@pytest.fixture
def client():
    return TestClient(app)


# ── auth ────────────────────────────────────────────────────────────────

def test_health_is_open(client):
    assert client.get("/health").status_code == 200


def test_agent_chat_without_key_is_401(client):
    r = client.post("/agent/chat", json={"workflow_run_id": 1, "stage": "draft"})
    assert r.status_code == 401


def test_agent_chat_with_wrong_key_is_401(client):
    r = client.post(
        "/agent/chat",
        json={"workflow_run_id": 1, "stage": "draft"},
        headers={"X-Internal-Api-Key": "not-the-key"},
    )
    assert r.status_code == 401


def test_wrong_and_missing_key_are_indistinguishable(client):
    """A 401 must not confirm whether a key is configured.

    Different bodies or headers for "missing" versus "wrong" turn the endpoint
    into an oracle for guessing keys.
    """
    missing = client.post("/agent/chat", json={"workflow_run_id": 1, "stage": "draft"})
    wrong = client.post(
        "/agent/chat",
        json={"workflow_run_id": 1, "stage": "draft"},
        headers={"X-Internal-Api-Key": "wrong"},
    )
    assert missing.status_code == wrong.status_code == 401
    assert missing.json() == wrong.json()


# ── the tool gate ───────────────────────────────────────────────────────

class _StubClient:
    def __init__(self):
        self.reads = 0
        self.read_budget = 20
        self.writes = []

    async def append_tool_call(self, *a, **k):
        self.writes.append(a)


def test_every_permission_names_only_allow_listed_tools():
    for agent, tools in AGENT_TOOL_PERMISSIONS.items():
        assert tools <= TOOL_ALLOWLIST, f"{agent} names a tool that is not allow-listed"


def test_allow_listed_tools_all_have_handlers():
    # The registry asserts this at import; repeated here so a regression names
    # the cause rather than surfacing as an opaque RuntimeError.
    from ai.tools.registry import _HANDLERS
    assert TOOL_ALLOWLIST <= set(_HANDLERS)


def test_outreach_agent_cannot_read_the_freelancer_pool():
    box = ToolBox("outreach", 1, _StubClient())
    assert not box.permits("get_freelancer")
    assert not box.permits("search_freelancers")
    assert box.permits("send_invitations")


def test_screening_agent_cannot_read_the_project_brief():
    box = ToolBox("screening", 1, _StubClient())
    assert not box.permits("get_project")
    assert box.permits("get_freelancer")


# ── the price bands the drafter quotes from ─────────────────────────────
#
# The drafter previously had no way to answer "what does this usually cost?" and
# so declined to: a client who posted a brief with no budget got "no price is
# suggested". That is the whole value of a drafting assistant, withdrawn, and it
# failed silently -- the draft looked complete, because a missing capability and
# a considered decision produce the same output.
#
# These pin the wiring, not the model's judgement. Whether it picks the *right*
# band is an evaluation question; that it *can* is a contract question, and a
# contract is what a test is for.


def test_the_drafter_may_read_the_price_bands():
    box = ToolBox("draft", 1, _StubClient())
    assert box.permits("get_price_bands"), (
        "without this the drafter cannot price anything the client did not price "
        "for themselves, which is the case that most needs help"
    )


def test_the_price_bands_are_read_only_and_cost_read_budget():
    """A reference an agent could write would be an agent quoting itself."""
    from ai.tools.registry import READ_TOOLS

    assert "get_price_bands" in READ_TOOLS, (
        "it is a read, so a workflow must not be able to starve itself of its "
        "own write-back budget by consulting it"
    )
    assert not any(
        "get_price_bands" in tools
        for agent, tools in AGENT_TOOL_PERMISSIONS.items()
        if agent in ("outreach",)
    ), "the outreach agent sends invitations; it has no reason to price anything"


def test_the_price_band_schema_tells_the_model_they_are_bands_not_quotes():
    """The words that keep the model from presenting a reference as a price.

    This is prompt text, so it cannot be asserted by behaviour -- only pinned. It
    is pinned because it is the only thing standing between an advertised band
    and a client being told what their work costs, and because the source data is
    genuinely weaker than the claim: observed freelancer rates on the same
    marketplace run well outside these ranges at both ends.
    """
    from ai.agents.draft_agent import DRAFTER_TOOL_SCHEMAS

    schema = next(
        s for s in DRAFTER_TOOL_SCHEMAS if s["function"]["name"] == "get_price_bands"
    )
    description = schema["function"]["description"].lower()

    assert "reference" in description
    assert "not quotes" in description or "not a quote" in description
    assert "source" in description, (
        "the model has to be able to attribute the number, or it is asserting it"
    )


def test_the_drafter_is_told_to_price_from_the_bands():
    """A tool that is never named in the request is a tool that is never called.

    Pinned on the objective rather than the system prompt, because the objective
    is the last thing read before the model decides what to do.
    """
    import inspect

    from ai.agents.draft_agent import DraftAgent

    source = inspect.getsource(DraftAgent._draft_with_tools)
    assert "get_price_bands" in source, (
        "the drafter must be told, in the request itself, to look a band up when "
        "the client gave no budget"
    )


def test_unknown_agent_is_refused_outright():
    with pytest.raises(ValueError):
        ToolBox("not_an_agent", 1, _StubClient())


@pytest.mark.asyncio
async def test_calling_a_forbidden_tool_raises():
    box = ToolBox("outreach", 1, _StubClient())
    with pytest.raises(ToolNotPermittedError):
        await box.call("get_freelancer", freelancer_id=str(uuid.uuid4()))


@pytest.mark.asyncio
async def test_calling_an_unknown_tool_raises():
    box = ToolBox("validation", 1, _StubClient())
    with pytest.raises(UnknownToolError):
        await box.call("drop_database", table="users")


# ── read budget ─────────────────────────────────────────────────────────

def _settings(**overrides):
    base = dict(
        internal_api_key=KEY,
        dotnet_base_url="https://dotnet.invalid",
        max_internal_reads_per_workflow=3,
    )
    base.update(overrides)
    return Settings(**base)


@pytest.mark.asyncio
async def test_read_budget_stops_the_runaway_before_the_request_is_made():
    """The budget is enforced in DotNetClient, and *before* the call.

    Checked after the request would mean the breaching read still happened and
    was still paid for. So the assertion is on the httpx layer: it must be
    called exactly `budget` times, not budget + 1.
    """
    from ai.clients.dotnet_client import BudgetExceededError, DotNetClient

    calls = []

    class _Transport(httpx.AsyncBaseTransport):
        async def handle_async_request(self, request):
            calls.append(str(request.url))
            return httpx.Response(200, json={"id": 1})

    settings = _settings()
    client = DotNetClient(settings, workflow_run_id=7)

    original = httpx.AsyncClient

    def patched(*args, **kwargs):
        kwargs["transport"] = _Transport()
        return original(*args, **kwargs)

    httpx.AsyncClient = patched
    try:
        for _ in range(3):
            await client.get_project(1)
        with pytest.raises(BudgetExceededError):
            await client.get_project(1)
    finally:
        httpx.AsyncClient = original

    # The fourth call was refused, not made.
    assert len(calls) == 3, f"expected exactly 3 reads to reach the network, got {len(calls)}"
    assert client.reads == 3


@pytest.mark.asyncio
async def test_a_failed_read_is_not_charged():
    """A request that returned nothing must not consume the allowance.

    Otherwise a flaky endpoint exhausts a workflow's budget and the stage fails
    for a reason that has nothing to do with how much it asked for.
    """
    from ai.clients.dotnet_client import DotNetClient, DotNetUnavailableError

    class _Failing(httpx.AsyncBaseTransport):
        async def handle_async_request(self, request):
            return httpx.Response(500, text="boom")

    client = DotNetClient(_settings(), workflow_run_id=7)
    original = httpx.AsyncClient
    httpx.AsyncClient = lambda *a, **k: original(*a, **{**k, "transport": _Failing()})
    try:
        with pytest.raises(DotNetUnavailableError):
            await client.get_project(1)
    finally:
        httpx.AsyncClient = original

    assert client.reads == 0


# ── validation rules: the model has no authority here ───────────────────

def _facts(**overrides):
    base = dict(
        stage="validate",
        project_id=1,
        project={"pricingType": "fixed", "fixedPrice": 1000, "expectedDays": 14,
                 "commissionPercentage": "10"},
        freelancer={"isActive": True, "isPhoneVerified": True, "averageRating": 4.5,
                    "reviewCount": 10},
        proposed_amount_usd=None,
        delivery_days=None,
    )
    base.update(overrides)
    return Facts(**base)


def test_clean_facts_pass():
    verdict = evaluate(_facts())
    assert verdict.valid, [i.message for i in verdict.errors]


def test_amount_below_bid_is_an_error():
    verdict = evaluate(_facts(
        proposed_amount_usd=100,
        proposal={"bidAmount": 500},
        commission_percentage=10,
    ))
    assert not verdict.valid
    assert any(i.rule == "amount_meets_bid" for i in verdict.errors)


def test_amount_above_ceiling_is_an_error():
    verdict = evaluate(_facts(proposed_amount_usd=5000, commission_percentage=10))
    assert any(i.rule == "within_project_budget" for i in verdict.errors)


def test_delivery_beyond_window_is_an_error():
    verdict = evaluate(_facts(delivery_days=60))
    assert any(i.rule == "delivery_within_window" for i in verdict.errors)


def test_inactive_freelancer_is_an_error_at_both_stages():
    for stage in ("validate", "talent_match"):
        verdict = evaluate(_facts(stage=stage, freelancer={
            "isActive": False, "isPhoneVerified": True, "averageRating": 5, "reviewCount": 3,
        }))
        assert any(i.rule == "freelancer_active" for i in verdict.errors), stage


def test_unverified_phone_blocks_validate_but_not_matching():
    """Onboarding depends on new freelancers not being locked out.

    An unverified number is a blocker for a contract but not for a shortlist --
    otherwise every new freelancer is excluded before they can verify, which is
    the entire point of letting them in.
    """
    unverified = {"isActive": True, "isPhoneVerified": False, "averageRating": 0, "reviewCount": 0}

    assert not evaluate(_facts(stage="validate", freelancer=unverified)).valid
    assert evaluate(_facts(stage="talent_match", freelancer=unverified)).valid


def test_low_rating_is_a_warning_not_a_block():
    verdict = evaluate(_facts(freelancer={
        "isActive": True, "isPhoneVerified": True, "averageRating": 3.0, "reviewCount": 8,
    }, min_rating_threshold=4.0))
    assert verdict.valid, "a low rating must not make a freelancer unhireable"
    assert any(i.rule == "rating_threshold" for i in verdict.warnings)


def test_no_reviews_is_not_a_warning():
    verdict = evaluate(_facts(freelancer={
        "isActive": True, "isPhoneVerified": True, "averageRating": 0, "reviewCount": 0,
    }))
    assert not any(i.rule == "rating_threshold" for i in verdict.warnings)


def test_unrecommended_freelancer_is_an_error():
    known = "11111111-1111-1111-1111-111111111111"
    stranger = "22222222-2222-2222-2222-222222222222"
    verdict = evaluate(_facts(
        stage="talent_match", shortlist=[known, stranger], recommended_ids={known},
    ))
    assert any(i.rule == "shortlist_was_recommended" for i in verdict.errors)


def test_cost_breakdown_is_exact():
    from decimal import Decimal
    verdict = evaluate(_facts(proposed_amount_usd=Decimal("1000"), commission_percentage=10))
    b = verdict.cost_breakdown
    assert b.subtotal_usd == Decimal("1000.00")
    assert b.commission_usd == Decimal("100.00")
    # The client pays subtotal plus commission; the freelancer receives subtotal.
    assert b.total_usd == Decimal("1100.00")


def test_a_fact_read_failure_is_reported_not_swallowed():
    """A gate that silently skips a rule is not a gate.

    At `validate` an unreadable project is blocking, because rules 1-3 and 7 all
    depend on it -- see `test_an_unreadable_freelancer_blocks_at_validate...`
    for the stage asymmetry.
    """
    verdict = evaluate(_facts(notes={"project_read_failed": "could not reach the API"}))
    assert any(i.rule == "project_read_failed" for i in verdict.issues)
    assert not verdict.valid


def test_human_summary_is_never_empty():
    for facts in (_facts(), _facts(delivery_days=90), _facts(proposed_amount_usd=9999)):
        assert evaluate(facts).human_summary


# ── screening bands ─────────────────────────────────────────────────────

def test_bands_agree_with_the_score():
    assert band_for(0.95) == "strong_match"
    assert band_for(0.75) == "strong_match"
    assert band_for(0.74) == "possible_match"
    assert band_for(0.50) == "possible_match"
    assert band_for(0.49) == "weak_match"
    assert band_for(0.0) == "weak_match"


# ── stage dispatch ──────────────────────────────────────────────────────

def test_unknown_stage_fails_loudly_rather_than_defaulting(client):
    r = client.post(
        "/agent/chat",
        json={"workflow_run_id": 1, "stage": "not_a_stage", "message": "x"},
        headers={"X-Internal-Api-Key": KEY},
    )
    assert r.status_code == 200
    body = r.json()
    # 200 with status=failed, not a 500: the reason is the useful part.
    assert body["status"] == "failed"
    assert "not_a_stage" in body["error"]


def test_malformed_body_is_a_422(client):
    r = client.post(
        "/agent/chat",
        json={"stage": "draft"},
        headers={"X-Internal-Api-Key": KEY},
    )
    assert r.status_code == 422


def test_empty_draft_input_asks_a_question_instead_of_inventing(client, monkeypatch):
    """Empty input must not cost a model call or produce a confident guess."""
    r = client.post(
        "/agent/chat",
        json={"workflow_run_id": 1, "stage": "draft", "context": {"partial_text": ""}},
        headers={"X-Internal-Api-Key": KEY},
    )
    body = r.json()
    assert body["status"] == "needs_clarification"
    assert body["payload"]["clarification_question"]
    assert body["payload"]["confidence"] == 0.0


# ── the .NET wire format ────────────────────────────────────────────────
#
# The .NET API serialises snake_case and sends money as strings. Both are
# deliberate on that side -- a float cannot represent money exactly -- and both
# are silent failures here if unhandled: a missing key yields None, a None score
# yields 0, and a shortlist comes back empty with no error.

def test_snake_case_keys_become_camel_case():
    from ai.clients.normalize import normalise
    out = normalise({"pricing_type": "Fixed", "expected_days": 30, "client_id": "x"})
    assert out == {"pricingType": "Fixed", "expectedDays": 30, "clientId": "x"}


def test_money_arrives_as_a_string_and_becomes_a_decimal():
    """Real payload captured from GET /internal/read/projects/1."""
    from ai.clients.normalize import normalise
    from decimal import Decimal
    out = normalise({
        "id": 1, "title": "Payment gateway integration",
        "pricing_type": "Fixed", "fixed_price": "2500.00",
        "min_price": None, "max_price": None, "expected_days": 30,
        "status": "Completed", "client_id": "50f5", "category_id": 1,
        "skills": ["CSharp", "React"],
    })
    assert out["fixedPrice"] == Decimal("2500.00")
    assert out["skills"] == ["CSharp", "React"]
    # Nulls dropped, so callers need not check for both "absent" and "null".
    assert "minPrice" not in out and "maxPrice" not in out


def test_ratings_arrive_as_strings_and_become_decimals():
    """Real payload captured from GET /internal/read/freelancers."""
    from ai.clients.normalize import normalise
    from decimal import Decimal
    out = normalise({
        "id": "b944584e", "full_name": "Seed Freelancer", "category_id": 1,
        "availability": True, "average_rating": "5.00", "review_count": 1,
        "packages": [{"price_usd": "650.00", "delivery_days": 12, "package_type": "pro"}],
    })
    assert out["averageRating"] == Decimal("5.00")
    assert out["reviewCount"] == 1
    assert out["packages"][0]["priceUsd"] == Decimal("650.00")
    assert out["packages"][0]["deliveryDays"] == 12


def test_a_prose_field_that_looks_numeric_is_left_alone():
    """A description of "1000" is prose, and must not become a Decimal."""
    from ai.clients.normalize import normalise
    out = normalise({"description": "1000", "title": "42"})
    assert out["description"] == "1000"
    assert out["title"] == "42"


def test_the_price_band_money_arrives_as_decimals():
    """Real payload captured from GET /internal/read/platform/price-bands.

    `DECIMAL_FIELDS` is an explicit list rather than inferred from "looks
    numeric", which is the right call -- inferring would mangle free text. But it
    means a new money field is invisible until somebody names it, and the symptom
    is an agent holding the string "4000.00" and unable to compare it against a
    band it just read. That is exactly what happened to `min_price_usd` and
    `max_price_usd`.
    """
    from decimal import Decimal

    from ai.clients.normalize import normalise

    out = normalise([{
        "deliverable": "AI chatbot or virtual assistant",
        "level": "intermediate",
        "min_price_usd": "2500.00",
        "max_price_usd": "9000.00",
        "typical_days_min": None,
        "typical_days_max": None,
        "category_id": 3,
        "category_name": "AI & Automation",
        "source": "Upwork public category/package pages",
    }])

    band = out[0]
    assert band["minPriceUsd"] == Decimal("2500.00")
    assert band["maxPriceUsd"] == Decimal("9000.00")
    # And they must be orderable against each other, which is the whole point of
    # handing an agent a band rather than prose.
    assert band["minPriceUsd"] < band["maxPriceUsd"]
    # The provenance travels with the numbers.
    assert band["source"].startswith("Upwork")
    # Nulls dropped, consistently with every other payload.
    assert "typicalDaysMin" not in band


def test_matching_agent_can_actually_read_a_real_freelancer_payload():
    """The failure this prevents: an empty shortlist with no error anywhere.

    A real response, through the normaliser, into the scoring function. If the
    key names drift, this fails -- rather than the agent quietly scoring
    everything zero and returning no matches.
    """
    from ai.clients.normalize import normalise
    from ai.agents.matching_agent import MatchingAgent
    from ai.agents.planning_agent import HardFilters, MatchWeights
    from ai.clients.dotnet_client import DotNetClient
    from ai.config import Settings

    raw = {
        "id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
        "full_name": "Seed Freelancer", "availability": True,
        "average_rating": "5.00", "review_count": 1,
        "skills": ["React"],
        "packages": [
            {"price_usd": "650.00", "delivery_days": 12, "package_type": "pro"},
            {"price_usd": "280.00", "delivery_days": 3, "package_type": "starter"},
        ],
    }
    candidate = normalise(raw)

    agent = MatchingAgent(
        DotNetClient(Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"))
    )
    score = agent._score_candidate(
        candidate,
        project={"pricingType": "Fixed", "fixedPrice": "2500.00", "skills": ["React"]},
        required_skills=["React"],
        ceiling=2500.0,
        weights=MatchWeights(),
    )

    # A React freelancer on a React project with a 4.9 average must score well.
    # Zero here would mean the keys did not line up.
    assert score.final_score > 50, f"expected a real score, got {score.final_score}"
    assert score.strengths == ["React"]
    assert not score.gaps


def test_screening_can_actually_read_a_real_project_payload():
    from ai.clients.normalize import normalise
    from ai.agents.screening_agent import ScreeningAgent, ProposalIn
    from ai.clients.dotnet_client import DotNetClient
    from ai.config import Settings

    project = normalise({
        "id": 1, "title": "Payment gateway integration",
        "pricing_type": "Fixed", "fixed_price": "2500.00", "expected_days": 30,
        "skills": ["CSharp"],
    })
    agent = ScreeningAgent(
        DotNetClient(Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"))
    )
    fit, strengths, concerns = agent._fit(
        ProposalIn(proposal_id=1, freelancer_id="b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
                   bid_amount_usd="2000.00", delivery_days=20, cover_letter="I have built these before, twice."),
        profile=normalise({"skills": ["CSharp"], "average_rating": "5.00", "review_count": 3}),
        required=project["skills"],
        ceiling=2500.0,
        days_allowed=30,
    )
    assert fit > 0.7, f"expected a strong fit, got {fit}"
    assert concerns == [] or not any("exceeds" in c for c in concerns)


# ── a failing tool must be visible ──────────────────────────────────────
#
# The bug this pins: ToolBox.call used to log a tool failure and return None.
# The validation agent wrapped its reads in try/except expecting to record a
# note, and because nothing escaped, the except was dead code -- so a run whose
# every read failed reported `valid: true` having checked nothing. A gate that
# claims success without running is worse than a missing gate, because it is
# trusted.

class _FailingClient:
    reads = 0
    read_budget = 20

    async def get_project(self, project_id):
        raise RuntimeError("the API is down")

    async def get_freelancer(self, freelancer_id):
        raise RuntimeError("the API is down")

    async def append_tool_call(self, *a, **k):
        return None

    async def append_step(self, *a, **k):
        return None

    async def append_validation(self, *a, **k):
        return None

    async def append_screening(self, *a, **k):
        return None

    async def append_recommendation(self, *a, **k):
        return None

    async def complete_workflow(self, *a, **k):
        return None


@pytest.mark.asyncio
async def test_a_failed_tool_raises_by_default():
    box = ToolBox("validation", 1, _FailingClient())
    with pytest.raises(RuntimeError):
        await box.call("get_project", project_id=1)


@pytest.mark.asyncio
async def test_a_failed_tool_can_opt_out_where_that_is_safe():
    box = ToolBox("draft", 1, _FailingClient())
    # Best-effort is an explicit choice by the caller, not a default.
    assert await box.call("get_platform_commission", best_effort=True) is None


@pytest.mark.asyncio
async def test_a_failed_tool_is_still_audited():
    box = ToolBox("validation", 1, _FailingClient())
    with pytest.raises(RuntimeError):
        await box.call("get_project", project_id=1)
    assert len(box.calls) == 1
    assert box.calls[0].success is False
    assert "the API is down" in box.calls[0].error


@pytest.mark.asyncio
async def test_validation_reports_a_read_failure_instead_of_passing():
    """The end-to-end version of the same bug.

    A validation run whose every read fails must not come back valid. It must
    come back with the failure surfaced.
    """
    from ai.agents.validation_agent import ValidationAgent, ValidationInput
    from ai.clients.dotnet_client import DotNetClient
    from ai.config import Settings

    agent = ValidationAgent(_FailingClient())
    out = await agent.run(ValidationInput(
        workflow_run_id=1,
        project_id=1,
        # Both reads requested, so both failures should be reported. A read that
        # was never attempted cannot fail, so the test has to ask for it.
        freelancer_id=uuid.UUID("b944584e-2cd9-43f9-b5c7-a25e2c865ff4"),
    ))

    rules = {i["rule"] for i in out.issues}
    assert "project_read_failed" in rules, out.issues
    assert "freelancer_read_failed" in rules, out.issues
    # At the money gate, an unreadable subject is blocking, not advisory.
    assert not out.valid, "a contract must not pass when the freelancer could not be read"
    # The summary must mention that something could not be checked.
    assert "could not" in out.human_summary.lower()
    # And it is never presented as a clean pass.
    assert out.requires_human_approval


def test_a_candidate_with_no_is_active_field_is_not_silently_excluded():
    """The freelancer list projection has no is_active column.

    Absent is not false. Treating a missing field as inactive would empty every
    shortlist the moment the projection changed, with no error anywhere.
    """
    from ai.agents.matching_agent import MatchingAgent
    from ai.agents.planning_agent import HardFilters

    agent = MatchingAgent.__new__(MatchingAgent)
    assert not MatchingAgent._excluded_by_filters({"availability": True}, HardFilters())
    assert MatchingAgent._excluded_by_filters({"availability": False}, HardFilters())


def test_an_unreadable_freelancer_blocks_at_validate_but_only_warns_at_matching():
    """The asymmetry, stated.

    The same read failure is fatal when money moves and advisory when a list is
    being assembled -- because at matching a hiccup should not empty the
    shortlist, and at validate it must not let a contract through unchecked.
    """
    validate_verdict = evaluate(_facts(stage="validate", notes={
        "freelancer_read_failed": "404",
    }))
    match_verdict = evaluate(_facts(stage="talent_match", notes={
        "freelancer_read_failed": "404",
    }))

    assert not validate_verdict.valid
    assert match_verdict.valid
    assert any(i.rule == "freelancer_read_failed" for i in validate_verdict.errors)
    assert any(i.rule == "freelancer_read_failed" for i in match_verdict.warnings)


def test_an_unreadable_threshold_is_always_only_a_warning():
    """Not being able to read the admin's rating threshold is an inconvenience."""
    for stage in ("validate", "talent_match"):
        verdict = evaluate(_facts(stage=stage, notes={
            "agent_thresholds_read_failed": "timeout",
        }))
        assert verdict.valid, stage
        assert any(i.rule == "agent_thresholds_read_failed" for i in verdict.warnings)


def test_a_warning_always_asks_for_human_eyes():
    """The bug: a run that could not check anything reported no approval needed."""
    verdict = evaluate(_facts(notes={"project_read_failed": "down"}))
    assert verdict.requires_human_approval

# ── fixtures ────────────────────────────────────────────────────────────
#
# The fixture provider is what makes the suite runnable with no keys and no
# network. Two properties matter and both are tested here: a recorded prompt
# replays, and an unrecorded one fails loudly rather than inventing an answer.

def test_a_recorded_fixture_replays():
    from ai.llm.providers import FixtureProvider
    from ai.scripts.record_fixture import stamped

    provider = FixtureProvider()
    prompts = {}
    for agent in ("draft", "planning", "matching", "screening", "validation"):
        path = provider._dir / f"{agent}.jsonl"
        if not path.exists():
            continue
        import json
        for line in path.read_text(encoding="utf-8").splitlines():
            if line.strip():
                import json as _j
                prompts.setdefault(agent, []).append(_j.loads(line)["prompt_sha256"])
        assert prompts[agent], f"{agent}.jsonl is empty"

    assert any(prompts.values()), "no fixtures recorded at all"


@pytest.mark.asyncio
async def test_an_unrecorded_prompt_fails_instead_of_guessing():
    from ai.llm.providers import FixtureProvider
    from ai.llm.base import FixtureMissingError

    provider = FixtureProvider()
    with pytest.raises(FixtureMissingError) as exc:
        await provider.complete("# agent: draft\nsomething nobody ever recorded")

    # The message has to say what to do about it.
    assert "fixture" in str(exc.value).lower()
    assert "re-record" in str(exc.value).lower() or "prompt changed" in str(exc.value).lower()


@pytest.mark.asyncio
async def test_a_draft_grounded_in_a_real_fixture_produces_real_ids():
    """A recorded fixture, replayed and put through grounding.

    Uses the canonical inputs from ``ai.tests.examples`` -- the same module the
    recorder uses. When the two sides each built their own prompt they drifted and
    this test failed with "no recorded response", which is the fixture provider
    correctly reporting a changed prompt. One definition removes that.
    """
    from ai.agents.draft_agent import DraftAgent, DraftInput, DraftOutput
    from ai.clients.dotnet_client import DotNetClient
    from ai.config import Settings
    from ai.llm.chain import call_llm, reset_for_tests
    from ai.tests.examples import CATEGORIES, COMMISSION, DRAFT_PARTIAL_TEXT

    client = DotNetClient(Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"))
    async def noop(*a, **k): return None
    for n in ("append_step", "append_tool_call", "complete_workflow", "get_commission"):
        setattr(client, n, noop)

    agent = DraftAgent(client, categories=CATEGORIES)
    prompt = agent._compose_prompt(
        DraftInput(workflow_run_id=1, partial_text=DRAFT_PARTIAL_TEXT), CATEGORIES, COMMISSION
    )

    settings = Settings(
        internal_api_key=KEY, dotnet_base_url="https://x.invalid",
        llm_provider_chain="fixture", llm_cache_enabled=False, llm_allow_fixture_fallback=False,
    )
    reset_for_tests()
    response = await call_llm(prompt, agent="draft", workflow_run_id=1, settings=settings)

    draft = await agent._parse(response.text, DraftOutput, workflow_run_id=1)
    grounded = agent._ground(draft, CATEGORIES, {c["id"] for c in CATEGORIES}, {})

    # Whatever the recorded model said, the output is now in-bounds.
    valid_ids = {c["id"] for c in CATEGORIES}
    assert grounded.category_id is None or grounded.category_id in valid_ids
    for skill_id in grounded.skill_ids:
        assert skill_id in valid_ids, f"grounding let an ungrounded skill id through: {skill_id}"
    assert 1 <= grounded.delivery_days <= 365
    assert 0.0 <= grounded.confidence <= 1.0
    if grounded.pricing_type == "fixed":
        assert grounded.max_budget_usd is None
    else:
        if grounded.max_budget_usd is not None and grounded.min_budget_usd is not None:
            assert grounded.max_budget_usd >= grounded.min_budget_usd


@pytest.mark.asyncio
async def test_grounding_drops_a_hallucinated_category_and_skill():
    """The specific failure the grounding exists for."""
    from ai.agents.draft_agent import DraftAgent, DraftOutput

    agent = DraftAgent.__new__(DraftAgent)   # no client needed for pure grounding

    # Nested, because that is what the server sends. With a bare
    # `{"id": 1, "name": "Web Development"}` row this test asserted
    # `skill_ids == [1]` for a skill named "React" -- i.e. it asserted that a
    # category id is a valid skill id, and passed for exactly that reason. The
    # real catalogue holds 654 skills; grounding against categories proved
    # nothing about any of them.
    categories = [
        {
            "id": 1,
            "name": "Web Development",
            "skills": [{"id": 1, "name": "C#"}, {"id": 2, "name": "React"}],
        }
    ]

    draft = DraftOutput(
        title="A portal", description="Long enough to be a real description.",
        requirements="", expected_deliverables="",
        category_id=9999,                 # does not exist
        category_name="Astrophysics",
        skill_ids=[9999, 1],               # 9999 does not exist
        skill_names=["Quantum Chromodynamics", "React"],
    )

    grounded = agent._ground(draft, categories, {1}, {"web development": categories[0]})

    assert grounded.category_id is None
    assert any("9999" in n for n in grounded.notes), "the drop was not explained"
    # "React" survives, resolved by name to its real id -- 2, not the 1 the model
    # supplied. Asserting the resolved name alongside the id is what stops this
    # from drifting back into "some id in the pool is good enough".
    assert grounded.skill_ids == [2]
    assert grounded.skill_names == ["React"]


# ── provider configuration ──────────────────────────────────────────────

def test_every_configured_provider_is_reported():
    from ai.llm.chain import describe_providers
    rows = describe_providers()
    assert rows
    for row in rows:
        assert set(row) == {"provider", "keys_configured", "model", "model_is_default", "usable"}
        assert isinstance(row["keys_configured"], int)


def test_health_does_not_leak_keys():
    from fastapi.testclient import TestClient
    from ai.main import app
    body = TestClient(app).get("/health").text
    for leak in ("AQ.Ab8", "csk-", "gsk_", "nvapi-", "mstrl_", "sk-or-v1-", "cohere_"):
        assert leak not in body, f"/health leaked something shaped like a {leak!r} key"


def test_startup_log_does_not_print_key_material():
    """The startup summary is read by humans, so it must be safe to paste."""
    import io
    import logging
    from ai.llm.chain import log_configuration
    from ai.config import get_settings

    stream = io.StringIO()
    handler = logging.StreamHandler(stream)
    root = logging.getLogger("ai")
    root.addHandler(handler)
    root.setLevel(logging.INFO)
    try:
        log_configuration(get_settings())
    finally:
        root.removeHandler(handler)

    output = stream.getvalue()
    for leak in ("AQ.Ab8", "csk-", "gsk_", "nvapi-", "mstrl_", "sk-or-v1-", "cohere_"):
        assert leak not in output, f"the startup log printed something shaped like a key"

# ── the .NET write contract ───────────────────────────────────────────

@pytest.mark.asyncio
async def test_a_recommendation_sends_the_evidence_not_just_the_score():
    """`strengths` and `gaps` must reach .NET.

    The matching agent has always computed them. They were dropped in two
    places -- here, where the request body did not name them, and in the C#
    request DTO, which had no field for them. Nothing failed; the client's
    shortlist and the freelancer's invitation just quietly showed a score and a
    sentence with nothing behind them.

    Both halves had to be wrong for the loss to be invisible, which is exactly
    why it survived: a test on either side alone would have passed.
    """
    from ai.clients.dotnet_client import DotNetClient
    from ai.config import Settings

    captured: dict = {}

    client = DotNetClient(
        Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"),
        workflow_run_id=3,
    )

    async def capture(method, path, payload, **_kwargs):
        captured["payload"] = payload
        return None

    client._write = capture

    await client.append_recommendation(
        3,
        {
            "project_id": 1,
            "freelancer_id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
            "score": 0.875,
            "factors": {"skill_coverage": 0.9, "budget_fit": 0.85},
            "reason": "Strong React history.",
            "strengths": ["React", "Stripe"],
            "gaps": ["C#"],
            "portfolio_samples": [],
        },
    )

    payload = captured["payload"]
    assert payload["strengths"] == ["React", "Stripe"]
    assert payload["gaps"] == ["C#"]
    # The numeric factors stay a JSON *string* -- the column is text, and the
    # server parses it. Sending a dict here would bind to nothing and store
    # "System.Collections.Generic..." or fail deserialisation.
    assert isinstance(payload["factors_json"], str)
    assert "skill_coverage" in payload["factors_json"]


@pytest.mark.asyncio
async def test_a_recommendation_with_no_evidence_sends_empty_lists_not_none():
    """Absent evidence is `[]`, not `None`.

    `None` would serialise to JSON null and bind to a null list server-side,
    which is the same outcome -- but only by accident. An explicit empty list
    says "the model had nothing to add", which is a different statement from
    "the field was never populated" and the two mean different things once
    someone is debugging a shortlist that rendered no detail.
    """
    from ai.clients.dotnet_client import DotNetClient
    from ai.config import Settings

    captured: dict = {}

    client = DotNetClient(
        Settings(internal_api_key=KEY, dotnet_base_url="https://x.invalid"),
        workflow_run_id=3,
    )

    async def capture(method, path, payload, **_kwargs):
        captured["payload"] = payload
        return None

    client._write = capture

    await client.append_recommendation(
        3,
        {"project_id": 1, "freelancer_id": "x", "score": 0.5, "factors": {}},
    )

    assert captured["payload"]["strengths"] == []
    assert captured["payload"]["gaps"] == []


# ── a caller-supplied plan ─────────────────────────────────────────────
#
# The client can move the weights and re-run. For that control to be honest the
# supplied plan has to win over the Planning Agent, and a bad one has to fail
# rather than fall back. A silent fallback is the failure mode worth guarding:
# the client would see a plausible shortlist scored on numbers it believes it
# chose, and nothing anywhere would say otherwise.

from ai.graph.dispatcher import AgentChatRequest, PlanRejected, StageDispatcher


def _dispatcher(plan):
    return StageDispatcher(
        AgentChatRequest(
            workflow_run_id=1,
            stage="talent_match",
            project_id=1,
            context={},
            plan=plan,
        )
    )


def _valid_plan(**overrides):
    plan = {
        "weights": {
            "skill_coverage": 0.5,
            "portfolio_relevance": 0.2,
            "past_success_rate": 0.1,
            "availability_and_speed": 0.1,
            "budget_fit": 0.1,
        },
        "hard_filters": {
            "require_active": True,
            "require_available": True,
            "min_rating": 0.0,
            "exclude_already_proposed": True,
        },
        "shortlist_size": 10,
    }
    plan.update(overrides)
    return plan


def test_no_plan_means_plan_it_normally():
    # None, not an error. The common path is a first run with no client input.
    assert _dispatcher(None)._supplied_plan() is None
    assert _dispatcher({})._supplied_plan() is None


def test_a_valid_supplied_plan_is_accepted_verbatim():
    plan = _dispatcher(_valid_plan())._supplied_plan()

    assert plan is not None
    assert plan.weights.skill_coverage == 0.5
    assert plan.shortlist_size == 10
    # The source is stated, so the client can distinguish "the planner chose
    # this" from "you chose this".
    assert "client" in plan.reasoning_summary.lower()


def test_weights_that_do_not_sum_to_one_are_rejected():
    # They are relative. A total of 1.3 over-scores every candidate and the
    # shortlist still looks plausible.
    bad = _valid_plan()
    bad["weights"]["skill_coverage"] = 0.9

    with pytest.raises(PlanRejected, match="sum to"):
        _dispatcher(bad)._supplied_plan()


def test_a_negative_weight_is_rejected_even_though_it_sums_correctly():
    # {-1, 0, 0, 0, 2} sums to 1 and inverts the score. The sum check alone
    # would pass it.
    bad = _valid_plan()
    bad["weights"] = {
        "skill_coverage": -1.0,
        "portfolio_relevance": 0.0,
        "past_success_rate": 0.0,
        "availability_and_speed": 0.0,
        "budget_fit": 2.0,
    }

    with pytest.raises(PlanRejected, match="between 0 and 1"):
        _dispatcher(bad)._supplied_plan()


def test_a_weight_above_one_is_rejected():
    bad = _valid_plan()
    bad["weights"] = {
        "skill_coverage": 1.5,
        "portfolio_relevance": -0.1,
        "past_success_rate": 0.0,
        "availability_and_speed": 0.0,
        "budget_fit": -0.4,
    }

    with pytest.raises(PlanRejected, match="between 0 and 1"):
        _dispatcher(bad)._supplied_plan()


@pytest.mark.parametrize("size", [0, 4, 16, 100])
def test_a_shortlist_size_outside_the_supported_range_is_rejected(size):
    with pytest.raises(PlanRejected, match="shortlist_size"):
        _dispatcher(_valid_plan(shortlist_size=size))._supplied_plan()


def test_an_unknown_field_is_rejected_rather_than_ignored():
    # Pydantic's default is to drop unrecognised keys, so a client sending
    # `skill_coverge` (one missing "a") would get the *default* weights, a
    # successful run, and a shortlist scored on numbers it never chose, with
    # nothing reporting a problem. Rejected instead.
    with pytest.raises(PlanRejected, match="unknown plan field"):
        _dispatcher(_valid_plan(weights_nonsense={"skill_coverge": 1.0}))._supplied_plan()


def test_a_typo_in_a_weight_name_is_rejected_and_the_name_is_echoed():
    plan = _valid_plan()
    plan["weights"] = {"skill_coverge": 0.9}

    with pytest.raises(PlanRejected, match="skill_coverge"):
        _dispatcher(plan)._supplied_plan()


def test_a_clarification_question_on_a_supplied_plan_is_rejected():
    # A contradiction: the client is answering a question, not asking one.
    with pytest.raises(PlanRejected, match="clarification_question"):
        _dispatcher(_valid_plan(clarification_question="What budget?"))._supplied_plan()


def test_a_malformed_plan_is_rejected_rather_than_ignored():
    with pytest.raises(PlanRejected, match="not valid"):
        _dispatcher({"weights": "not a mapping"})._supplied_plan()


@pytest.mark.asyncio
async def test_a_rejected_plan_becomes_a_failure_the_client_can_read():
    # Not a 500 and not a stack trace: the caller needs to know its own input
    # was wrong, or it will go looking at the service.
    dispatcher = _dispatcher(_valid_plan())
    dispatcher.request.plan["shortlist_size"] = 99

    response = await dispatcher.run()

    assert response.status == "failed"
    assert "shortlist_size" in (response.error or "")


# ── production guards ──────────────────────────────────────────────────
#
# Two settings are refused outright when APP_ENV=production. Both are
# "insecure but convenient" switches that are correct in development and
# indefensible in production, and both fail at startup rather than at first use.
#
# Tested because a guard nobody tests is a guard that gets broken: the
# alternative is a value that is only refused by remembering that it was ever
# meant to be.

def _production_settings(**overrides):
    """A production `Settings` with every other guard satisfied.

    `llm_provider_chain` is set to a real provider on purpose. The guards run in
    declaration order and the fixture guard is first, so a helper that leaves the
    chain at its `"fixture"` default fails on *that* whatever else it is testing
    -- which is how a test about TLS ends up asserting about the provider chain.
    """
    from ai.config import Settings

    base = {
        "internal_api_key": KEY,
        "app_env": "production",
        "llm_provider_chain": "groq",
    }
    base.update(overrides)
    return Settings(**base)


def test_production_refuses_to_disable_tls_verification():
    # With verification off, anything between this service and .NET can read and
    # alter every response -- including the shared internal key, which travels in
    # a header on every call.
    with pytest.raises(ValidationError, match="DOTNET_VERIFY_TLS"):
        _production_settings(dotnet_verify_tls=False)


def test_production_allows_tls_verification():
    assert _production_settings(dotnet_verify_tls=True).dotnet_verify_tls is True


def test_development_may_disable_tls_verification():
    # The local .NET API uses a development certificate, so a local run fails
    # every internal read with CERTIFICATE_VERIFY_FAILED without this. The point
    # of the guard is that it is *named*, not that it is impossible.
    settings = _production_settings(app_env="development", dotnet_verify_tls=False)
    assert settings.dotnet_verify_tls is False


def test_production_refuses_the_fixture_provider():
    # Fixtures are recorded responses, not a model. A production deployment on
    # them looks healthy -- runs complete, statuses advance -- while making no
    # decisions at all.
    with pytest.raises(ValidationError, match="fixture"):
        _production_settings(llm_provider_chain="fixture")


def test_production_refuses_a_chain_containing_fixture():
    # Not an exact match. `groq,fixture` would satisfy an equality check and put
    # a recorded fixture second in line for every request that groq rate-limits.
    with pytest.raises(ValidationError, match="fixture"):
        _production_settings(llm_provider_chain="groq,fixture")


def test_production_refuses_an_empty_provider_chain():
    # Nothing to call. Every run would fall through to a fixture if one matched,
    # which is the previous case by another route.
    with pytest.raises(ValidationError):
        _production_settings(llm_provider_chain="  ")


def test_production_requires_an_internal_api_key():
    # Fail closed. Without this the service starts and every internal call is
    # refused one at a time, which is a slower and noisier way to learn it is
    # misconfigured.
    with pytest.raises(ValidationError):
        _production_settings(internal_api_key="")
