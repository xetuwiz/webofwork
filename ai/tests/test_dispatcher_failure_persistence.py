"""Does a failed stage stop looking alive?

## The failure this exists for

Run 23 died on the tool gate:

    agent tool gate rejected a call: Agent 'matching' may not use 'get_project'
    WARNING ai.graph.dispatcher: stage talent_match failed for run 23
    INFO    ai.main: agent/chat run=23 stage=talent_match -> failed in 0ms

and then the client's next press of "Find best talent" returned the same dead run
in 539ms, without calling a model. The shortlist page read `pending` forever.

## Why

`_failed()` returned `status="failed"` in the HTTP body and stopped there. Every
*success* path calls `complete_workflow`, which is what writes the run's status.
The failure path did not, so `workflow_runs.status` stayed `Pending`.

That matters because `AiTalentMatchController.Start` de-duplicates on

    WHERE project_id = @id AND stage = 'talent_match' AND status IN (0, 1, 9)

`Pending` is 0. So a run that had failed was indistinguishable from one that was
mid-flight, and the de-duplication kept handing it back instead of re-dispatching.
The feature was unrecoverable without editing the database.

## What is asserted

That the failure is *persisted*, not merely reported. A test asserting the
returned `status == "failed"` passes in both the broken and the fixed state, which
is why it is not the test that matters.
"""

from __future__ import annotations

from typing import Any

from ai.graph.dispatcher import AgentChatRequest, StageDispatcher
from ai.tools.registry import ToolNotPermittedError


class RecordingClient:
    """A `DotNetClient` stand-in that records writes instead of making them."""

    def __init__(self, reads: int = 0) -> None:
        self.reads = reads
        self.read_budget = 20
        self.steps: list[dict[str, Any]] = []
        self.completions: list[int] = []
        #: Set to make every write raise, for the "cannot record it" path.
        self.writes_fail = False

    async def append_step(self, workflow_run_id: int, **kwargs: Any) -> Any:
        if self.writes_fail:
            raise RuntimeError("the API is down")
        self.steps.append({"workflow_run_id": workflow_run_id, **kwargs})
        return None

    async def complete_workflow(self, workflow_run_id: int, output: Any) -> Any:
        self.completions.append(workflow_run_id)
        return None


def _dispatcher(stage: str = "talent_match", client: RecordingClient | None = None) -> StageDispatcher:
    request = {
        "workflow_run_id": 23,
        "stage": stage,
        "message": "",
        "project_id": 11,
        "context": {},
    }

    class FakeSettings:
        llm_provider_chain = "fixture"
        llm_cache_enabled = False
        llm_max_calls_per_workflow = 15

        def keys_for(self, _name: str) -> list[str]:
            return []

        def providers(self) -> list[str]:
            return ["fixture"]

    dispatcher = StageDispatcher.__new__(StageDispatcher)
    dispatcher.request = _request(request)
    dispatcher.settings = FakeSettings()
    dispatcher.client = client or RecordingClient()
    dispatcher._categories = []
    return dispatcher


def _request(payload: dict[str, Any]) -> AgentChatRequest:
    return AgentChatRequest(**payload)


class TestAFailedStageIsRecorded:
    async def test_a_tool_gate_failure_is_written_to_the_run(self):
        client = RecordingClient()
        dispatcher = _dispatcher(client=client)

        # The exact failure from run 23: our own code called a tool the gate
        # refuses, outside the LLM loop, so nothing caught it inside.
        async def boom(*_args: Any, **_kwargs: Any):
            raise ToolNotPermittedError("Agent 'matching' may not use 'get_project'.")

        dispatcher._dispatch = boom

        response = await dispatcher.run()

        assert response.status == "failed"
        assert len(client.steps) == 1, "the failure was never persisted"

    async def test_the_recorded_status_is_failed_not_pending(self):
        # The whole bug in one assertion. `Pending` is what made `Start` keep
        # returning the run instead of re-dispatching it.
        client = RecordingClient()
        dispatcher = _dispatcher(client=client)

        async def boom(*_args: Any, **_kwargs: Any):
            raise ToolNotPermittedError("nope")

        dispatcher._dispatch = boom
        await dispatcher.run()

        assert client.steps[0]["status"] == "Failed"

    async def test_the_error_message_is_recorded_not_only_returned(self):
        # A run marked Failed with no explanation is not debuggable from the UI.
        client = RecordingClient()
        dispatcher = _dispatcher(client=client)

        async def boom(*_args: Any, **_kwargs: Any):
            raise ToolNotPermittedError("Agent 'matching' may not use 'get_project'.")

        dispatcher._dispatch = boom
        await dispatcher.run()

        assert "get_project" in str(client.steps[0]["output_summary"])

    async def test_the_step_is_named_for_the_stage_that_failed(self):
        client = RecordingClient()
        dispatcher = _dispatcher(stage="screen", client=client)

        async def boom(*_args: Any, **_kwargs: Any):
            raise RuntimeError("nope")

        dispatcher._dispatch = boom
        await dispatcher.run()

        assert client.steps[0]["step_name"] == "screen"


class TestAnUnknownStageIsAlsoRecorded:
    async def test_a_run_dispatched_to_a_stage_that_does_not_exist_is_failed(self):
        # Checked before dispatch, so nothing would have been written at all. A
        # typo in a stage name would otherwise leave a permanently Pending run.
        client = RecordingClient()
        dispatcher = _dispatcher(stage="not_a_stage", client=client)

        response = await dispatcher.run()

        assert response.status == "failed"
        assert client.steps and client.steps[0]["status"] == "Failed"


class TestReportingSurvivesAFailedWrite:
    async def test_the_failure_is_still_reported_when_it_cannot_be_recorded(self):
        # The two halves are independent. If the API is unreachable, the caller
        # must still be told the stage failed -- losing that report because the
        # bookkeeping failed would replace one useful error with a less useful one.
        client = RecordingClient()
        client.writes_fail = True
        dispatcher = _dispatcher(client=client)

        async def boom(*_args: Any, **_kwargs: Any):
            raise ToolNotPermittedError("nope")

        dispatcher._dispatch = boom

        response = await dispatcher.run()

        assert response.status == "failed"
        assert response.error


class TestNoDoubleCompletion:
    async def test_a_failure_does_not_also_call_complete_workflow(self):
        # `complete_workflow` hardcodes `WorkflowRunStatus.Completed`. Writing
        # both would leave a failed run marked Completed, which is worse than
        # either alone.
        client = RecordingClient()
        dispatcher = _dispatcher(client=client)

        async def boom(*_args: Any, **_kwargs: Any):
            raise RuntimeError("nope")

        dispatcher._dispatch = boom
        await dispatcher.run()

        assert client.completions == []
