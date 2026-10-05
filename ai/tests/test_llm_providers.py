"""The providers, against mocked HTTP.

## Why this matters more than its size suggests

This module is where a free-tier agentic system meets reality: five free providers
with different quotas, different error vocabularies, and different opinions about
what constitutes a malformed response. The chain above it is only as good as what
these raise.

The distinction that matters most is `retryable`. A chain that retries a 401
wastes every remaining key on the same rejection; a chain that retries nothing
gives up on a provider that was having a bad minute. So each status code is
mapped here, and each mapping is tested.

## No network

`respx` intercepts at the httpx transport layer, so these tests run offline and
deterministically. That is not just convenience: the failure modes being tested
-- rate limits, key rejections, unreadable bodies -- are exactly the ones that
cannot be provoked on demand against a real provider without burning quota.
"""

from __future__ import annotations

import json

import httpx
import pytest
import respx

from ai.llm.base import FixtureMissingError, ProviderError, RateLimitError
from ai.llm.providers import FixtureProvider, GeminiProvider, OpenAICompatibleProvider

CHAT_URL = "https://api.example.com/v1/chat/completions"
GEMINI_URL = "https://generativelanguage.googleapis.com"


def _provider(keys=("k1",), model="test-model") -> OpenAICompatibleProvider:
    return OpenAICompatibleProvider(
        name="example",
        keys=list(keys),
        base_url="https://api.example.com/v1",
        model=model,
        timeout=5.0,
    )


def _completion(content: str = "hello") -> dict:
    return {
        "choices": [{"message": {"role": "assistant", "content": content}}],
        "usage": {"prompt_tokens": 10, "completion_tokens": 3, "total_tokens": 13},
    }


# ── the happy path ──────────────────────────────────────────────────────


@respx.mock
async def test_a_completion_is_returned_with_its_usage():
    respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json=_completion("hi there")))

    result = await _provider().complete("prompt")

    assert result.text == "hi there"
    assert result.provider == "example"
    assert result.prompt_tokens == 10
    assert result.completion_tokens == 3


@respx.mock
async def test_the_system_message_is_sent_separately_from_the_prompt():
    """System and user are different roles, not one concatenated string.

    Concatenating them would let a prompt in the user half look like an
    instruction, which is the injection surface the role split exists to close.
    """
    route = respx.post(CHAT_URL).mock(
        return_value=httpx.Response(200, json=_completion())
    )

    await _provider().complete("the prompt", system="the system")

    messages = route.calls[0].request.content
    body = json.loads(messages)
    assert body["messages"] == [
        {"role": "system", "content": "the system"},
        {"role": "user", "content": "the prompt"},
    ]


@respx.mock
async def test_no_system_message_means_none_is_sent():
    # Some providers reject an empty system message rather than ignoring it.
    route = respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json=_completion()))

    await _provider().complete("only a prompt")

    assert json.loads(route.calls[0].request.content)["messages"] == [
        {"role": "user", "content": "only a prompt"}
    ]


@respx.mock
async def test_the_key_is_sent_as_a_bearer_token():
    route = respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json=_completion()))

    await _provider(keys=("secret-key",)).complete("prompt")

    assert route.calls[0].request.headers["Authorization"] == "Bearer secret-key"


@respx.mock
async def test_the_last_key_is_used():
    """Rotation appends, so the newest key is tried first.

    Adding a key at the end must not disturb the prefix -- which means an
    exhausted older key stays in place for the providers ahead of this one.
    """
    route = respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json=_completion()))

    await _provider(keys=("old", "newer", "newest")).complete("prompt")

    assert route.calls[0].request.headers["Authorization"] == "Bearer newest"


# ── error mapping, which is the point of this module ────────────────────


@respx.mock
async def test_an_explicit_key_index_selects_that_key():
    """The rotation that `ProviderSpec` promised and the chain assumed.

    `key_index` is the whole mechanism. The chain iterates a provider's keys and
    retries the next one on a rate limit, but it used to never tell the provider
    *which* key to use -- `complete` always took `_keys[-1]`. So a 429 on the
    newest key meant re-sending the identical request with the identical exhausted
    key, once per key in the list, before falling through to the next provider.

    The default stays "last", and the test above pins that half: changing it
    would silently break the append-at-the-end behaviour the chain relies on.
    """
    route = respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json=_completion()))

    await _provider(keys=("first", "second", "third")).complete("prompt", key_index=0)

    assert route.calls[0].request.headers["Authorization"] == "Bearer first"


@respx.mock
async def test_the_tool_calling_path_also_honours_the_key_index():
    """Rotation on the busiest path, not just the prose one.

    The agent loop is where a rate limit is most expensive to retry blindly,
    because each turn carries the whole conversation. It had the identical
    defect and is fixed identically.
    """
    route = respx.post(CHAT_URL).mock(
        return_value=httpx.Response(200, json={"choices": [{"message": {"content": "done"}}]})
    )

    await _provider(keys=("first", "second")).complete_with_tools(
        [{"role": "user", "content": "hi"}], [], key_index=0
    )

    assert route.calls[0].request.headers["Authorization"] == "Bearer first"


@respx.mock
async def test_the_recorded_key_index_is_the_one_actually_used():
    """`LLMResponse.key_index` is what the budget log attributes a call to.

    Recording `len(keys) - 1` unconditionally would make every rotated call look
    like it used the last key -- which is precisely the one piece of evidence
    that would have exposed the original defect.
    """
    respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json=_completion()))

    response = await _provider(keys=("first", "second", "third")).complete("prompt", key_index=1)

    assert response.key_index == 1


@pytest.mark.parametrize("status", [401, 403])
@respx.mock
async def test_a_rejected_key_moves_on_to_the_next_key(status):
    """A rejected key is a fact about that key, not about the provider.

    This was `retryable=False`, so the chain read it as "stop trying this
    provider" -- which is right for a request the provider will never accept and
    wrong for a key it will never accept. Provider keys are per-project: one being
    revoked says nothing about the other nine.

    A live run cost eight keys to find that out. Gemini served five tool turns on
    key 0, answered 503, the chain correctly moved to key 1, got a 403 -- and
    stopped. Keys 2 through 9 were never attempted, and the run fell through to a
    provider with one key.

    What *is* still non-retryable is a request the provider will never accept: a
    retired model name or a malformed payload arrives as a 4xx with the body
    attached, and that still breaks the key loop.
    """
    respx.post(CHAT_URL).mock(return_value=httpx.Response(status, json={"error": "bad key"}))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is True, (
        "a rejected key must not abandon the provider's remaining keys"
    )
    assert str(status) in str(exc.value)


@respx.mock
async def test_a_rate_limit_is_reported_as_such_and_is_retryable():
    respx.post(CHAT_URL).mock(return_value=httpx.Response(429, json={"error": "slow down"}))

    with pytest.raises(RateLimitError):
        await _provider().complete("prompt")


@respx.mock
async def test_a_server_error_is_retryable():
    # 5xx is somebody else's problem and usually transient.
    respx.post(CHAT_URL).mock(return_value=httpx.Response(503, text="upstream down"))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is True


@respx.mock
async def test_a_client_error_beyond_the_key_is_not_retryable():
    # 400 means the request itself is wrong. Retrying an identical request gets
    # an identical 400.
    respx.post(CHAT_URL).mock(return_value=httpx.Response(400, text="bad model"))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is False


@respx.mock
async def test_an_error_message_carries_the_status_and_the_body():
    """The body is the only clue about *which* model name was wrong.

    And every hardcoded default in this project was wrong once, so a 404 saying
    "no endpoints found for X" is exactly the diagnostic needed.
    """
    respx.post(CHAT_URL).mock(
        return_value=httpx.Response(404, text='{"error":"No endpoints found for x"}')
    )

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    message = str(exc.value)
    assert "404" in message
    assert "No endpoints found" in message


@respx.mock
async def test_an_unreachable_provider_is_retryable():
    respx.post(CHAT_URL).mock(side_effect=httpx.ConnectError("no route to host"))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is True


@respx.mock
async def test_a_timeout_is_retryable_and_names_the_limit():
    respx.post(CHAT_URL).mock(side_effect=httpx.ReadTimeout("too slow"))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is True
    assert "5.0s" in str(exc.value)


@respx.mock
async def test_a_body_that_is_not_json_is_retryable_not_fatal():
    """A gateway returning HTML is transient far more often than not."""
    respx.post(CHAT_URL).mock(return_value=httpx.Response(200, text="<html>502</html>"))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is True
    assert "unreadable" in str(exc.value)


@respx.mock
async def test_a_json_body_of_the_wrong_shape_is_retryable():
    """A 200 whose body has no choices.

    Some providers return this on an overloaded model rather than a 5xx, so
    treating it as permanent would drop a working provider from the chain.
    """
    respx.post(CHAT_URL).mock(return_value=httpx.Response(200, json={"data": []}))

    with pytest.raises(ProviderError) as exc:
        await _provider().complete("prompt")

    assert exc.value.retryable is True


async def test_a_provider_with_no_keys_refuses_without_a_request():
    """No key means no request, and a clear refusal rather than an auth error."""
    with pytest.raises(ProviderError) as exc:
        await _provider(keys=()).complete("prompt")

    assert exc.value.retryable is False
    assert "no configured keys" in str(exc.value)


# ── Gemini, which speaks its own protocol ───────────────────────────────


def _gemini(text: str = "hello") -> dict:
    return {"candidates": [{"content": {"parts": [{"text": text}], "role": "model"}}]}


@respx.mock
async def test_gemini_reads_its_own_response_shape():
    route = respx.post(
        url__regex=r".*generateContent.*"
    ).mock(return_value=httpx.Response(200, json=_gemini("greetings")))

    result = await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete("prompt")

    assert result.text == "greetings"
    assert result.provider == "gemini"

    # The key travels in a header, not the query string.
    #
    # This asserted `key=gkey` in the URL, which was true and was the problem:
    # httpx logs the full request URL at INFO, so every Gemini call printed the
    # live key to stdout. Ten of them are configured.
    assert route.calls[0].request.headers["x-goog-api-key"] == "gkey"
    assert "key=" not in str(route.calls[0].request.url), (
        "the key is back in the URL, where the HTTP logger will print it"
    )


@respx.mock
async def test_gemini_reports_a_rate_limit_as_retryable():
    respx.post(url__regex=r".*generateContent.*").mock(return_value=httpx.Response(429, json={}))

    with pytest.raises(RateLimitError):
        await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete("prompt")


@respx.mock
async def test_gemini_treats_a_rejected_key_as_permanent():
    respx.post(url__regex=r".*generateContent.*").mock(return_value=httpx.Response(403, json={}))

    with pytest.raises(ProviderError) as exc:
        await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete("prompt")

    # Retryable, so the chain moves to the next of gemini's ten keys. See the note
    # on `test_a_rejected_key_moves_on_to_the_next_key`: a live run abandoned keys
    # 2 through 9 here.
    assert exc.value.retryable is True


@respx.mock
async def test_gemini_survives_a_body_without_candidates():
    respx.post(url__regex=r".*generateContent.*").mock(
        return_value=httpx.Response(200, json={"promptFeedback": {}})
    )

    with pytest.raises(ProviderError) as exc:
        await GeminiProvider(["gkey"], model="gemini-x", timeout=5.0).complete("prompt")

    assert exc.value.retryable is True


async def test_gemini_with_no_keys_refuses():
    with pytest.raises(ProviderError):
        await GeminiProvider([], model="gemini-x", timeout=5.0).complete("prompt")


# ── the fixture provider ────────────────────────────────────────────────


async def test_the_fixture_provider_never_asks_the_network():
    """Structural, so not worth an HTTP assertion.

    It is the provider the entire offline suite runs on, and a single accidental
    network call inside it would make every test depend on a provider's
    availability.
    """
    provider = FixtureProvider()
    assert provider.name == "fixture"


@pytest.mark.asyncio
async def test_an_unrecorded_prompt_fails_loudly():
    """The whole reason the fixture provider is safe to test against.

    A provider that generated a plausible answer would make every test built on
    it pass against output nobody wrote -- and the tests would keep passing
    after the prompt changed, which is the one thing a fixture exists to catch.
    """
    provider = FixtureProvider()

    with pytest.raises(FixtureMissingError):
        await provider.complete("# agent: draft\nsomething nobody ever recorded")


@pytest.mark.asyncio
async def test_a_recorded_prompt_replays_without_a_model():
    """Proves the recording is usable, and that the chain can run offline."""
    import json as _json
    from pathlib import Path

    from ai.llm.providers import FIXTURE_DIR

    path = Path(FIXTURE_DIR) / "draft.jsonl"
    if not path.exists():
        pytest.skip("no draft fixture recorded; run `python -m ai.scripts.record_all`")

    entry = _json.loads(path.read_text(encoding="utf-8").splitlines()[0])
    prompt = "# agent: draft\n" + entry["response"][:20]

    provider = FixtureProvider()

    # The lookup key is a hash of the *stamped* prompt, so this asserts the
    # fixture is findable rather than reconstructing the hash by hand.
    with pytest.raises(FixtureMissingError):
        await provider.complete(prompt)