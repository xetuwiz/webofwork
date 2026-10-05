"""Rotation, backoff and the circuit breaker -- and the bug that made all three moot.

## The bug this file exists for

The chain's key-rotation loop read:

    for key_index, key in enumerate(getattr(provider, "_keys", []) or [None]):
        response = await provider.complete(stamped, system=system)

`key_index` was computed and then thrown away. `complete` defaults to `-1`, which
indexes `_keys[-1]` -- so every iteration re-sent the identical request on the
identical exhausted key, once per key in the list, before falling through to the
next provider.

`ProviderSpec`'s docstring claimed rotation was "the cheapest defence against a
per-key rate limit", and `OpenAICompatibleProvider.complete` carried a comment
describing this exact defect as already fixed. Both described an intention; the
call site was never changed. Verified live: with a single groq key, six calls in
four seconds and five 429s, all on that one key.

Rotation on its own would not have fixed that anyway -- groq had one key. The
thing that fixes it is remembering the key is exhausted so the *next* request
skips it too, which is what `CooldownRegistry` is for.
"""

from __future__ import annotations

import httpx
import pytest
import respx

from ai.config import Settings
from ai.llm.base import RateLimitError, ToolCall, ToolCallResponse
from ai.llm.chain import CooldownRegistry, call_llm, call_llm_with_tools, reset_for_tests
from ai.llm.providers import GeminiProvider, OpenAICompatibleProvider


@pytest.fixture(autouse=True)
def _clean_chain():
    """Cooldowns are process-wide state; without this they leak between tests."""
    reset_for_tests()
    yield
    reset_for_tests()


def _settings(**overrides) -> Settings:
    base = {
        "llm_provider_chain": "groq",
        "llm_groq_api_keys": "k0,k1,k2",
        "llm_allow_fixture_fallback": False,
        "llm_cache_enabled": False,
        "llm_rate_limit_base_delay_seconds": 100.0,
        "llm_rate_limit_max_delay_seconds": 100.0,
        # Jitter off, so "was this key skipped" is a deterministic assertion
        # rather than a statistical one.
        "llm_rate_limit_jitter": 0.0,
    }
    base.update(overrides)
    return Settings(**base)


def _ok(text: str = "ok") -> httpx.Response:
    return httpx.Response(200, json={"choices": [{"message": {"content": text}}]})


def _tools_turn(text: str = "") -> httpx.Response:
    return httpx.Response(
        200, json={"choices": [{"message": {"content": text, "tool_calls": []}}]}
    )


# ── rotation actually rotates ─────────────────────────────────────────────


class TestRotationReachesTheProvider:
    @respx.mock
    @pytest.mark.asyncio
    async def test_a_rate_limited_key_is_not_retried(self):
        """The core regression.

        Three keys, the first exhausted. The second must be tried -- and the
        evidence is the Authorization header, because that is the only place the
        key actually appears.
        """
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[
                httpx.Response(429, json={}),
                _ok("from the second key"),
            ]
        )

        result = await call_llm(
            "prompt", agent="draft", workflow_run_id=1, settings=_settings()
        )

        assert result.text == "from the second key"
        sent = [c.request.headers["Authorization"] for c in route.calls]
        assert sent == ["Bearer k0", "Bearer k1"], (
            "the exhausted key was re-sent; rotation did not happen. "
            f"Authorization headers were {sent}"
        )

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_third_key_is_reached_when_two_are_exhausted(self):
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[
                httpx.Response(429, json={}),
                httpx.Response(429, json={}),
                _ok("third"),
            ]
        )

        result = await call_llm(
            "prompt", agent="draft", workflow_run_id=1, settings=_settings()
        )

        assert result.text == "third"
        sent = [c.request.headers["Authorization"] for c in route.calls]
        assert sent == ["Bearer k0", "Bearer k1", "Bearer k2"]

    @respx.mock
    @pytest.mark.asyncio
    async def test_tool_calling_rotates_too(self):
        """The tool path had the identical defect, and it is the hotter path."""
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[httpx.Response(429, json={}), _tools_turn("done")]
        )

        turn = await call_llm_with_tools(
            [{"role": "user", "content": "hi"}],
            [],
            agent="draft",
            workflow_run_id=1,
            settings=_settings(),
        )

        assert turn.text == "done"
        sent = [c.request.headers["Authorization"] for c in route.calls]
        assert sent == ["Bearer k0", "Bearer k1"], (
            f"tool-calling rotation is still broken; sent {sent}"
        )

    @respx.mock
    @pytest.mark.asyncio
    async def test_the_key_index_reaches_the_audit_trail(self):
        """`LLMResponse.key_index` must name the key actually used.

        It is the only record of which credential a call was charged to. If it
        always says the last key, a ten-key account produces a wrong audit trail
        nine times out of ten.
        """
        respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[httpx.Response(429, json={}), _ok("second")]
        )

        result = await call_llm(
            "prompt", agent="draft", workflow_run_id=1, settings=_settings()
        )

        assert result.key_index == 1, (
            f"the audit trail says key {result.key_index}; the second key was used"
        )


# ── the cooldown: skip, do not re-ask ─────────────────────────────────────


class TestCooldownSkipsRatherThanSleeps:
    @respx.mock
    @pytest.mark.asyncio
    async def test_a_second_request_does_not_ask_the_exhausted_key(self):
        """The reason cooldown exists at all.

        Rotation fixes *this* request. Nothing about it stops the next request --
        arriving milliseconds later from a different workflow -- from walking
        straight back into the key that just said 429.
        """
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[httpx.Response(429, json={}), _ok("second"), _ok("third")]
        )
        settings = _settings()

        # First call: key 0 is rate limited, key 1 answers.
        await call_llm("one", agent="draft", workflow_run_id=1, settings=settings)
        assert [c.request.headers["Authorization"] for c in route.calls] == [
            "Bearer k0",
            "Bearer k1",
        ]

        # Second call: key 0 must not be asked again.
        await call_llm("two", agent="draft", workflow_run_id=2, settings=settings)

        sent = [c.request.headers["Authorization"] for c in route.calls]
        assert sent == ["Bearer k0", "Bearer k1", "Bearer k1"], (
            f"the exhausted key was asked a second time: {sent}"
        )

    def test_the_backoff_doubles_per_consecutive_strike(self):
        registry = CooldownRegistry(base_delay=2.0, max_delay=100.0, jitter=0.0)

        first = registry.penalise_rate_limit("groq", 0)
        second = registry.penalise_rate_limit("groq", 0)
        third = registry.penalise_rate_limit("groq", 0)

        assert first == 2.0
        assert second == 4.0
        assert third == 8.0

    def test_the_backoff_is_capped(self):
        registry = CooldownRegistry(base_delay=2.0, max_delay=5.0, jitter=0.0)

        for _ in range(10):
            delay = registry.penalise_rate_limit("groq", 0)

        assert delay == 5.0, "an uncapped backoff eventually stops being a retry"

    def test_jitter_keeps_the_delay_within_half_the_computed_value(self):
        """Full jitter over [delay/2, delay], never [0, delay].

        A retry landing at zero is no backoff at all, so the lower bound matters
        as much as the spread.

        A fresh registry per draw, because the backoff *doubles* per consecutive
        strike: measuring 50 draws against one registry asserts a constant that
        was never constant, and the test would fail for the right reason at the
        wrong line.
        """
        delays = [
            CooldownRegistry(base_delay=10.0, max_delay=100.0, jitter=1.0)
            .penalise_rate_limit("groq", 0)
            for _ in range(50)
        ]

        assert all(5.0 <= d <= 10.0 for d in delays), f"outside the band: {delays}"
        assert len(set(delays)) > 1, (
            "identical delays put every concurrent workflow on the same retry "
            "instant, which is the thundering herd that caused the limit"
        )

    def test_retry_after_raises_the_delay_when_the_provider_names_one(self):
        registry = CooldownRegistry(base_delay=2.0, max_delay=100.0, jitter=0.0)

        delay = registry.penalise_rate_limit("groq", 0, retry_after=30.0)

        assert delay == 30.0

    def test_retry_after_cannot_exceed_the_ceiling(self):
        registry = CooldownRegistry(base_delay=2.0, max_delay=10.0, jitter=0.0)

        delay = registry.penalise_rate_limit("groq", 0, retry_after=3600.0)

        assert delay == 10.0, (
            "an unbounded provider-supplied wait would park the whole chain for "
            "an hour on one header"
        )

    def test_a_success_clears_the_streak_so_backoff_never_ratchets(self):
        registry = CooldownRegistry(base_delay=2.0, max_delay=100.0, jitter=0.0)

        registry.penalise_rate_limit("groq", 0)
        registry.penalise_rate_limit("groq", 0)
        registry.succeed("groq", 0)

        assert registry.penalise_rate_limit("groq", 0) == 2.0, (
            "a key that recovered must start from the base delay again, not from "
            "wherever the last outage left it"
        )

    def test_cooldown_is_per_key(self):
        registry = CooldownRegistry(base_delay=60.0, max_delay=100.0, jitter=0.0)

        registry.penalise_rate_limit("groq", 0)

        assert registry.available_for("groq", 0) is False
        assert registry.available_for("groq", 1) is True, (
            "one exhausted key must not put the provider's other keys to sleep"
        )


# ── the circuit breaker, for failures a backoff cannot fix ────────────────


class TestCircuitBreaker:
    def test_it_opens_after_repeated_non_rate_limit_failures(self):
        registry = CooldownRegistry(
            failure_threshold=3, failure_cooldown=300.0, base_delay=0.0, jitter=0.0
        )

        assert registry.penalise_failure("nvidia", 0) is False
        assert registry.penalise_failure("nvidia", 0) is False
        assert registry.penalise_failure("nvidia", 0) is True
        assert registry.available_for("nvidia", 0) is False

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_provider_that_only_times_out_is_taken_out_of_rotation(self):
        """nvidia, live.

        Probed at a 20s and a 120s timeout and timed out on both, while sitting
        mid-chain -- so every draft paid a full timeout for a provider that was
        never going to answer. A timeout is not a 429, so the rate-limit backoff
        never applied to it; this is the mechanism that does.
        """
        respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=httpx.TimeoutException("too slow")
        )
        settings = _settings(
            llm_groq_api_keys="k0",
            llm_provider_failure_threshold=2,
            llm_provider_failure_cooldown_seconds=600.0,
        )
        from ai.llm.base import LLMUnavailableError

        # The first two calls fail on the wire -- which is the point: a provider
        # that is down still raises, because "it is unreachable" and "nothing was
        # attempted" must not look the same to the caller.
        for run_id in (1, 2):
            with pytest.raises(LLMUnavailableError):
                await call_llm(
                    "prompt", agent="draft", workflow_run_id=run_id, settings=settings
                )

        # Third call must not touch the wire at all.
        with respx.mock:
            respx.post(url__regex=r".*chat/completions.*").mock(
                side_effect=AssertionError("the breaker did not hold")
            )
            with pytest.raises(LLMUnavailableError):
                await call_llm(
                    "three", agent="draft", workflow_run_id=3, settings=settings
                )

    def test_a_rate_limit_does_not_trip_the_failure_breaker(self):
        """They are different faults and must not share a counter.

        A 429 is a quota statement; a timeout is a broken endpoint. Counting them
        together means a burst of rate limiting on a provider that is otherwise
        healthy eventually takes a working provider out of rotation.
        """
        registry = CooldownRegistry(
            failure_threshold=2, base_delay=1.0, jitter=0.0, max_delay=1.0
        )

        for _ in range(5):
            registry.penalise_rate_limit("groq", 0)

        assert registry.snapshot()[0]["consecutive_failures"] == 0


# ── Gemini tool calling ───────────────────────────────────────────────────


def _gemini_tool_response(*parts: dict, text: str | None = None) -> httpx.Response:
    """A Gemini response body from raw parts.

    `text` is keyword-only, deliberately. As a leading positional it swallowed
    the first part: `_gemini_tool_response({"functionCall": ...})` produced
    `parts: [{"text": {"functionCall": ...}}]`, the provider correctly found no
    `functionCall`, and the test failed on an empty tool-call list while looking
    like a provider bug. `parts` is variadic for the same reason -- a mapping
    passed as `**kwargs` would have been iterated as *keys*.
    """
    body_parts: list[dict] = []
    if text is not None:
        body_parts.append({"text": text})
    body_parts.extend(parts)
    return httpx.Response(
        200, json={"candidates": [{"content": {"parts": body_parts}}]}
    )


def _call_part(name: str = "get_weather", call_id: str = "c1", signature: str | None = None) -> dict:
    part: dict = {
        "functionCall": {
            "name": name,
            "args": {"city": "Karachi"},
            "id": call_id,
        }
    }
    if signature is not None:
        # Beside `functionCall`, not inside it -- confirmed by dumping a live
        # response, because the obvious placement is the one that is wrong.
        part["thoughtSignature"] = signature
    return part


TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "get_weather",
            "description": "Get the weather for a city.",
            "parameters": {
                "type": "object",
                "properties": {"city": {"type": "string"}},
                "required": ["city"],
            },
        },
    }
]


class TestGeminiToolCalling:
    @respx.mock
    @pytest.mark.asyncio
    async def test_it_emits_tool_calls_rather_than_declining_to_participate(self):
        """Without this method the chain skipped Gemini entirely.

        The chain records "does not support native tool calling" and moves on, so
        the one provider with ten keys and a generous free tier never took a turn.
        Verified live: Gemini answers a `functionDeclarations` request with a
        `functionCall` part.
        """
        respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(
                {"functionCall": {"name": "get_weather", "args": {"city": "Karachi"}, "id": "c1"}}
            )
        )

        turn = await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete_with_tools(
            [{"role": "user", "content": "weather?"}], TOOLS
        )

        assert [c.name for c in turn.tool_calls] == ["get_weather"]
        assert turn.tool_calls[0].arguments == {"city": "Karachi"}

    @respx.mock
    @pytest.mark.asyncio
    async def test_tool_schemas_are_declared_in_geminis_shape(self):
        route = respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(text="done")
        )

        await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete_with_tools(
            [{"role": "user", "content": "hi"}], TOOLS
        )

        sent = route.calls[0].request
        import json as _json

        body = _json.loads(sent.content)
        decls = body["tools"][0]["functionDeclarations"]
        assert decls[0]["name"] == "get_weather"
        assert decls[0]["parameters"]["properties"]["city"]["type"] == "string"
        assert decls[0]["parameters"]["required"] == ["city"]

    @respx.mock
    @pytest.mark.asyncio
    async def test_the_system_prompt_leaves_the_conversation(self):
        """Gemini carries the system instruction outside `contents`.

        Sending it as an ordinary first turn would make it a `user` message, and
        the model would treat its own instructions as something the user said.
        """
        route = respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(text="ok")
        )

        await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete_with_tools(
            [
                {"role": "system", "content": "You are a test harness."},
                {"role": "user", "content": "hi"},
            ],
            TOOLS,
        )

        import json as _json

        body = _json.loads(route.calls[0].request.content)
        assert "systemInstruction" in body
        assert body["systemInstruction"]["parts"][0]["text"] == "You are a test harness."
        assert all(part.get("text") != "You are a test harness." for c in body["contents"] for part in c["parts"])

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_tool_result_becomes_a_function_response_on_a_user_turn(self):
        """Not a `role: tool` message -- Gemini has no such role.

        Getting this wrong means the model never sees the result of the tool it
        asked for, and the loop can never converge.
        """
        route = respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(text="it is 31C")
        )

        await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete_with_tools(
            [
                {"role": "user", "content": "weather?"},
                {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                        {
                            "id": "c1",
                            "type": "function",
                            "function": {
                                "name": "get_weather",
                                "arguments": '{"city": "Karachi"}',
                            },
                        }
                    ],
                },
                {"role": "tool", "tool_call_id": "c1", "content": '{"temp_c": 31}'},
            ],
            TOOLS,
        )

        import json as _json

        body = _json.loads(route.calls[0].request.content)
        roles = [c["role"] for c in body["contents"]]
        assert roles == ["user", "model", "user"], f"unexpected turn roles: {roles}"

        responses = [
            part["functionResponse"]
            for c in body["contents"]
            for part in c["parts"]
            if "functionResponse" in part
        ]
        assert responses, f"the tool result was dropped entirely: {body['contents']}"
        assert responses[0]["name"] == "get_weather", (
            "Gemini pairs a result to a request by NAME, and the runtime's "
            f"`tool_call_id` is not it. Got {responses[0]['name']!r}."
        )
        assert responses[0]["response"] == {"temp_c": 31}

    @respx.mock
    @pytest.mark.asyncio
    async def test_the_thought_signature_is_carried_on_the_tool_call(self):
        """The requirement that makes multi-turn possible at all.

        Gemini rejects a follow-up whose replayed `functionCall` lacks the
        `thoughtSignature` the model returned beside it. Live error, verbatim:
        "Function call is missing a thought_signature in functionCall parts. This
        is required for tools to work correctly." It sits *beside* `functionCall`
        on the same part, which is not where you would put it.
        """
        respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(_call_part(signature="SIG-abc"))
        )

        turn = await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete_with_tools(
            [{"role": "user", "content": "weather?"}], TOOLS
        )

        assert turn.tool_calls[0].provider_metadata.get("thought_signature") == "SIG-abc"

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_follow_up_turn_replays_the_signature(self):
        """End to end: what the loop stores is what the next request must send."""
        route = respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(_call_part(signature="SIG-xyz"))
        )
        provider = GeminiProvider(["gkey"], model="gemini-x", timeout=5.0)

        turn = await provider.complete_with_tools(
            [{"role": "user", "content": "weather?"}], TOOLS
        )
        call = turn.tool_calls[0]

        # Exactly what `run_tool_loop` now assembles.
        from ai.llm.providers import TOOL_METADATA_KEY

        messages = [
            {"role": "user", "content": "weather?"},
            {
                "role": "assistant",
                "content": "",
                "tool_calls": [
                    {
                        "id": call.id,
                        "type": "function",
                        "function": {
                            "name": call.name,
                            "arguments": '{"city": "Karachi"}',
                        },
                    }
                ],
                TOOL_METADATA_KEY: {call.id: call.provider_metadata},
            },
            {"role": "tool", "tool_call_id": call.id, "content": '{"temp_c": 31}'},
        ]

        await provider.complete_with_tools(messages, TOOLS)

        import json as _json

        body = _json.loads(route.calls[1].request.content)
        model_turn = next(c for c in body["contents"] if c["role"] == "model")
        assert model_turn["parts"][0].get("thoughtSignature") == "SIG-xyz", (
            "the signature was lost, so the next turn would be rejected with a "
            f"400: {model_turn}"
        )

    @respx.mock
    @pytest.mark.asyncio
    async def test_the_key_index_reaches_the_audit_trail(self):
        """Ten keys configured, and every response claimed the last one."""
        respx.post(url__regex=r".*generateContent.*").mock(
            return_value=httpx.Response(
                200, json={"candidates": [{"content": {"parts": [{"text": "hi"}]}}]}
            )
        )
        provider = GeminiProvider(["k0", "k1", "k2"], model="gemini-x", timeout=5.0)

        result = await provider.complete("p", key_index=0)

        assert result.key_index == 0, (
            f"the audit trail named key {result.key_index}; key 0 was sent. "
            "With ten keys this is wrong nine times in ten."
        )


class TestTheLoopCarriesProviderMetadata:
    """The link between the provider and the next turn, exercised for real.

    The Gemini tests above build the follow-up request by hand, which proves the
    adapter is correct *given* the right messages. It does not prove anything
    produces them -- and the loop is what does.

    This gap was found by mutation: dropping the carry in `run_tool_loop` left
    every other test in this file green, because none of them went through the
    loop. A test that only checks the adapter's own output is a test of the
    adapter.
    """

    @pytest.mark.asyncio
    async def test_a_tool_calls_metadata_reaches_the_next_turn(self):
        from ai.agents.tool_loop import run_tool_loop
        from ai.llm.providers import TOOL_METADATA_KEY

        seen: list[list[dict]] = []

        async def ask(messages, tools):
            seen.append([dict(m) for m in messages])
            if len(seen) == 1:
                return ToolCallResponse(
                    text="",
                    tool_calls=(
                        ToolCall(
                            name="get_weather",
                            arguments={"city": "Karachi"},
                            id="c1",
                            provider_metadata={"thought_signature": "SIG-abc"},
                        ),
                    ),
                    provider="gemini",
                )
            return ToolCallResponse(text="31C", provider="gemini")

        async def execute(name, arguments, run_id):
            return '{"temp_c": 31}'

        await run_tool_loop(
            ask=ask,
            system="sys",
            objective="obj",
            tools=TOOLS,
            execute=execute,
            workflow_run_id=1,
        )

        assert len(seen) == 2, f"expected two turns, got {len(seen)}"

        assistant = next(m for m in seen[1] if m.get("role") == "assistant")
        metadata = assistant.get(TOOL_METADATA_KEY)
        assert metadata, (
            "the loop dropped the model's provider metadata, so Gemini's "
            f"thoughtSignature never reaches the follow-up request: {assistant}"
        )
        assert metadata["c1"]["thought_signature"] == "SIG-abc"

    @pytest.mark.asyncio
    async def test_a_tool_call_without_metadata_adds_no_empty_key(self):
        """OpenAI-shaped providers must not grow a spurious field.

        The key is omitted entirely rather than set to `{}`, so a provider with no
        state to carry sees exactly the request shape it saw before.
        """
        from ai.agents.tool_loop import run_tool_loop
        from ai.llm.providers import TOOL_METADATA_KEY

        seen: list[list[dict]] = []

        async def ask(messages, tools):
            seen.append([dict(m) for m in messages])
            if len(seen) == 1:
                return ToolCallResponse(
                    text="",
                    tool_calls=(
                        ToolCall(name="get_weather", arguments={}, id="c1"),
                    ),
                    provider="groq",
                )
            return ToolCallResponse(text="done", provider="groq")

        async def execute(name, arguments, run_id):
            return "{}"

        await run_tool_loop(
            ask=ask, system="sys", objective="obj", tools=TOOLS,
            execute=execute, workflow_run_id=1,
        )

        assistant = next(m for m in seen[1] if m.get("role") == "assistant")
        assert TOOL_METADATA_KEY not in assistant, (
            "an empty metadata key is sent to providers that have no use for it"
        )


class TestRejectedKeyDoesNotAbandonTheProvider:
    """Both halves of this were found by running the real agent, not by reading it."""

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_403_on_one_key_still_tries_the_others(self):
        """Ten keys, and a 403 on the second one ended the provider.

        Reproduces the live run: Gemini served five tool turns on key 0, answered
        503, the chain moved to key 1, got a 403 -- and stopped. Keys 2 through 9
        were never attempted, so a run with ten healthy keys fell through to a
        provider with one.
        """
        settings = _settings(
            llm_groq_api_keys="k0,k1,k2",
            llm_provider_failure_threshold=99,  # isolate the 403 behaviour
        )
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[
                httpx.Response(429, json={}),
                httpx.Response(403, json={"error": "revoked"}),
                _ok("third key survived"),
            ]
        )

        result = await call_llm(
            "prompt", agent="draft", workflow_run_id=1, settings=settings
        )

        assert result.text == "third key survived"
        sent = [c.request.headers["Authorization"] for c in route.calls]
        assert sent == ["Bearer k0", "Bearer k1", "Bearer k2"], (
            f"a 403 on one key ended the provider; keys used were {sent}"
        )

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_malformed_request_still_stops_the_provider_immediately(self):
        """The other half, and the reason the split is safe.

        A 400 is a fact about the *request*: retrying the same payload with the
        other nine keys produces the same 400 nine times. This is what
        non-retryable is actually for.
        """
        settings = _settings(
            llm_groq_api_keys="k0,k1,k2",
            llm_provider_failure_threshold=99,
        )
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            return_value=httpx.Response(400, json={"error": "model retired"})
        )

        from ai.llm.base import LLMUnavailableError

        with pytest.raises(LLMUnavailableError):
            await call_llm("prompt", agent="draft", workflow_run_id=1, settings=settings)

        assert route.call_count == 1, (
            "a malformed request was retried against every key: "
            f"{route.call_count} calls"
        )


class TestInternalFieldsNeverReachTheWire:
    """A bug this work introduced, caught by running it.

    The tool loop parks Gemini's `thoughtSignature` on the assistant message under
    an underscore-prefixed key. Those messages are protocol-shaped and get handed
    to every provider, so the field went out to groq and cerebras too -- which
    answer 400 for an unknown property on a message object. OpenRouter ignored
    it, which made the failure look provider-specific and intermittent rather than
    structural.

    It surfaced as: a draft Gemini drove for five turns, a 503, and then a
    follow-up that both fallbacks rejected with a 400 no amount of rotation could
    get past.
    """

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_gemini_turn_can_be_replayed_to_an_openai_provider(self):
        """The exact handover that broke: Gemini history, groq fallback."""
        from ai.llm.providers import TOOL_METADATA_KEY

        settings = _settings(llm_groq_api_keys="k0")
        route = respx.post(url__regex=r".*chat/completions.*").mock(return_value=_tools_turn("ok"))

        await call_llm_with_tools(
            [
                {"role": "user", "content": "weather?"},
                {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                        {
                            "id": "c1",
                            "type": "function",
                            "function": {"name": "get_weather", "arguments": "{}"},
                        }
                    ],
                    TOOL_METADATA_KEY: {"c1": {"thought_signature": "SIG"}},
                },
                {"role": "tool", "tool_call_id": "c1", "content": "{}"},
            ],
            TOOLS,
            agent="draft",
            workflow_run_id=1,
            settings=settings,
        )

        import json as _json

        sent = _json.loads(route.calls[0].request.content)
        for message in sent["messages"]:
            assert not any(str(k).startswith("_") for k in message), (
                f"an internal field reached the provider: {message}"
            )

    @respx.mock
    @pytest.mark.asyncio
    async def test_gemini_still_receives_its_own_metadata(self):
        """Stripping is the OpenAI path's job, not a global sanitiser.

        Gemini needs the signature -- it is the only reason its multi-turn works.
        """
        from ai.llm.providers import TOOL_METADATA_KEY

        respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(_call_part(call_id="c1", signature="SIG"))
        )
        provider = GeminiProvider(["gkey"], model="gemini-x", timeout=5.0)
        call = ToolCall(
            name="get_weather", arguments={}, id="c1",
            provider_metadata={"thought_signature": "SIG"},
        )

        await provider.complete_with_tools(
            [
                {"role": "user", "content": "hi"},
                {
                    "role": "assistant",
                    "content": "",
                    "tool_calls": [
                        {"id": "c1", "type": "function",
                         "function": {"name": "get_weather", "arguments": "{}"}}
                    ],
                    TOOL_METADATA_KEY: {"c1": call.provider_metadata},
                },
                {"role": "tool", "tool_call_id": "c1", "content": "{}"},
            ],
            TOOLS,
        )

        import json as _json

        body = _json.loads(respx.calls[0].request.content)
        model_turn = next(c for c in body["contents"] if c["role"] == "model")
        assert model_turn["parts"][0]["thoughtSignature"] == "SIG"


class TestBusyIsNotGone:
    """A distinction I got wrong first, and live runs showed it.

    503 was being counted as a hard failure, so three of them opened a 300-second
    circuit breaker on the provider. On Gemini -- which has ten keys -- that means
    ordinary overload would have taken out the entire account. The breaker is for
    an endpoint that is not answering (nvidia, which timed out at 20s and 120s);
    a 503 says the opposite.
    """

    def test_a_transient_failure_naps_and_does_not_open_the_breaker(self):
        registry = CooldownRegistry(
            base_delay=1.0, max_delay=4.0, jitter=0.0, failure_threshold=2
        )

        for _ in range(6):
            registry.penalise("gemini", 0, transient=True)

        assert registry.available_for("gemini", 0) is False
        assert registry.snapshot()[0]["cooldown_remaining_seconds"] <= 4.0, (
            "an overloaded provider was benched for the breaker's full duration"
        )
        assert registry.snapshot()[0]["consecutive_failures"] == 0, (
            "transient failures must not count toward the hard-failure breaker"
        )

    def test_a_hard_failure_still_opens_the_breaker(self):
        registry = CooldownRegistry(
            base_delay=1.0, max_delay=4.0, jitter=0.0,
            failure_threshold=2, failure_cooldown=300.0,
        )

        registry.penalise("nvidia", 0, transient=False)
        registry.penalise("nvidia", 0, transient=False)

        assert registry.available_for("nvidia", 0) is False
        assert registry.snapshot()[0]["cooldown_remaining_seconds"] == 300.0

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_503_does_not_bench_a_healthy_provider(self):
        # Two keys, so the claim is actually testable. With one key and a 60s nap
        # the chain skipping it on the next call is *correct*, and asserting
        # otherwise would be asserting a bug.
        settings = _settings(
            llm_groq_api_keys="k0,k1",
            llm_provider_failure_threshold=2,
            llm_provider_failure_cooldown_seconds=600.0,
        )
        route = respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=[
                httpx.Response(503, json={}),
                _ok("recovered on the second key"),
                _ok("still serving"),
            ]
        )

        result = await call_llm("one", agent="draft", workflow_run_id=1, settings=settings)

        assert result.text == "recovered on the second key"
        sent = [c.request.headers["Authorization"] for c in route.calls]
        assert sent == ["Bearer k0", "Bearer k1"], (
            f"the chain did not move to the next key after a 503: {sent}"
        )

        # And the breaker is not open: a second call still gets through rather
        # than finding both keys benched for 600 seconds.
        again = await call_llm("two", agent="draft", workflow_run_id=2, settings=settings)
        assert again.text == "still serving", (
            "repeated 503s opened a 600s breaker on a provider that was merely "
            "busy; live, that would have taken out all ten Gemini keys"
        )


class TestProvidersReportBusyVersusGone:
    """The flag itself, not just what the chain does with it.

    Found by mutation: reverting `transient=response.status_code >= 500` left the
    whole suite green, because every other test exercised the flag only through
    the chain's behaviour -- and the chain's behaviour was then mutated
    separately. Two mutations, one guard between them, and neither alone caught
    the seam.
    """

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_5xx_is_transient(self):
        respx.post(url__regex=r".*chat/completions.*").mock(
            return_value=httpx.Response(503, json={})
        )

        with pytest.raises(Exception) as exc:
            await OpenAICompatibleProvider(
                name="groq", keys=["k0"], base_url="https://x/v1", model="m", timeout=5.0
            ).complete("p")

        assert getattr(exc.value, "transient", None) is True, (
            "a 503 is 'busy' and must not be treated as a dead endpoint"
        )

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_4xx_is_not_transient(self):
        """A bad request is neither busy nor gone -- retrying it is pointless."""
        respx.post(url__regex=r".*chat/completions.*").mock(
            return_value=httpx.Response(400, json={})
        )

        with pytest.raises(Exception) as exc:
            await OpenAICompatibleProvider(
                name="groq", keys=["k0"], base_url="https://x/v1", model="m", timeout=5.0
            ).complete("p")

        assert getattr(exc.value, "transient", None) is False

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_timeout_is_not_transient(self):
        """The nvidia case: not answering at all, so the breaker is correct."""
        respx.post(url__regex=r".*chat/completions.*").mock(
            side_effect=httpx.TimeoutException("too slow")
        )

        with pytest.raises(Exception) as exc:
            await OpenAICompatibleProvider(
                name="nvidia", keys=["k0"], base_url="https://x/v1", model="m", timeout=5.0
            ).complete("p")

        assert getattr(exc.value, "transient", None) is False, (
            "a timeout must open the breaker; treating it as a nap would re-probe "
            "a dead endpoint on every request, which is what nvidia was doing"
        )


class TestKeysStayOutOfUrls:
    """Ten live keys were being written to stdout on every Gemini call.

    `httpx` logs the request URL at INFO and uvicorn turns httpx's logger up, so
    the `?key=` form printed a real credential on every single call. That is
    visible in the transcript of the run that found it.
    """

    @respx.mock
    @pytest.mark.asyncio
    async def test_the_gemini_key_is_a_header_not_a_query_parameter(self):
        route = respx.post(url__regex=r".*generateContent.*").mock(
            return_value=httpx.Response(
                200, json={"candidates": [{"content": {"parts": [{"text": "hi"}]}}]}
            )
        )

        await GeminiProvider(["SUPERSECRETKEY"], model="gemini-x", timeout=5.0).complete("p")

        url = str(route.calls[0].request.url)
        assert "SUPERSECRETKEY" not in url, (
            f"the key is in the URL and will be logged: {url}"
        )
        assert route.calls[0].request.headers["x-goog-api-key"] == "SUPERSECRETKEY"

    @respx.mock
    @pytest.mark.asyncio
    async def test_the_tool_calling_path_keeps_the_key_out_of_the_url_too(self):
        """Both Gemini methods build their own URL, so both have to be checked.

        Fixing only the prose path would have left the tool-calling path -- the
        busier of the two -- leaking.
        """
        route = respx.post(url__regex=r".*generateContent.*").mock(
            return_value=_gemini_tool_response(text="ok")
        )

        await GeminiProvider(["SUPERSECRETKEY"], model="gemini-x", timeout=5.0).complete_with_tools(
            [{"role": "user", "content": "hi"}], TOOLS
        )

        url = str(route.calls[0].request.url)
        assert "SUPERSECRETKEY" not in url, f"the key is in the URL: {url}"
        assert route.calls[0].request.headers["x-goog-api-key"] == "SUPERSECRETKEY"


class TestRetryAfterIsSurfaced:
    @respx.mock
    @pytest.mark.asyncio
    async def test_a_retry_after_header_reaches_the_error(self):
        """So the chain can respect a provider that tells us when to come back.

        Mistral sends none -- three consecutive 429s with no header, verified
        live -- so this can only ever be a refinement, never the mechanism.
        """
        respx.post(url__regex=r".*chat/completions.*").mock(
            return_value=httpx.Response(429, headers={"Retry-After": "7"}, json={})
        )

        with pytest.raises(RateLimitError) as exc:
            await OpenAICompatibleProvider(
                name="groq", keys=["k0"], base_url="https://x/v1", model="m", timeout=5.0
            ).complete("p")

        assert exc.value.retry_after == 7.0

    @respx.mock
    @pytest.mark.asyncio
    async def test_a_provider_that_sends_none_is_still_retryable(self):
        respx.post(url__regex=r".*chat/completions.*").mock(
            return_value=httpx.Response(429, json={})
        )

        with pytest.raises(RateLimitError) as exc:
            await OpenAICompatibleProvider(
                name="mistral", keys=["k0"], base_url="https://x/v1", model="m", timeout=5.0
            ).complete("p")

        assert exc.value.retry_after is None
        assert exc.value.retryable is True


class TestProviderUniformity:
    @pytest.mark.asyncio
    async def test_every_provider_accepts_a_key_index(self):
        """The chain now passes one unconditionally.

        It catches broad exceptions, so a provider whose signature lacked the
        parameter would have failed with a `TypeError` and quietly taken itself
        out of the chain -- rotation disabled everywhere by one provider that has
        no keys at all.
        """
        from ai.llm.providers import FixtureProvider

        for provider in (
            OpenAICompatibleProvider(
                name="groq", keys=["k0"], base_url="https://x/v1", model="m"
            ),
            GeminiProvider(["k0"]),
            FixtureProvider(),
        ):
            assert "key_index" in provider.complete.__code__.co_varnames or any(
                "key_index" in str(v) for v in provider.complete.__defaults__ or ()
            ), f"{type(provider).__name__}.complete cannot accept key_index"

    def test_the_tool_calling_path_exists_on_the_providers_that_can_do_it(self):
        """Named explicitly because the failure was a missing method.

        The chain's response to a provider without `complete_with_tools` is to
        record "does not support native tool calling" and move on -- correct
        behaviour, and exactly why Gemini was never used.
        """
        for provider in (
            OpenAICompatibleProvider(
                name="groq", keys=["k0"], base_url="https://x/v1", model="m"
            ),
            GeminiProvider(["k0"]),
        ):
            assert callable(getattr(provider, "complete_with_tools", None)), (
                f"{type(provider).__name__} would be skipped by the tool loop"
            )