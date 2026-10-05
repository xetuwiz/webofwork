"""The model-directed agent loop.

## Why these tests exist

The loop is the only place in this system where **the model chooses what
happens**. Every other agent's control flow is a line of Python. That makes these
tests the most load-bearing in the AI service: if the loop loses a guardrail, the
model gains control of something it should not have.

They use a **scripted model**, never a live one. Three reasons:

1. A live model cannot be asked for "exactly this sequence of tool calls, every
   time", so most of these assertions would be flaky rather than strict.
2. The guardrails must hold against a *misbehaving* model -- one that hallucinates
   tools, calls tools that raise, and never stops. A well-behaved model would
   pass every test without the guardrails existing.
3. Cost and rate limits. The suite must not depend on a provider being up.

The one thing a scripted model cannot prove is that a real model will produce
useful tool calls. That is covered by the recorded trajectory assertions in
``test_golden_cases.py`` and by the live probe in the README.
"""

from __future__ import annotations

import json

import pytest

from ai.agents.tool_loop import (
    MAX_STEPS,
    STOPPED_STEP_LIMIT,
    LoopResult,
    ToolLoopError,
    json_schema_for,
    run_tool_loop,
)
from ai.llm.base import LLMError, LLMUnavailableError, ToolCall, ToolCallResponse

# ── a scripted model ────────────────────────────────────────────────────


class ScriptedModel:
    """Replays a fixed list of turns.

    Records the messages it was handed each time, which is what lets a test
    assert that a tool result was fed back -- the part of the loop that is
    easiest to get wrong and hardest to notice.
    """

    def __init__(self, turns: list[ToolCallResponse]):
        self.turns = list(turns)
        self.seen: list[list[dict]] = []
        self.tools_seen: list[list[dict]] = []

    async def ask(self, messages: list[dict], tools: list[dict]) -> ToolCallResponse:
        self.seen.append(list(messages))
        self.tools_seen.append(list(tools))
        if not self.turns:
            raise AssertionError("the loop asked for more turns than the script has")
        return self.turns.pop(0)


def call(name: str, **args) -> ToolCallResponse:
    """A turn that requests one tool."""
    return ToolCallResponse(
        text="",
        tool_calls=(ToolCall(name=name, arguments=args, id=f"id_{name}"),),
        provider="scripted",
    )


def calls(*names: str) -> ToolCallResponse:
    """A turn that requests several tools, as a model does when it can parallelise."""
    return ToolCallResponse(
        text="",
        tool_calls=tuple(
            ToolCall(name=n, arguments={}, id=f"id_{n}") for n in names
        ),
        provider="scripted",
    )


def prose(text: str) -> ToolCallResponse:
    return ToolCallResponse(text=text, tool_calls=(), provider="scripted")


class RecordingExecutor:
    """Executes tools, optionally refusing or raising."""

    def __init__(self, *, refuse: set[str] | None = None, explode: set[str] | None = None):
        self.calls: list[tuple[str, dict]] = []
        self.refuse = refuse or set()
        self.explode = explode or set()

    async def __call__(self, name: str, arguments: dict, workflow_run_id: int) -> str:
        self.calls.append((name, arguments))
        if name in self.refuse:
            return f"REFUSED: {name} is not permitted for this agent."
        if name in self.explode:
            raise RuntimeError("the upstream service returned a 500")
        return f"result of {name}"


def schemas():
    return [
        json_schema_for("get_project", "Read a project.", {"project_id": {"type": "integer"}}, ["project_id"]),
        json_schema_for("search_freelancers", "Search the pool.", {"query": {"type": "string"}}, []),
    ]


async def run(model: ScriptedModel, executor, **kwargs) -> LoopResult:
    return await run_tool_loop(
        ask=model.ask,
        system="you are a test",
        objective="do the thing",
        tools=schemas(),
        execute=executor,
        **kwargs,
    )


# ── the happy path ──────────────────────────────────────────────────────


class TestTheLoopRuns:
    @pytest.mark.asyncio
    async def test_the_model_chooses_the_tool_and_the_loop_feeds_the_result_back(self):
        model = ScriptedModel([
            call("get_project", project_id=7),
            prose("the project needs React"),
        ])
        executor = RecordingExecutor()

        result = await run(model, executor)

        assert result.completed
        assert result.answer == "the project needs React"
        assert [name for name, _ in executor.calls] == ["get_project"]

        # The result must be in the transcript the model saw next, keyed by the
        # call id. Without this the model is asked to continue with no memory of
        # what it just learned, and produces a confident answer from nothing.
        second_turn = model.seen[1]
        tool_messages = [m for m in second_turn if m.get("role") == "tool"]
        assert len(tool_messages) == 1
        assert tool_messages[0]["content"] == "result of get_project"
        assert tool_messages[0]["tool_call_id"] == "id_get_project"

    @pytest.mark.asyncio
    async def test_the_models_own_request_is_kept_in_the_transcript(self):
        """Otherwise its follow-up call has nothing to attach to."""
        model = ScriptedModel([call("get_project"), prose("done")])
        await run(model, RecordingExecutor())

        assistant_turns = [
            m for m in model.seen[1] if m.get("role") == "assistant"
        ]
        assert assistant_turns, "the model's own tool call must be replayed to it"
        assert assistant_turns[0]["tool_calls"][0]["function"]["name"] == "get_project"

    @pytest.mark.asyncio
    async def test_the_model_may_call_several_tools_in_one_turn(self):
        model = ScriptedModel([
            calls("get_project", "search_freelancers"),
            prose("both done"),
        ])
        executor = RecordingExecutor()

        result = await run(model, executor)

        assert result.completed
        assert sorted(n for n, _ in executor.calls) == [
            "get_project",
            "search_freelancers",
        ]
        assert result.tools_called == ("get_project", "search_freelancers")

    @pytest.mark.asyncio
    async def test_the_tool_schemas_are_offered_to_the_model(self):
        model = ScriptedModel([prose("no tools needed")])

        await run(model, RecordingExecutor())

        assert [t["function"]["name"] for t in model.tools_seen[0]] == [
            "get_project",
            "search_freelancers",
        ]

    @pytest.mark.asyncio
    async def test_arguments_reach_the_executor_unchanged(self):
        model = ScriptedModel([
            call("get_project", project_id=42, extra="kept"),
            prose("ok"),
        ])
        executor = RecordingExecutor()

        await run(model, executor)

        assert executor.calls[0][1] == {"project_id": 42, "extra": "kept"}


# ── guardrail 1: a hallucinated tool ────────────────────────────────────


class TestUnknownTool:
    @pytest.mark.asyncio
    async def test_a_hallucinated_tool_is_refused_rather_than_crashing(self):
        """Lecture 05, failure mode 1.

        A model can emit a tool that does not exist. Indexing a dict raises
        KeyError and kills the run; returning a refusal gives the model something
        it can correct.
        """
        model = ScriptedModel([
            call("delete_database", table="users"),
            prose("that tool does not exist, I will use get_project"),
        ])
        executor = RecordingExecutor()

        result = await run(model, executor)

        assert result.completed, "a hallucinated tool must not end the run"
        assert "REFUSED" in result.steps[0].results["delete_database"]
        assert executor.calls == [], "nothing may execute for an unknown tool"

    @pytest.mark.asyncio
    async def test_an_empty_tool_name_is_refused(self):
        model = ScriptedModel([
            ToolCallResponse(text="", tool_calls=(ToolCall(name="", arguments={}, id="x"),)),
            prose("recovered"),
        ])

        result = await run(model, RecordingExecutor())

        assert result.completed
        assert "REFUSED" in list(result.steps[0].results.values())[0]

    @pytest.mark.asyncio
    async def test_unparseable_arguments_are_refused_not_executed(self):
        """A model that emitted malformed JSON must not have the call run.

        Silently substituting an empty dict would call the tool with nothing
        rather than nothing at all, which is worse.
        """
        model = ScriptedModel([
            ToolCallResponse(
                text="",
                tool_calls=(
                    ToolCall(
                        name="get_project",
                        arguments={"__unparseable_arguments__": "{not json"},
                        id="x",
                    ),
                ),
            ),
            prose("resending with valid JSON"),
        ])
        executor = RecordingExecutor()

        result = await run(model, executor)

        assert result.completed
        assert "REFUSED" in list(result.steps[0].results.values())[0]
        assert executor.calls == []


# ── guardrail 2: a tool that raises ─────────────────────────────────────


class TestFailingTool:
    @pytest.mark.asyncio
    async def test_an_exception_becomes_an_observation_not_a_traceback(self):
        """Lecture 05, failure mode 2.

        "An agent's error handling is not about preventing errors. It is about
        converting them into observations that the model can act on."
        """
        model = ScriptedModel([
            call("search_freelancers", query="react"),
            prose("that service is down, I will proceed without it"),
        ])
        executor = RecordingExecutor(explode={"search_freelancers"})

        result = await run(model, executor)

        assert result.completed
        outcome = result.steps[0].results["search_freelancers"]
        assert outcome.startswith("ERROR:")
        assert "RuntimeError" in outcome

    @pytest.mark.asyncio
    async def test_a_refusal_from_the_executor_is_recorded_as_refused(self):
        """The allow-list lives in the executor, so refusals come from there.

        Recorded separately from results because "the platform said no" and "the
        tool ran" are different facts for an audit trail.
        """
        model = ScriptedModel([
            call("send_invitations"),
            prose("ok"),
        ])
        executor = RecordingExecutor(refuse={"send_invitations"})

        result = await run(model, executor)

        assert result.steps[0].refused["send_invitations"].startswith("REFUSED:")


# ── guardrail 3: the step cap ───────────────────────────────────────────


class TestStepLimit:
    @pytest.mark.asyncio
    async def test_a_model_that_never_stops_is_stopped(self):
        """Lecture 05: "Autonomy without limits isn't intelligence -- it's an
        infinite loop with a credit card."
        """
        endless = [call("search_freelancers") for _ in range(50)]
        model = ScriptedModel(endless)
        executor = RecordingExecutor()

        result = await run(model, executor, max_steps=4)

        assert not result.completed
        assert result.stop_reason == "step_limit"
        assert len(result.steps) == 4
        assert len(executor.calls) == 4, "no fifth execution past the cap"

    @pytest.mark.asyncio
    async def test_a_truncated_run_says_so_rather_than_claiming_an_answer(self):
        model = ScriptedModel([call("search_freelancers") for _ in range(50)])

        result = await run(model, RecordingExecutor(), max_steps=3)

        assert result.answer == STOPPED_STEP_LIMIT
        assert "step limit" in result.answer.lower()

    @pytest.mark.asyncio
    async def test_the_cap_cannot_be_raised_past_the_module_maximum(self):
        """A caller-supplied cap is a request, not a decision.

        Without this, any call site could ask for 500 steps and the runaway
        guard would be a suggestion.
        """
        model = ScriptedModel([call("search_freelancers") for _ in range(50)])
        executor = RecordingExecutor()

        result = await run(model, executor, max_steps=500)

        assert len(result.steps) <= MAX_STEPS
        assert len(executor.calls) <= MAX_STEPS

    @pytest.mark.asyncio
    async def test_a_zero_or_negative_cap_still_runs_one_step(self):
        """A cap of zero would produce an empty answer, which is not safe.

        One step is the minimum that can return something.
        """
        model = ScriptedModel([prose("at least one turn")])

        result = await run(model, RecordingExecutor(), max_steps=0)

        assert result.completed
        assert len(result.steps) == 1


# ── failure to start ────────────────────────────────────────────────────


class TestModelUnavailable:
    @pytest.mark.asyncio
    async def test_a_model_that_cannot_be_reached_raises_rather_than_faking(self):
        class Broken:
            async def ask(self, messages, tools):
                raise LLMUnavailableError([("groq", "rate limited")])

        with pytest.raises(ToolLoopError) as caught:
            await run_tool_loop(
                ask=Broken().ask,
                system="s",
                objective="o",
                tools=schemas(),
                execute=RecordingExecutor(),
            )

        assert "rate limited" in str(caught.value)

    @pytest.mark.asyncio
    async def test_nothing_is_executed_when_the_model_never_started(self):
        executor = RecordingExecutor()

        class Broken:
            async def ask(self, messages, tools):
                raise LLMError("no network")

        with pytest.raises(ToolLoopError):
            await run_tool_loop(
                ask=Broken().ask,
                system="s",
                objective="o",
                tools=schemas(),
                execute=executor,
            )

        assert executor.calls == [], "a loop that never ran must execute nothing"


# ── the trajectory, which is what evaluation reads ──────────────────────


class TestTrajectory:
    @pytest.mark.asyncio
    async def test_every_turn_is_recorded(self):
        model = ScriptedModel([
            calls("get_project", "search_freelancers"),
            call("get_project"),
            prose("finished"),
        ])

        result = await run(model, RecordingExecutor())

        assert [s["step"] for s in result.trace()] == [0, 1, 2]
        assert result.trace()[0]["tool_calls"] == ["get_project", "search_freelancers"]
        assert result.trace()[2]["tool_calls"] == []

    @pytest.mark.asyncio
    async def test_the_trajectory_is_json_serialisable(self):
        """It is persisted, so it has to survive a round trip to PostgreSQL."""
        model = ScriptedModel([call("get_project"), prose("done")])

        trace = (await run(model, RecordingExecutor())).trace()

        assert json.loads(json.dumps(trace)) == trace

    @pytest.mark.asyncio
    async def test_a_refused_tool_is_visible_in_the_trace(self):
        """Not just absent from `results` -- positively recorded as refused.

        A trace that shows nothing for a refused call is indistinguishable from
        one where the model never asked.
        """
        model = ScriptedModel([call("send_invitations"), prose("ok")])

        trace = (await run(model, RecordingExecutor(refuse={"send_invitations"}))).trace()

        assert "REFUSED" in trace[0]["refused"]["send_invitations"]

    @pytest.mark.asyncio
    async def test_repeated_calls_of_the_same_tool_are_not_collapsed(self):
        """Calling a tool twice is behaviour worth seeing, not noise to hide."""
        model = ScriptedModel([
            call("search_freelancers", query="a"),
            call("search_freelancers", query="b"),
            prose("done"),
        ])
        executor = RecordingExecutor()

        result = await run(model, executor)

        assert result.tools_called == ("search_freelancers", "search_freelancers")

    @pytest.mark.asyncio
    async def test_a_long_result_is_truncated_before_it_re_enters_the_prompt(self):
        """A 400-item tool result is a worse use of the window than a summary."""
        model = ScriptedModel([call("search_freelancers"), prose("ok")])

        class Huge(RecordingExecutor):
            async def __call__(self, name, arguments, run_id):
                return "x" * 50_000

        result = await run(model, Huge())

        observed = result.steps[0].results["search_freelancers"]

        # Capped, and -- the part that matters -- it says so.
        #
        # A silent cut is a list that lies. Development has 199 skills; the first
        # 4000 characters cover roughly the first hundred by name, ending before
        # "HTML". A live draft read that as a complete catalogue, wrote "the
        # listing was truncated before HTML/JavaScript entries" in its own notes,
        # and settled for alphabetically early skills. The model cannot act on a
        # limitation it was never told about.
        assert len(observed) < 50_000, "the result must not re-enter the prompt whole"
        assert "TRUNCATED" in observed, (
            f"a truncated result must announce itself; got {observed[-200:]!r}"
        )
        assert "50,000" in observed or "50000" in observed, (
            "the notice must state the real size, so the model can judge how much "
            f"it is missing; got {observed[-200:]!r}"
        )

    @pytest.mark.asyncio
    async def test_a_short_result_is_not_announced_as_truncated(self):
        """The notice must not appear when nothing was cut.

        Otherwise every result carries a warning about truncation that did not
        happen, and the model learns to ignore the marker -- which is the same
        failure as never emitting it.
        """
        model = ScriptedModel([call("search_freelancers"), prose("ok")])

        class Small(RecordingExecutor):
            async def __call__(self, name, arguments, run_id):
                return [{"id": 1, "name": "React"}]

        result = await run(model, Small())

        assert "TRUNCATED" not in result.steps[0].results["search_freelancers"]

    @pytest.mark.asyncio
    async def test_tokens_accumulate_across_turns(self):
        """Cost accounting is an evaluation requirement, so it must be right."""
        model = ScriptedModel([
            ToolCallResponse(
                text="",
                tool_calls=(ToolCall(name="get_project", arguments={}, id="a"),),
                provider="scripted",
                prompt_tokens=100,
                completion_tokens=10,
            ),
            ToolCallResponse(
                text="done",
                provider="scripted",
                prompt_tokens=250,
                completion_tokens=40,
            ),
        ])

        result = await run(model, RecordingExecutor())

        assert result.prompt_tokens == 350
        assert result.completion_tokens == 50


# ── the schema builder ──────────────────────────────────────────────────


class TestSchemaBuilder:
    def test_a_schema_has_the_shape_providers_require(self):
        schema = json_schema_for(
            "get_project",
            "Read a project.",
            {"project_id": {"type": "integer", "description": "The id."}},
            ["project_id"],
        )

        assert schema["type"] == "function"
        assert schema["function"]["name"] == "get_project"
        assert schema["function"]["description"] == "Read a project."
        assert schema["function"]["parameters"]["required"] == ["project_id"]

    def test_a_tool_with_no_required_arguments_declares_none(self):
        schema = json_schema_for("list_categories", "List categories.", {}, [])

        assert schema["function"]["parameters"]["required"] == []

    @pytest.mark.asyncio
    async def test_the_description_is_what_the_model_gets(self):
        """Lab 05's cheapest agent bug: "Does stuff" is a bug.

        The description is the only documentation the model has, so it has to be
        carried through verbatim rather than replaced by the function name.
        """
        model = ScriptedModel([prose("done")])
        tools = [json_schema_for("t", "A precise description of when to use this.", {}, [])]

        await run_tool_loop(
            ask=model.ask,
            system="s",
            objective="o",
            tools=tools,
            execute=RecordingExecutor(),
        )

        assert model.tools_seen[0][0]["function"]["description"] == (
            "A precise description of when to use this."
        )


# ── advertised schemas must match the allow-list ───────────────────────


class TestAdvertisedToolsAreUsable:
    """The drift guard.

    An agent is shown tool schemas and separately holds a permission set, and
    nothing in the type system connects them. They drifted once: the planner
    advertised five tools and was permitted three, so the model could see
    `list_categories` and `search_freelancers`, try them, and be refused --
    while still producing a plausible plan, because the loop turns a refusal into
    an observation rather than an error.

    That is the worst shape of bug: silent, degrading the agent's judgement, and
    invisible in a passing test suite. A live probe missed it too, because it
    used a stub toolbox that permitted everything.

    So the agreement is asserted here instead. Cheap, and it fails loudly.
    """

    def test_planner_advertises_exactly_what_it_is_permitted(self):
        from ai.agents.planning_agent import PLANNER_TOOL_SCHEMAS
        from ai.tools.registry import AGENT_TOOL_PERMISSIONS

        advertised = {s["function"]["name"] for s in PLANNER_TOOL_SCHEMAS}
        permitted = AGENT_TOOL_PERMISSIONS["planning"]

        assert advertised == permitted, (
            f"advertised but not permitted: {sorted(advertised - permitted)}; "
            f"permitted but not advertised: {sorted(permitted - advertised)}"
        )

    def test_drafter_advertises_exactly_what_it_is_permitted(self):
        from ai.agents.draft_agent import DRAFTER_TOOL_SCHEMAS
        from ai.tools.registry import AGENT_TOOL_PERMISSIONS

        advertised = {s["function"]["name"] for s in DRAFTER_TOOL_SCHEMAS}
        permitted = AGENT_TOOL_PERMISSIONS["draft"]

        assert advertised == permitted, (
            f"advertised but not permitted: {sorted(advertised - permitted)}; "
            f"permitted but not advertised: {sorted(permitted - advertised)}"
        )

    @pytest.mark.parametrize(
        "agent,module,schemas",
        [
            ("planning", "ai.agents.planning_agent", "PLANNER_TOOL_SCHEMAS"),
            ("draft", "ai.agents.draft_agent", "DRAFTER_TOOL_SCHEMAS"),
        ],
    )
    def test_every_advertised_tool_exists_in_the_registry(self, agent, module, schemas):
        import importlib

        from ai.tools.registry import TOOL_ALLOWLIST

        advertised = {
            s["function"]["name"]
            for s in getattr(importlib.import_module(module), schemas)
        }

        assert advertised <= set(TOOL_ALLOWLIST), (
            f"{agent} advertises tools that do not exist: "
            f"{sorted(advertised - set(TOOL_ALLOWLIST))}"
        )

    def test_no_agent_may_advertise_the_tool_that_contacts_people(self):
        """The one tool that must never be model-selectable.

        ``send_invitations`` is the only capability that reaches a human. If an
        agentic agent could advertise it, the model could decide to invite
        someone -- before validation, before the approval gate -- and every
        safety property in the system would depend on a prompt saying not to.

        This is Lecture 07's least-privilege rule applied to the orchestrator
        itself: a capability that must never be exercised is removed, not
        discouraged.
        """
        from ai.agents.draft_agent import DRAFTER_TOOL_SCHEMAS
        from ai.agents.planning_agent import PLANNER_TOOL_SCHEMAS

        for schemas in (PLANNER_TOOL_SCHEMAS, DRAFTER_TOOL_SCHEMAS):
            assert "send_invitations" not in {
                s["function"]["name"] for s in schemas
            }, (
                "an agentic agent must never be offered the ability to contact "
                "anyone; that belongs to the outreach agent alone"
            )

    @pytest.mark.asyncio
    async def test_a_tool_the_allow_list_refuses_is_refused_not_errored(self):
        """The end-to-end property: advertised, permitted and reachable agree.

        Driven through the real ``ToolBox`` rather than a stub, because a stub is
        what hid this the first time.

        The assertion is on the *classification*, not just the text. The executor
        signals a refusal by raising, so without special-casing it the trace files
        "the platform denied this agent" under errors, alongside "the service was
        down" -- and an audit trail that cannot tell those apart cannot answer
        "was this agent denied access, or did it break?"
        """
        from ai.agents.planning_agent import PLANNER_TOOL_SCHEMAS
        from ai.clients.dotnet_client import DotNetClient
        from ai.config import Settings
        from ai.tools.registry import ToolBox

        # A schema the planner does NOT hold permission for.
        bogus = json_schema_for("send_invitations", "Invite someone.", {}, [])
        model = ScriptedModel([
            ToolCallResponse(
                text="",
                tool_calls=(
                    ToolCall(name="send_invitations", arguments={}, id="a"),
                    ToolCall(name="get_project", arguments={"project_id": 7}, id="b"),
                ),
            ),
            prose("understood"),
        ])

        box = ToolBox("planning", 1, DotNetClient(Settings()))

        async def execute(name, arguments, run_id):
            return await box.call(name, **arguments)

        result = await run_tool_loop(
            ask=model.ask,
            system="s",
            objective="o",
            tools=[*PLANNER_TOOL_SCHEMAS, bogus],
            execute=execute,
            max_steps=1,
        )

        step = result.steps[0]
        assert "send_invitations" in step.refused, (
            "a permissions refusal belongs in `refused`, not in `results` or "
            f"errors; got refused={step.refused}"
        )
        # get_project is permitted, so it was attempted -- and failed only
        # because the .NET API is not running, which is a different failure.
        assert "get_project" not in step.refused
