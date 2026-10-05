"""The model-directed agent loop.

## Why this module exists

Until now the system had **no** model-directed control flow. Every tool call was a
hard-coded line of Python and every agent sequence was a ``frozenset`` of stages.
That satisfies most of the Agentic AI acceptance criteria in the assignment, but
not the three that make a system *agentic* rather than a pipeline with good
plumbing:

- "analyses a user objective, creates a structured multi-step plan"
- "delegates steps to distinct agent roles"
- "calls allow-listed tools using validated inputs and structured outputs"

and it does not satisfy Lecture 05's definition, which is an LLM "directing a
loop: it observes state, decides on actions, a runtime executes them, and results
feed back."

This module is that loop. It is deliberately hand-rolled rather than built on
``create_agent`` for three reasons:

1. **The transcript has to be inspectable.** Every turn is recorded as a
   :class:`LoopStep`, which is what the trajectory checks in the evaluation read.
2. **The existing provider chain already does key rotation, rate-limit
   backoff, caching and budget enforcement.** A framework's ``bind_tools`` would
   bypass all of it, or require reimplementing it.
3. **The tool executor is :class:`~ai.tools.registry.ToolBox`,** which already
   enforces the per-agent allow-list. The allow-list is the security property the
   assignment asks for, and it must not live in two places.

## The three guardrails

Lecture 05 names these as the failure modes a naive loop does not handle. All
three are here, in code rather than in a prompt:

1. **Unknown tool.** A model can hallucinate a tool name. Handing that to
   ``TOOLS[...]`` raises ``KeyError`` and kills the run, so a lookup miss becomes
   an *observation* the model can correct.
2. **Tool raises.** A traceback is not something a model can act on, so failures
   are returned as text.
3. **Runaway loop.** ``max_steps`` is an integer bound in code. A prompt asking
   the model to stop is not a bound.

## What the model is and is not allowed to decide

It decides **which tool, with what arguments, and when to stop**. It does not
decide anything about the *result*: the executor validates against the tool's
allow-list and schema, and downstream deterministic code owns every score, price
and verdict. That split is the reason the golden cases still assert exact numbers
while the trajectory itself is model-chosen.
"""

from __future__ import annotations

import json
import logging
from dataclasses import dataclass, field
from typing import Any

from ..llm.base import LLMError, ProviderError, ToolCall, ToolCallResponse
from ..llm.providers import TOOL_METADATA_KEY

log = logging.getLogger(__name__)

#: Hard ceiling on model turns for one run.
#:
#: Not configurable per call site on purpose. A per-call parameter is how a
#: runaway budget becomes reachable, and the only value that justifies a high cap
#: is the caller asserting it needs one. Lecture 05 is explicit: "Autonomy without
#: limits isn't intelligence -- it's an infinite loop with a credit card."
MAX_STEPS = 8

#: Refusal when the cap is hit.
#:
#: "stopped" rather than "answered", because those are different claims. A caller
#: that treats a truncated run as a complete answer is the bug the lab's
#: ``stop_reason`` field exists to prevent.
STOPPED_STEP_LIMIT = (
    "Stopped: reached the step limit without a final answer. "
    "The partial trace above is what was established."
)

#: Longest tool result handed back to the model, before the truncation notice.
#:
#: Generous enough for a small catalogue and small enough that a 654-skill list
#: cannot eat the window. Exceeding it is not itself an error, so the result is
#: cut with an explicit notice rather than refused.
MAX_TOOL_RESULT_CHARS = 4000

#: Exception names that mean "refused", not "broke".
#:
#: Matched by name so this module does not import the executor. A refusal is a
#: *policy* outcome and belongs in ``refused`` in the trajectory; filing it under
#: errors makes "the platform denied this" and "the service was down" the same
#: event, which is exactly the distinction an audit trail exists to preserve.
_REFUSAL_EXCEPTIONS = frozenset(
    {
        "ToolNotPermittedError",
        "UnknownToolError",
        "PermissionError",
    }
)


class ToolLoopError(RuntimeError):
    """The loop could not run at all -- as opposed to running and failing."""


@dataclass(frozen=True)
class LoopStep:
    """One turn, recorded whether or not it succeeded.

    Recorded on failure as well as success, and that is the point: an audit trail
    that omits the failed turns is not an audit trail. A run that hit the cap is
    the run most worth reading.
    """

    #: 0-based turn index.
    index: int
    #: Prose the model produced this turn. Empty when it only asked for a tool.
    text: str
    #: Tools it asked for. Empty on the final turn.
    tool_calls: tuple[str, ...] = ()
    #: Tool name -> what the runtime returned, including refusals.
    results: dict[str, str] = field(default_factory=dict)
    #: Tools refused before execution, with the reason.
    refused: dict[str, str] = field(default_factory=dict)
    prompt_tokens: int | None = None
    completion_tokens: int | None = None

    @property
    def tokens(self) -> int:
        return (self.prompt_tokens or 0) + (self.completion_tokens or 0)


@dataclass(frozen=True)
class LoopResult:
    """The outcome of one run."""

    #: The model's final answer, or the stop notice when the cap was hit.
    answer: str
    #: Every turn, in order. The trajectory.
    steps: tuple[LoopStep, ...]
    #: `"answered"` or `"step_limit"`.
    stop_reason: str
    prompt_tokens: int = 0
    completion_tokens: int = 0

    @property
    def completed(self) -> bool:
        """Whether the model finished on its own terms.

        A truncated run is not a successful one, and anything downstream that
        treats it as one is how a partial result reaches a client as if it were
        whole.
        """
        return self.stop_reason == "answered"

    @property
    def tools_called(self) -> tuple[str, ...]:
        """Every tool name that actually executed, in order.

        Duplicates are kept: calling the same tool twice is a real behaviour worth
        seeing, not noise to be deduplicated away.
        """
        return tuple(name for step in self.steps for name in step.results)

    def trace(self) -> list[dict[str, Any]]:
        """The trajectory as plain data, for persistence and evaluation."""
        return [
            {
                "step": step.index,
                "text": step.text[:500],
                "tool_calls": list(step.tool_calls),
                "results": {k: v[:500] for k, v in step.results.items()},
                "refused": step.refused,
                "tokens": step.tokens,
            }
            for step in self.steps
        ]


def json_schema_for(name: str, description: str, properties: dict, required: list[str]) -> dict:
    """Build one tool schema in the shape every OpenAI-compatible provider expects.

    Built here rather than imported from LangChain so the schemas are identical
    whatever is behind :class:`~ai.llm.base.Provider`, and so a test can assert on
    them without a framework dependency.
    """
    return {
        "type": "function",
        "function": {
            "name": name,
            "description": description,
            "parameters": {
                "type": "object",
                "properties": properties,
                "required": required,
            },
        },
    }


def _message_for_tool_result(call: ToolCall, content: str) -> dict[str, Any]:
    """Pair a result with the request that asked for it.

    The ``tool_call_id`` must match or the provider cannot connect result to
    request, and the next turn fails in a way that looks like a model problem.
    """
    return {"role": "tool", "tool_call_id": call.id, "content": content}


async def run_tool_loop(
    *,
    ask: Any,
    system: str,
    objective: str,
    tools: list[dict],
    execute: Any,
    max_steps: int = MAX_STEPS,
    workflow_run_id: int = 0,
) -> LoopResult:
    """Run the model-directed loop until it answers or the cap is reached.

    Args:
        ask: Coroutine taking ``(messages, tools)`` and returning a
            :class:`~ai.llm.base.ToolCallResponse`. Injected rather than imported
            so the loop can be tested with a scripted model and no network, which
            is the only way to test the guardrails deterministically.
        system: System instruction. States what the agent may do and when to stop.
        objective: The domain objective, in the user's own framing.
        tools: Tool schemas advertised to the model.
        execute: Coroutine taking ``(name, arguments)`` and returning a string
            result or raising. This is the :class:`~ai.tools.registry.ToolBox`
            boundary, where the allow-list lives.
        max_steps: Ceiling on model turns. Capped at :data:`MAX_STEPS` regardless
            of what is passed.
        workflow_run_id: Scopes the budget and the log.

    Returns:
        A :class:`LoopResult` carrying the answer and the full trajectory.

    Raises:
        ToolLoopError: the model could not be reached at all. A loop that cannot
            start is a different failure from one that runs and gets nowhere, and
            conflating them hides which happened.
    """
    cap = max(1, min(int(max_steps), MAX_STEPS))

    # Derived from the schemas actually advertised, so the two can never drift.
    # If a caller advertises a tool the executor would refuse, the model finds
    # out as a REFUSED observation rather than by never being able to use it.
    allowed = frozenset(
        str(spec.get("function", {}).get("name") or "")
        for spec in tools
        if str(spec.get("function", {}).get("name") or "")
    )

    messages: list[dict[str, Any]] = [
        {"role": "system", "content": system},
        {"role": "user", "content": objective},
    ]

    steps: list[LoopStep] = []
    prompt_tokens = 0
    completion_tokens = 0

    for index in range(cap):
        turn: ToolCallResponse
        try:
            turn = await ask(messages, tools)
        except LLMError as exc:
            # No turn happened, so there is no step to record. Raised rather than
            # swallowed: the caller has a real fallback path for this and a
            # fabricated "answer" would be worse than an exception.
            raise ToolLoopError(f"model call failed on step {index}: {exc}") from exc

        prompt_tokens += turn.prompt_tokens or 0
        completion_tokens += turn.completion_tokens or 0

        # No tool calls means the model chose to speak, and that is the exit
        # condition. It is the only exit: the model terminates the loop, not us.
        if not turn.wants_tools:
            steps.append(
                LoopStep(
                    index=index,
                    text=turn.text,
                    prompt_tokens=turn.prompt_tokens,
                    completion_tokens=turn.completion_tokens,
                )
            )
            return LoopResult(
                answer=turn.text.strip(),
                steps=tuple(steps),
                stop_reason="answered",
                prompt_tokens=prompt_tokens,
                completion_tokens=completion_tokens,
            )

        # The model's own turn goes back in before the results, or its follow-up
        # request has no context to attach to.
        #
        # `TOOL_METADATA_KEY` carries per-call provider state that the OpenAI
        # shape has nowhere to put. Today that is Gemini's `thoughtSignature`,
        # without which its API rejects the follow-up turn with a 400 -- so this
        # is what makes Gemini usable in this loop at all, rather than working
        # for exactly one turn. Opaque here on purpose: the loop copies it
        # without knowing which provider needed it, so a second provider needing
        # it does not mean editing this loop again.
        metadata = {
            call.id: call.provider_metadata
            for call in turn.tool_calls
            if call.provider_metadata
        }

        assistant_message: dict[str, Any] = {
            "role": "assistant",
            "content": turn.text or "",
            "tool_calls": [
                {
                    "id": call.id,
                    "type": "function",
                    "function": {
                        "name": call.name,
                        "arguments": json.dumps(call.arguments),
                    },
                }
                for call in turn.tool_calls
            ],
        }
        if metadata:
            assistant_message[TOOL_METADATA_KEY] = metadata

        messages.append(assistant_message)

        results: dict[str, str] = {}
        refused: dict[str, str] = {}

        for call in turn.tool_calls:
            outcome = await _execute_one(call, execute, workflow_run_id, allowed)
            if outcome.startswith("REFUSED"):
                refused[call.name] = outcome
            results[call.name] = outcome
            messages.append(_message_for_tool_result(call, outcome))

        steps.append(
            LoopStep(
                index=index,
                text=turn.text,
                tool_calls=tuple(c.name for c in turn.tool_calls),
                results=results,
                refused=refused,
                prompt_tokens=turn.prompt_tokens,
                completion_tokens=turn.completion_tokens,
            )
        )

    return LoopResult(
        answer=STOPPED_STEP_LIMIT,
        steps=tuple(steps),
        stop_reason="step_limit",
        prompt_tokens=prompt_tokens,
        completion_tokens=completion_tokens,
    )


async def _execute_one(
    call: ToolCall,
    execute: Any,
    workflow_run_id: int,
    allowed: frozenset[str],
) -> str:
    """Run one tool call, converting every failure into an observation.

    Four outcomes, all strings, because all four are things the model can act on:

    * ``REFUSED`` -- the tool was not advertised, or the executor declined it,
    * ``ERROR`` -- the tool raised,
    * the result.

    A traceback is not an observation. ``ERROR`` plus the exception type is.

    **The allow-list is checked here as well as in the executor**, and the
    duplication is deliberate. The executor's check is the security property --
    it is what stops an agent reaching a tool it must not have. This one is a
    correctness property: without it a hallucinated tool name is passed straight
    through to whatever the executor happens to be, and a permissive or
    stubbed executor will happily "run" it. That is exactly what happened the
    first time this was written, and the test for it caught it.
    """
    if not call.name:
        return "REFUSED: a tool call arrived with no name."

    if call.name not in allowed:
        # Named explicitly so the model knows the difference between "that tool
        # does not exist" and "that tool exists and you may not use it" -- the
        # second is a permissions problem it cannot solve by trying again.
        return (
            f"REFUSED: there is no tool called {call.name!r}. "
            f"Available tools: {', '.join(sorted(allowed)) or 'none'}."
        )

    if "__unparseable_arguments__" in call.arguments:
        return (
            "REFUSED: the arguments were not valid JSON and were not executed. "
            "Resend the call with a valid JSON object."
        )

    try:
        result = await execute(call.name, call.arguments, workflow_run_id)
    except Exception as exc:  # noqa: BLE001 - deliberately broad, see docstring
        # A permissions refusal is not a crash. The executor signals it by
        # raising `ToolNotPermittedError`, and it must stay distinguishable from a
        # genuine failure in the trajectory: "the platform said no" and "the tool
        # broke" are different facts, and an audit trail that conflates them
        # cannot answer "was this agent denied access, or did it hit an error?"
        #
        # Classified by exception *name* rather than by importing the registry,
        # because the loop is deliberately independent of the executor's
        # vocabulary -- a different executor should not need this module edited.
        if type(exc).__name__ in _REFUSAL_EXCEPTIONS:
            log.info("tool %s refused: %s", call.name, exc)
            return f"REFUSED: {exc}"

        log.warning("tool %s raised %s: %s", call.name, type(exc).__name__, exc)
        return f"ERROR: {type(exc).__name__}: {exc}"

    text = result if isinstance(result, str) else json.dumps(result, default=str)
    # Truncated because it goes straight back into the prompt, and a 400-item
    # tool result is a worse use of the window than a summary plus an offer to
    # narrow the query.
    #
    # The truncation has to be *announced*. A silent cut is not a smaller list, it
    # is a list that lies: the model cannot tell "these are all the skills in this
    # category" from "these are the first hundred, alphabetically".
    #
    # That distinction decided a live draft. Development has 199 skills; the first
    # 4000 characters cover roughly the first hundred by name, ending before
    # "HTML" and "JavaScript". The model, told nothing, wrote in its own notes
    # "the catalogue listing was truncated before HTML/JavaScript entries, so no
    # ids were invented for them" -- correctly noticing the absence, wrongly
    # concluding the absence was real, and settling for alphabetically early
    # skills like CSS3 and Bootstrap. An unlabelled truncation manufactures a
    # false fact about the catalogue, which is the same failure as the empty list
    # it replaced: a confident claim that this category has no such skills.
    if len(text) <= MAX_TOOL_RESULT_CHARS:
        return text
    return (
        text[:MAX_TOOL_RESULT_CHARS]
        + f"\n\n[TRUNCATED: this result was {len(text)} characters and only the first "
        f"{MAX_TOOL_RESULT_CHARS} are shown. The list above is NOT complete -- it is "
        f"the beginning of a longer list, in the order the platform returned it. Do "
        f"not conclude that something is absent because it is missing here. Narrow "
        f"the query or filter the result to see the rest.]"
    )
