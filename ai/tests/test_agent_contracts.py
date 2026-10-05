"""The three agents whose contracts are least about prose.

Draft, Validation and Matching have the strongest coverage because their
guarantees are about *grounding* -- ids that reach the database, formulas that
produce scores -- and those are the assertions worth writing. The agents tested
here have guarantees that are just as real but sit further from the code, so
they were the ones left thin.

What is asserted is behaviour, not implementation: that the gate refuses, that a
band matches its score, that a low-confidence plan asks rather than guesses.
"""

from __future__ import annotations

import pytest

from ai.agents.outreach_agent import OutreachAgent, OutreachInput
from ai.agents.planning_agent import (
    HardFilters,
    MatchWeights,
    PlanStep,
    PlanningOutput,
)
from ai.agents.screening_agent import (
    MissingProjectError,
    ProposalScreening,
    ScreeningAgent,
    ScreeningInput,
    ScreeningOutput,
    band_for,
)
from ai.config import Settings
from ai.clients.dotnet_client import DotNetClient

KEY = "test-key-abc123"


def _client() -> DotNetClient:
    """A client whose reads come from the shared examples and whose writes go
    nowhere. The agents only need the prompts and the tool responses here, and
    pointing at a live API would make the suite depend on the server being up."""
    from ai.tests.examples import COMMISSION, PROJECT, THRESHOLDS

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

    async def get_project(_):
        return PROJECT

    async def commission():
        return COMMISSION

    async def thresholds():
        return THRESHOLDS

    client.get_project = get_project
    client.get_commission = commission
    client.get_agent_thresholds = thresholds
    return client


# ── Outreach ────────────────────────────────────────────────────────────
#
# This is the only agent that acts on the world. Everything else produces advice
# a human can ignore; this one sends invitations from a client's account. So its
# tests are about what it refuses to do.

FREELANCER_A = "b944584e-2cd9-43f9-b5c7-a25e2c865ff4"
FREELANCER_B = "c2b7f0e1-1111-2222-3333-444455556666"



@pytest.mark.asyncio
async def test_outreach_refuses_an_empty_approval():
    # Nothing to invite is not the same as an invitation to nobody: one is a
    # mistake in the approval and the other is a decision, and the run log should
    # say which happened.
    agent = _outreach()

    result = await agent.run(
        OutreachInput(workflow_run_id=1, project_id=1, approved_freelancer_ids=[])
    )

    assert result.invitation_ids == []
    assert result.errors
    assert "no freelancer ids" in result.errors[0].lower()
    assert "nothing was sent" in result.human_summary.lower()


@pytest.mark.asyncio
async def test_outreach_refuses_an_oversized_approval():
    # The limit exists so one approval cannot turn into an unbounded mailing.
    # The ids are real UUIDs because the input is typed as such, which is itself
    # worth knowing: a malformed id is refused by the schema before the agent
    # sees it, rather than reaching .NET as a string.
    import uuid

    agent = _outreach()

    result = await agent.run(
        OutreachInput(
            workflow_run_id=1,
            project_id=1,
            approved_freelancer_ids=[uuid.uuid4() for _ in range(21)],
        )
    )

    assert result.invitation_ids == []
    assert result.errors
    assert "21" in result.errors[0]
    assert "nothing was sent" in result.human_summary.lower()


def test_outreach_refuses_a_freelancer_id_that_is_not_a_uuid():
    # The schema is the first gate. An approval carrying a malformed id is a
    # client bug, and it should not become a 500 or a silent drop.
    import uuid

    with pytest.raises(Exception) as exc:
        OutreachInput(
            workflow_run_id=1,
            project_id=1,
            approved_freelancer_ids=["not-a-uuid", str(uuid.uuid4())],
        )

    assert "uuid" in str(exc.value).lower()


def _outreach(reply=None) -> OutreachAgent:
    """An outreach agent whose only outbound call is replaced.

    Patched at `DotNetClient._http`, which is the single point every request goes
    through. Two lower seams were tried first and both are traps:

    - patching a `ToolBox` instance does nothing, because `BaseAgent._toolbox`
      builds a fresh one on every call -- the patch is silently ignored and the
      test makes a real DNS lookup;
    - patching `_write` does nothing for this particular tool, because
      `send_invitations` posts through `_http` directly rather than the shared
      write helper.

    Either mistake produces a test that still runs, still fails or passes, and is
    asserting on a network error rather than on the agent. Patching the transport
    is the only seam that cannot be missed.
    """
    agent = OutreachAgent(_client())

    if reply is None:
        return agent

    sent: list[dict] = []

    class _Response:
        status_code = 200

        def __init__(self, payload):
            self._payload = payload

        def json(self):
            return self._payload

    class _FakeHttp:
        async def __aenter__(self):
            return self

        async def __aexit__(self, *exc):
            return False

        async def post(self, url, headers=None, json=None):
            sent.append({"url": url, "body": json or {}})
            if isinstance(reply, Exception):
                raise reply
            return _Response(reply)

    agent._client._http = lambda **kwargs: _FakeHttp()
    agent.sent = sent
    return agent


@pytest.mark.asyncio
async def test_outreach_deduplicates_before_calling():
    # Deduped here rather than left for .NET to reject, so the tool log shows the
    # request that was actually intended.
    agent = _outreach({"invitation_ids": [1, 2], "skipped": []})

    result = await agent.run(
        OutreachInput(
            workflow_run_id=1,
            project_id=7,
            approved_freelancer_ids=[FREELANCER_A, FREELANCER_B, FREELANCER_A],
        )
    )

    body = agent.sent[0]["body"]
    # Two ids sent, not three: the repeat was collapsed before the request.
    assert len(body["freelancer_ids"]) == 2
    assert set(body["freelancer_ids"]) == {FREELANCER_A, FREELANCER_B}
    assert result.invitation_ids == [1, 2]


@pytest.mark.asyncio
async def test_outreach_reports_a_failure_rather_than_raising():
    # A failed outreach should leave a run a person can see and retry, not a
    # stack trace in a background job nobody reads.
    agent = _outreach(RuntimeError("the .NET API is unreachable"))

    result = await agent.run(
        OutreachInput(
            workflow_run_id=1, project_id=1, approved_freelancer_ids=[FREELANCER_A]
        )
    )

    assert result.errors
    assert result.invitation_ids == []
    assert "nothing was contacted" in result.human_summary.lower()


@pytest.mark.asyncio
async def test_outreach_keeps_skipping_some_and_inviting_the_rest():
    # Ten invited and two skipped is a good outcome. Reporting it as a failure
    # would teach clients to distrust the summary.
    agent = _outreach({
        "invitation_ids": [1, 2, 3],
        "skipped": [{"freelancer_id": FREELANCER_B, "reason": "already applied"}],
    })

    result = await agent.run(
        OutreachInput(
            workflow_run_id=1,
            project_id=1,
            approved_freelancer_ids=[FREELANCER_A, FREELANCER_B],
        )
    )

    assert result.invitation_ids == [1, 2, 3]
    assert result.skipped
    # The reason is kept, because "we did not contact two of your five" is a
    # question a client will ask and needs an answer.
    assert "already applied" in result.human_summary


@pytest.mark.asyncio
async def test_outreach_survives_a_malformed_response():
    # A skipped entry that is not a dict, and an invitation id that is not a
    # number. Neither may raise.
    #
    # The id case was a real bug: the parsing sat outside the `try` around the
    # call, so `int("not a number")` raised out of `run()` and the whole result
    # was discarded -- including invitations that had genuinely been created. The
    # client's freelancers were contacted and the run said nothing happened.
    agent = _outreach({
        "invitation_ids": [11, "not a number", 13],
        "skipped": ["not a dict"],
    })

    result = await agent.run(
        OutreachInput(
            workflow_run_id=1, project_id=1, approved_freelancer_ids=[FREELANCER_A]
        )
    )

    # The two that parsed survive.
    assert result.invitation_ids == [11, 13]
    # And the one that did not is reported, so it is visible rather than lost.
    assert any("not a number" in e for e in result.errors)
    assert result.human_summary


# ── Planning ────────────────────────────────────────────────────────────


def test_weights_report_their_own_total():
    # Used to validate a supplied plan, and the sum is the check that matters.
    weights = MatchWeights(
        skill_coverage=0.5,
        portfolio_relevance=0.2,
        past_success_rate=0.1,
        availability_and_speed=0.1,
        budget_fit=0.1,
    )
    assert weights.total() == pytest.approx(1.0)


def test_planning_output_defaults_are_a_usable_plan():
    # The defaults are what a client sees before touching anything, so they must
    # survive the agent's own validation.
    plan = PlanningOutput()
    assert plan.weights.total() == pytest.approx(1.0)
    assert 5 <= plan.shortlist_size <= 15
    assert 0.0 <= plan.confidence <= 1.0


def test_planning_steps_carry_a_unique_order():
    # Two steps with the same order cannot be sequenced, and the graph is
    # expressed as an ordered list.
    steps = [
        PlanStep(order=1, agent_name="matching", rationale="a"),
        PlanStep(order=2, agent_name="screening", rationale="b"),
    ]
    orders = [s.order for s in steps]
    assert len(orders) == len(set(orders))


def test_hard_filters_default_to_the_safe_choices():
    # The defaults here are conservative on purpose. A plan that relaxes them
    # should have to say so.
    filters = HardFilters()
    assert filters.require_active is True
    assert filters.require_available is True
    assert filters.exclude_already_proposed is True


# ── Screening ───────────────────────────────────────────────────────────


def test_the_band_matches_the_score():
    # The band is derived, not chosen. A model that returns "strong_match" for a
    # score of 0.4 would be asserting a judgement the number contradicts.
    assert band_for(1.0) == "strong_match"
    assert band_for(0.75) == "strong_match"
    assert band_for(0.74) == "possible_match"
    assert band_for(0.50) == "possible_match"
    assert band_for(0.49) == "weak_match"
    assert band_for(0.0) == "weak_match"


def test_a_proposal_missing_from_the_output_is_reported_not_dropped():
    # A silently dropped proposal is one the client stops considering, and a
    # shortlist quietly missing people is the exact failure worth naming.
    output = ScreeningOutput(screenings=[ProposalScreening(proposal_id=1, fit_score=0.8)])
    supplied = [1, 2, 3]

    returned = {s.proposal_id for s in output.screenings}
    missing = [p for p in supplied if p not in returned]

    assert missing == [2, 3]


@pytest.mark.asyncio
async def test_screening_of_nothing_is_not_a_crash():
    agent = ScreeningAgent(_client(), categories=[])

    result = await agent.run(
        ScreeningInput(workflow_run_id=1, project_id=1, project={}, proposals=[])
    )

    assert result.screenings == []
    assert result.missing_proposal_ids == []


@pytest.mark.asyncio
async def test_screening_without_a_project_is_a_caller_bug_not_a_verdict():
    # The agent used to re-read the project with `get_project`, which its
    # permissions forbid, so every screening run raised `ToolNotPermittedError`
    # and the stage could not succeed at all. The dispatcher now supplies it.
    #
    # A missing project must be reported as a caller bug rather than producing an
    # empty screening -- an empty screening reads as "these proposals scored
    # badly", and a client acting on that would reject good proposals.
    agent = ScreeningAgent(_client(), categories=[])

    with pytest.raises(MissingProjectError):
        await agent.run(
            ScreeningInput(workflow_run_id=1, project_id=1, project=None, proposals=[])
        )


def test_a_screening_row_defaults_to_the_pessimistic_band():
    # An unparsed model response must not come back looking like a match.
    assert ProposalScreening(proposal_id=1, fit_score=0.0).recommendation == "weak_match"
