"""Does a run that stops for a person get recorded as stopping?

## The failure this exists for

Run 24 planned, matched and validated, then paused -- which is the *successful*
outcome of the talent-match stage. It left:

    workflow_runs.final_output  -> NULL
    workflow_runs.status        -> 0 (Pending)

`_screen` and `_validate` both call `complete_workflow`; `_talent_match` did not,
and it is the only stage that ends paused. Three failures came out of that one
omission:

* `final_output` is where the planner's weights live. Null meant the shortlist
  page had no weights and fell back to the panel defaults, captioned "The AI has
  not chosen its weights yet".
* `Pending` is `0`, which is inside `Start`'s de-duplication set
  `status IN (0, 1, 9)`, so every later press of "Find best talent" handed the
  finished run back instead of re-dispatching it.
* The page rendered "pending" over a run that was actually waiting for approval.

## What is asserted

The write, and the status it carries. Asserting the returned tuple is not enough:
it was already `("paused", ...)` in the broken state, which is exactly why this
went unnoticed for so long.
"""

from __future__ import annotations

import ast
from pathlib import Path
from typing import Any

from ai.graph.dispatcher import AgentChatRequest, StageDispatcher


class RecordingClient:
    """A `DotNetClient` stand-in that records writes instead of making them."""

    def __init__(self) -> None:
        self.reads = 0
        self.read_budget = 20
        self.completions: list[dict[str, Any]] = []
        self.steps: list[dict[str, Any]] = []
        #: Set to make every write raise, for the "cannot record it" path.
        self.writes_fail = False

    async def complete_workflow(
        self, workflow_run_id: int, output: Any, status: str = "Completed"
    ) -> Any:
        if self.writes_fail:
            raise RuntimeError("the API is down")
        self.completions.append(
            {"workflow_run_id": workflow_run_id, "output": output, "status": status}
        )
        return None

    async def append_step(self, workflow_run_id: int, **kwargs: Any) -> Any:
        if self.writes_fail:
            raise RuntimeError("the API is down")
        self.steps.append({"workflow_run_id": workflow_run_id, **kwargs})
        return None


def _dispatcher(client: RecordingClient | None = None) -> StageDispatcher:
    class FakeSettings:
        llm_provider_chain = "fixture"
        llm_cache_enabled = False
        llm_max_calls_per_workflow = 15

        def keys_for(self, _name: str) -> list[str]:
            return []

        def providers(self) -> list[str]:
            return ["fixture"]

    dispatcher = StageDispatcher.__new__(StageDispatcher)
    dispatcher.request = AgentChatRequest(
        workflow_run_id=24,
        stage="talent_match",
        message="",
        project_id=11,
        context={},
    )
    dispatcher.settings = FakeSettings()
    dispatcher.client = client or RecordingClient()
    dispatcher._categories = []
    return dispatcher


PAYLOAD = {"plan": {"weights": {"skill_coverage": 0.4}}, "plan_source": "planner"}


class TestAPauseIsRecorded:
    async def test_the_output_is_written(self):
        client = RecordingClient()
        dispatcher = _dispatcher(client)

        status, _summary, payload = await dispatcher._paused_at_gate("ready", PAYLOAD)

        assert status == "paused"
        assert len(client.completions) == 1, "the pause was never persisted"
        assert client.completions[0]["output"] == PAYLOAD

    async def test_the_run_lands_awaiting_client_approval(self):
        # The status is the whole point. `Pending` is 0, inside `Start`'s
        # dedup set, which is what made the button a no-op forever.
        client = RecordingClient()
        dispatcher = _dispatcher(client)

        await dispatcher._paused_at_gate("ready", PAYLOAD)

        assert client.completions[0]["status"] == "PendingClientApproval"

    async def test_a_gate_rejection_does_not_enable_the_approve_control(self):
        # Nothing for the client to approve, so PendingClientApproval would be a
        # lie the UI acts on: `ShortlistApprovalPanel` enables itself on exactly
        # that status.
        client = RecordingClient()
        dispatcher = _dispatcher(client)

        await dispatcher._paused_at_gate("rejected", PAYLOAD, gate_rejected=True)

        assert client.completions[0]["status"] == "PendingApproval"

    async def test_it_is_written_against_the_right_run(self):
        client = RecordingClient()
        dispatcher = _dispatcher(client)

        await dispatcher._paused_at_gate("ready", PAYLOAD)

        assert client.completions[0]["workflow_run_id"] == 24

    async def test_the_report_survives_a_failed_write(self):
        # The two halves are independent. Losing the pause report because the
        # bookkeeping failed would replace one useful message with a less useful
        # one.
        client = RecordingClient()
        client.writes_fail = True
        dispatcher = _dispatcher(client)

        status, summary, payload = await dispatcher._paused_at_gate("ready", PAYLOAD)

        assert status == "paused"
        assert summary == "ready"
        assert payload == PAYLOAD

    async def test_a_failure_is_not_also_recorded_as_a_failed_step(self):
        # `append_step` applies the status too. Calling both would leave the run
        # marked Failed by whichever landed last.
        client = RecordingClient()
        dispatcher = _dispatcher(client)

        await dispatcher._paused_at_gate("ready", PAYLOAD)

        assert client.steps == []


class TestTheStageActuallyCallsIt:
    """The helper is worthless if the stage does not call it.

    Read out of the source with `ast` rather than executed, because running it
    means running three agents and a live model. The alternative -- calling the
    helper directly, as a behavioural test would -- passes identically whether or
    not `_talent_match` calls it, which is the thing that actually broke.
    """

    @staticmethod
    def _talent_match_source() -> ast.FunctionDef:
        import ai.graph.dispatcher as module

        tree = ast.parse(Path(module.__file__).read_text(encoding="utf-8"))
        for node in ast.walk(tree):
            if isinstance(node, ast.ClassDef) and node.name == "StageDispatcher":
                for child in node.body:
                    if isinstance(child, ast.AsyncFunctionDef) and child.name == "_talent_match":
                        return child
        raise AssertionError("_talent_match was not found on StageDispatcher")

    def test_talent_match_never_returns_paused_on_its_own(self):
        func = self._talent_match_source()

        bare = [
            node for node in ast.walk(func)
            if isinstance(node, ast.Return) and isinstance(node.value, ast.Constant)
            and node.value.value == "paused"
        ]

        assert bare == [], (
            "_talent_match returns 'paused' without going through _paused_at_gate, "
            "so the run is left looking Pending and Start keeps returning it"
        )

    def test_talent_match_routes_both_branches_through_the_helper(self):
        func = self._talent_match_source()

        calls = [
            node for node in ast.walk(func)
            if isinstance(node, ast.Call)
            and isinstance(node.func, ast.Attribute)
            and node.func.attr == "_paused_at_gate"
        ]

        assert len(calls) == 2, (
            "expected the approval branch and the validation-rejected branch to "
            f"both persist, found {len(calls)} call(s)"
        )

    def test_the_rejected_branch_says_so(self):
        func = self._talent_match_source()

        assert any(
            isinstance(node, ast.Call)
            and isinstance(node.func, ast.Attribute)
            and node.func.attr == "_paused_at_gate"
            and any(
                kw.arg == "gate_rejected"
                and isinstance(kw.value, ast.Constant)
                and kw.value.value is True
                for kw in node.keywords
            )
            for node in ast.walk(func)
        ), "the validation-rejected branch must not park on the client-approval gate"