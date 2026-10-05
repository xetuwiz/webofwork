"""When drafting is impossible, asking a question must still work.

`DraftAgent.run` has a branch for "no provider could tool-call, or the loop ran
out of steps": return a draft carrying a `clarification_question` and
`confidence=0.0` instead of an empty project. A half-draft is worse than an
honest question.

That branch was unreachable. It built a `DraftOutput` without `description`,
`requirements` or `expected_deliverables` -- all three required by the contract --
so construction raised `ValidationError` and the run failed with three missing-field
errors instead of returning the clarification. Seen twice in one live session:
runs 8 and 9 both died there.

The failure is quiet in the worst way. The intent is in a comment, the code reads
as if it works, and the path only executes when something has *already* gone wrong
-- so it converts a recoverable provider problem into an opaque one.
"""

from __future__ import annotations

import pytest

from ai.agents.draft_agent import DraftAgent, DraftInput

BRIEF = "I am looking for a freelancer to help create a modern, responsive website."


class _StubClient:
    async def append_step(self, *args, **kwargs) -> None:
        return None


@pytest.fixture
def agent() -> DraftAgent:
    return DraftAgent(client=_StubClient())  # type: ignore[arg-type]


class _Box:
    """A toolbox with no permitted tools, so the loop cannot even start."""

    def call(self, *a, **k):  # pragma: no cover - never reached
        raise AssertionError("the loop must not reach a tool")


@pytest.mark.asyncio
async def test_an_unavailable_loop_returns_a_question_not_an_exception(agent, monkeypatch):
    async def no_loop(*, payload, toolbox, workflow_run_id):
        return None

    monkeypatch.setattr(agent, "_draft_with_tools", no_loop)
    agent._toolbox = lambda run_id: _Box()  # type: ignore[method-assign]

    out = await agent.run(DraftInput(workflow_run_id=1, partial_text=BRIEF))

    assert out.clarification_question, (
        "the client must be asked something; this branch exists so a provider "
        "failure does not become an empty project"
    )
    assert out.confidence == 0.0, (
        "a clarification request must not be mistaken for a draft worth trusting"
    )


@pytest.mark.asyncio
async def test_the_fallback_echoes_the_client_rather_than_blanking_the_form(agent, monkeypatch):
    """The form the client sees must not be wiped out by a provider failure.

    The three prose fields are required by the contract. Filling them with empty
    strings would satisfy pydantic and blank the client's own words out of the
    form, which is a worse outcome than the failure that caused it.
    """

    async def no_loop(*, payload, toolbox, workflow_run_id):
        return None

    monkeypatch.setattr(agent, "_draft_with_tools", no_loop)
    agent._toolbox = lambda run_id: _Box()  # type: ignore[method-assign]

    out = await agent.run(DraftInput(workflow_run_id=1, partial_text=BRIEF))

    assert out.description.strip(), "the client's own brief was discarded"
    assert out.requirements.strip()
    assert out.expected_deliverables.strip()


@pytest.mark.asyncio
async def test_a_loop_that_ran_out_of_steps_takes_the_same_branch(agent, monkeypatch):
    """`completed=False` is the other way in, and it must not raise either."""

    class _Unfinished:
        completed = False

        def trace(self):
            return []

    async def stalled_loop(*, payload, toolbox, workflow_run_id):
        return _Unfinished()

    monkeypatch.setattr(agent, "_draft_with_tools", stalled_loop)
    agent._toolbox = lambda run_id: _Box()  # type: ignore[method-assign]

    out = await agent.run(DraftInput(workflow_run_id=1, partial_text=BRIEF))

    assert out.clarification_question