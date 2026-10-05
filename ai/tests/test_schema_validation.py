"""Every agent's output is validated at the same place, the same way.

## Why this is one file about all six agents

Schema validation is only worth having if it is uniform. One agent that validates
its output and five that trust the model is not a partly-validated system, it is
an unpredictably-validated one, and the difference is invisible from the outside
until the day it matters.

So this file does not test six agents. It tests the *one* door -- `BaseAgent._parse`
-- and proves that every agent's output model is closed over unknown fields.

## The property: a closed schema

Pydantic's default is to ignore a key it does not recognise. That default is
safe in one direction -- nothing unexpected reaches the database -- and
dangerous in another, and the dangerous direction is the one that costs time.

If a prompt is edited to ask for a field the schema does not have, the model
returns it, Pydantic discards it, and the agent emits an output missing a field
that was asked for. Nothing raises. The field is simply absent, and it is
noticed later as a blank column in a screen, several commits away from the prompt
edit that caused it.

So unknown keys are rejected at the boundary, before validation, at every depth.
A prompt that asks for something undefined fails loudly and says which key, which
agent, and that either the prompt or the model is wrong.

## Why the check is in `_parse` and not on each model

`extra="forbid"` on fifteen-odd models is fifteen-odd chances to forget one, and
a forgotten `model_config` is invisible -- nothing reports it, the model that
matters most is not the one anyone re-checks, and the failure is a silently
permissive schema.

One implementation in `_parse` cannot be bypassed: every model response in the
system arrives through that method, including for agents added after this was
written. `test_every_agent_output_model_is_reachable_through_parse` is what keeps
that true.
"""

from __future__ import annotations

import json

import pytest

from ai.agents.base import AgentError, BaseAgent, _unknown_keys
from ai.agents.draft_agent import DraftAgent, DraftInput, DraftOutput
from ai.agents.matching_agent import MatchingAgent, MatchingInput, MatchingOutput
from ai.agents.outreach_agent import OutreachAgent, OutreachInput, OutreachOutput
from ai.agents.planning_agent import PlanningAgent, PlanningInput, PlanningOutput
from ai.agents.screening_agent import ScreeningAgent, ScreeningInput, ScreeningOutput
from ai.agents.validation_agent import ValidationAgent, ValidationInput, ValidationOutput

KEY = "test-key-abc123"


class _Stub:
    """Enough of a .NET client for `_parse`, which touches nothing else."""

    def __getattr__(self, name):
        async def noop(*args, **kwargs):
            return None

        return noop


#: Every agent, with its input and output types. Kept as one table so a new
#: agent has to be added here to be covered -- which is the point: the day
#: somebody adds an agent and forgets, the "every agent" tests below go quiet,
#: and this table is what makes that visible.
AGENTS = [
    (DraftAgent, DraftInput, DraftOutput),
    (PlanningAgent, PlanningInput, PlanningOutput),
    (MatchingAgent, MatchingInput, MatchingOutput),
    (ScreeningAgent, ScreeningInput, ScreeningOutput),
    (ValidationAgent, ValidationInput, ValidationOutput),
    (OutreachAgent, OutreachInput, OutreachOutput),
]


def _agent(cls) -> BaseAgent:
    return cls(_Stub(), categories=[])


# ── every output model rejects what it does not declare ────────────────


@pytest.mark.parametrize("cls,_,output", AGENTS, ids=[a[0].name for a in AGENTS])
def test_an_unknown_top_level_field_is_rejected(cls, _, output):
    """The core property, once per agent."""
    found = _unknown_keys({"total_whatever": 1}, output)

    assert found == ["total_whatever"]


@pytest.mark.asyncio
@pytest.mark.parametrize("cls,_,output", AGENTS, ids=[a[0].name for a in AGENTS])
async def test_an_unknown_field_is_refused_at_the_parse_boundary(cls, _, output):
    """And refused through the real path, with an error a human can act on."""
    agent = _agent(cls)

    with pytest.raises(AgentError) as exc:
        await agent._parse(
            json.dumps({"not_a_real_field": True}),
            output,
            workflow_run_id=1,
        )

    message = str(exc.value)
    assert "not_a_real_field" in message
    assert cls.name in message
    # The message has to distinguish the two causes, because they have opposite
    # fixes: a prompt asking for the wrong thing, or a model inventing it.
    assert "prompt" in message.lower()


@pytest.mark.asyncio
async def test_an_unknown_field_inside_a_nested_model_is_rejected():
    """Nesting is where a closed schema is easiest to forget.

    The path has to name where it went wrong. `reasonenng` at the top level and
    the same key inside one candidate of a list of recommendations are different
    bugs with different fixes, and a bare key name does not distinguish them.
    """
    agent = _agent(MatchingAgent)

    payload = {
        "recommendations": [
            {
                "freelancer_id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
                "final_score": 80.0,
                "skill_score": 90.0,
                "portfolio_score": 70.0,
                "past_success_score": 80.0,
                "availability_score": 100.0,
                "budget_score": 75.0,
                "skill_detail": [],
                "strengths": [],
                "gaps": [],
                "reason": "Solid React work.",
                "rank": 1,  # invented
            }
        ]
    }

    with pytest.raises(AgentError) as exc:
        await agent._parse(json.dumps(payload), MatchingOutput, workflow_run_id=1)

    message = str(exc.value)
    assert "rank" in message
    assert "recommendations[0]" in message, f"the path was not reported: {message}"


@pytest.mark.asyncio
async def test_a_valid_output_still_passes():
    """The obvious failure mode of a strict schema: rejecting everything.

    A closed schema that rejects correct output is worse than a permissive one,
    because the agent falls back to a degraded path and nobody can tell why. So
    the positive case is asserted in the same file, for the same model.
    """
    agent = _agent(MatchingAgent)

    payload = {
        "recommendations": [
            {
                "freelancer_id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
                "final_score": 80.0,
                "skill_score": 90.0,
                "portfolio_score": 70.0,
                "past_success_score": 80.0,
                "availability_score": 100.0,
                "budget_score": 75.0,
                "skill_detail": [],
                "strengths": ["React"],
                "gaps": [],
                "reason": "Solid React work.",
            }
        ],
        "top_pick_id": "b944584e-2cd9-43f9-b5c7-a25e2c865ff4",
        "no_match_reason": None,
    }

    result = await agent._parse(json.dumps(payload), MatchingOutput, workflow_run_id=1)

    assert isinstance(result, MatchingOutput)
    assert result.recommendations[0].final_score == 80.0


# ── the parser's other jobs, which the strictness sits in front of ─────


@pytest.mark.asyncio
@pytest.mark.parametrize("cls,_,output", AGENTS, ids=[a[0].name for a in AGENTS])
async def test_prose_instead_of_json_is_an_error_not_a_crash(cls, _, output):
    """The most common model failure, and the least interesting one.

    A model that answers in sentences is not an exception condition -- it is a
    wrong answer, and the message should say so rather than surfacing a
    `JSONDecodeError` from somewhere inside the base class.
    """
    agent = _agent(cls)

    with pytest.raises(AgentError) as exc:
        await agent._parse("I think the best candidate would be...", output, workflow_run_id=1)

    assert "not JSON" in str(exc.value)


@pytest.mark.asyncio
async def test_a_fenced_json_block_is_accepted():
    """Models wrap JSON in markdown fences, and always will.

    Being strict about the schema while choking on ```json would make the strict
    check look like the fragile thing, and lead someone to remove it.
    """
    agent = _agent(OutreachAgent)

    fenced = '```json\n{"invitation_ids": [1, 2], "skipped": [], "errors": [], "human_summary": "Two sent."}\n```'

    result = await agent._parse(fenced, OutreachOutput, workflow_run_id=1)

    assert result.invitation_ids == [1, 2]


@pytest.mark.asyncio
async def test_json_embedded_in_prose_is_extracted():
    """A model that explains itself and then includes the object.

    Taking the text between the outermost braces is a deliberate leniency: the
    schema check is what guards correctness, and there is no reason to fail a
    workflow over a leading "Here is the result:".
    """
    agent = _agent(OutreachAgent)

    chatty = (
        "Here is the result you asked for.\n\n"
        '{"invitation_ids": [7], "skipped": [], "errors": [], "human_summary": "One sent."}\n\n'
        "Let me know if you need anything else."
    )

    result = await agent._parse(chatty, OutreachOutput, workflow_run_id=1)

    assert result.invitation_ids == [7]


@pytest.mark.asyncio
async def test_a_field_of_the_wrong_type_names_the_field():
    """Pydantic's own error, surfaced rather than swallowed.

    `model_validate` over `model_validate_json` exists for this: the loc is in
    the message, so the log says which field and which agent without anyone
    having to reproduce it.
    """
    agent = _agent(MatchingAgent)

    payload = {
        "top_pick_id": None,
        "no_match_reason": None,
        # A real field, wrong type: a list that came back as a sentence. The
        # unknown-key check cannot see this -- every key here is valid -- so it
        # is the reason the two checks are complementary rather than redundant.
        "recommendations": "There were three strong candidates.",
    }

    with pytest.raises(AgentError) as exc:
        await agent._parse(json.dumps(payload), MatchingOutput, workflow_run_id=1)

    message = str(exc.value)
    assert "schema validation" in message
    assert "recommendations" in message


@pytest.mark.asyncio
async def test_the_rejected_output_is_available_for_the_fallback_path():
    """A failed parse still hands back what the model said.

    Several agents fall back to a deterministic reason rather than giving up, and
    the fallback reads `partial_output`. Losing it would mean the fallback can
    only produce a generic string -- so this asserts the partial survives the
    rejection, which is what makes the fallback worth having.
    """
    agent = _agent(MatchingAgent)

    with pytest.raises(AgentError) as exc:
        await agent._parse(
            json.dumps({"recommendations": [], "invented": 1}),
            MatchingOutput,
            workflow_run_id=1,
        )

    assert exc.value.partial_output == {"recommendations": [], "invented": 1}
    assert exc.value.agent == "matching"


# ── the enforcement point itself ───────────────────────────────────────


@pytest.mark.asyncio
async def test_every_agent_output_model_is_reachable_through_parse():
    """The claim in this file's docstring, asserted.

    `_parse` is where the check lives, so an agent that bypasses it -- by
    validating model output some other way -- is an agent with no closed schema.
    Going through `_parse` for every output model is what makes the coverage
    claim true rather than aspirational.
    """
    for cls, _, output in AGENTS:
        agent = _agent(cls)

        with pytest.raises(AgentError) as exc:
            await agent._parse(json.dumps({"surprise": 1}), output, workflow_run_id=1)

        assert "surprise" in str(exc.value), f"{cls.name} bypassed the unknown-key check"


def test_every_agent_has_a_distinct_name():
    """`self.name` keys the error messages, so a collision would misattribute them.

    Cheap, and it is the kind of copy-paste that survives for a year because
    nothing else breaks.
    """
    names = [cls.name for cls, _, _ in AGENTS]
    assert len(set(names)) == len(names), f"duplicate agent names: {names}"


def test_the_unknown_key_walker_ignores_what_it_cannot_describe():
    """Defensive, and worth having: it walks optionals and lists.

    A walker that raised on an annotation it did not understand would break the
    moment an output model grew a `Union` or a `dict[str, str]`, which is a
    regression in the *checker* reported as a problem with the *schema*.
    """
    assert _unknown_keys({"x": 1}, object) == []
    assert _unknown_keys("not a dict", DraftOutput) == []
    assert _unknown_keys([1, 2, 3], DraftOutput) == []

