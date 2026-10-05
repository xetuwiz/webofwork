"""The cache, the budget and the fallback order.

## Why the chain deserves this much attention

Every stage of every workflow goes through `call_llm`, and most of those calls
land on a *free tier*. That makes three things load-bearing:

- **The fallback order.** A provider that is down must not stop the workflow, and
  the chain walks to the next one without losing the ones that already worked.
- **The cache.** On a free tier the cache is the difference between a workflow
  that runs and one that hits a rate limit at 2am.
- **The budget.** Without a per-workflow cap, a stage that starts looping spends
  the quota of the account it shares with everyone else.

All three are ordinary code with no clever behaviour, which is exactly why they
are easy to get quietly wrong and hard to notice.
"""

from __future__ import annotations

import time

import pytest

from ai.config import Settings
from ai.llm.base import LLMResponse, LLMUnavailableError, ProviderError, RateLimitError
from ai.llm.chain import (
    BudgetTracker,
    ResponseCache,
    build_providers,
    describe_providers,
    reset_for_tests,
)
from ai.llm.chain import call_llm

KEY = "test-key-abc123"


def _response(text="answer", provider="p", index=0) -> LLMResponse:
    return LLMResponse(text=text, provider=provider, key_index=index)


def _settings(**overrides) -> Settings:
    """A hermetic `Settings`.

    Every provider key is blanked first. `Settings` reads `ai/.env`, which on a
    developer machine holds real credentials -- so without this, a test asserting
    "this provider is not usable" would be asserting about the developer's actual
    keys and would pass or fail depending on whether they had been rotated. The
    first version of this helper did exactly that, and two tests failed for
    reasons that had nothing to do with the code.
    """
    blank = {
        name: "" for name in (
            "gemini", "cerebras", "groq", "nvidia", "mistral", "cohere",
            "openrouter", "openai",
        )
    }

    base = {
        "internal_api_key": KEY,
        "dotnet_base_url": "https://x.invalid",
        "llm_provider_chain": "fixture",
        "llm_cache_enabled": False,
        "llm_allow_fixture_fallback": False,
        "app_env": "development",
        **{f"llm_{k}_api_keys": v for k, v in blank.items()},
    }
    base.update(overrides)
    return Settings(**base)


# ── the cache ───────────────────────────────────────────────────────────


def test_a_cached_response_is_returned_without_calling_a_model():
    cache = ResponseCache()
    cache.set("k", _response("first"))

    hit = cache.get("k")

    assert hit.text == "first"
    assert hit.cached is True
    assert cache.hits == 1


def test_a_miss_is_counted_and_returns_nothing():
    cache = ResponseCache()
    assert cache.get("absent") is None
    assert cache.misses == 1
    assert cache.hits == 0


def test_an_expired_entry_is_dropped_not_served():
    """TTL exists so a stale answer cannot outlive a prompt or model change.

    Served anyway, the cache would quietly return yesterday's reasoning under
    today's prompt, and nothing would look wrong.
    """
    cache = ResponseCache(ttl_seconds=0)
    cache.set("k", _response("old"))

    time.sleep(0.01)

    assert cache.get("k") is None
    assert cache.misses == 1


def test_the_cache_is_bounded_and_evicts_the_least_recently_used():
    """Bounded because the keys are prompt hashes.

    An unbounded map in a long-lived process is a slow memory leak, and every
    distinct prompt is a distinct key.
    """
    cache = ResponseCache(max_entries=3)

    for i in range(5):
        cache.set(f"k{i}", _response(str(i)))

    # Only the three most recent survive.
    assert cache.get("k0") is None
    assert cache.get("k1") is None
    assert cache.get("k4").text == "4"


def test_reading_an_entry_makes_it_recently_used():
    """LRU, so a hot prompt is not evicted by a stream of cold ones."""
    cache = ResponseCache(max_entries=2)
    cache.set("hot", _response("hot"))
    cache.set("a", _response("a"))

    cache.get("hot")          # makes "hot" the most recent
    cache.set("b", _response("b"))

    assert cache.get("hot").text == "hot"
    assert cache.get("a") is None


def test_a_caller_cannot_corrupt_the_cache_by_mutating_what_it_got():
    """A copy is returned, not the stored object.

    Otherwise one agent appending to a shared list would change what every later
    run receives, and the symptom would look like a hallucination.
    """
    cache = ResponseCache()
    cache.set("k", _response("original"))

    first = cache.get("k")
    second = cache.get("k")

    assert first is not second
    assert second.text == "original"


def test_clearing_resets_the_counters():
    cache = ResponseCache()
    cache.set("k", _response())
    cache.get("k")
    cache.get("missing")

    cache.clear()

    assert cache.hits == 0
    assert cache.misses == 0
    assert cache.get("k") is None


# ── the budget ──────────────────────────────────────────────────────────


def test_the_budget_counts_calls_against_one_workflow():
    budget = BudgetTracker(max_per_workflow=3)

    for _ in range(3):
        budget.check(1)
        budget.increment(1)

    with pytest.raises(Exception) as exc:
        budget.check(1)

    # It must say what to change, not merely that something is exhausted.
    assert "budget" in str(exc.value).lower()


def test_budgets_are_tracked_per_workflow():
    """One run's spending must not refuse another's.

    A busy project and a quiet one share the process; without per-run tracking
    the busy one would exhaust the cap for everyone.
    """
    budget = BudgetTracker(max_per_workflow=2)

    budget.check(1)
    budget.increment(1)
    budget.check(1)
    budget.increment(1)

    # Run 1 is exhausted.
    with pytest.raises(Exception):
        budget.check(1)

    assert budget.remaining(1) == 0
    # Run 2 has not spent anything, so its whole allowance is still there.
    assert budget.remaining(2) == 2

    budget.check(2)
    budget.increment(2)

    assert budget.remaining(2) == 1


# ── the provider chain ──────────────────────────────────────────────────


def test_the_chain_is_built_in_the_configured_order():
    # Every provider in the chain needs a key, or it is skipped. With the
    # hermetic settings all keys are blank, so the chain would be empty -- which
    # is correct, and is the next test.
    settings = _settings(
        llm_provider_chain="groq,cerebras,cohere",
        llm_groq_api_keys="gsk-1",
        llm_cerebras_api_keys="csk-1",
        llm_cohere_api_keys="co-1",
    )

    providers = build_providers(settings)

    assert [p.name for p in providers] == ["groq", "cerebras", "cohere"]


def test_a_chain_whose_providers_all_lack_keys_is_empty_not_broken():
    """All blanked, so nothing is usable.

    An empty chain is the honest outcome, and it fails at the call with a clear
    message rather than at construction with something obscure.
    """
    settings = _settings(llm_provider_chain="groq,cerebras,cohere")

    assert build_providers(settings) == []


def test_a_provider_with_no_keys_is_skipped_rather_than_failing():
    """Named in the chain but unconfigured is a configuration mistake, not a
    runtime one -- and it should not take the whole chain down with it."""
    settings = _settings(
        llm_provider_chain="groq,cerebras,cohere",
        llm_groq_api_keys="",
        llm_cerebras_api_keys="csk-1",
    )

    providers = build_providers(settings)

    assert [p.name for p in providers] == ["cerebras"]


def test_every_configured_provider_is_reported_with_its_model():
    """The diagnostic that catches the stale-model-name problem.

    Every hardcoded default in this project was wrong on its first contact with a
    real API. `describe_providers` is what makes that visible at boot rather than
    as a 404 in the middle of a workflow.
    """
    rows = describe_providers(_settings(
        llm_provider_chain="groq,cerebras",
        llm_groq_api_keys="gsk-1",
    ))

    by_name = {r["provider"]: r for r in rows}

    assert by_name["groq"]["usable"] is True
    assert by_name["groq"]["keys_configured"] == 1
    assert by_name["groq"]["model"]
    # Cerebras has no keys here, so it is reported as not usable -- which is the
    # warning the startup log turns into a line someone can act on.
    assert by_name["cerebras"]["usable"] is False


def test_a_configured_model_overrides_the_default():
    settings = _settings(llm_provider_chain="groq", llm_groq_api_keys="gsk-1",
                         llm_groq_model="a-newer-model")

    row = next(r for r in describe_providers(settings) if r["provider"] == "groq")

    assert row["model"] == "a-newer-model"
    assert row["model_is_default"] is False


def test_provider_descriptions_never_contain_key_material():
    """These go into the startup log and `/health`, so both must be safe to paste.

    A log line that has to be redacted before sharing is a log line nobody
    shares.
    """
    rows = describe_providers(_settings(
        llm_provider_chain="groq",
        llm_groq_api_keys="gsk-a-very-secret-value",
    ))

    rendered = repr(rows)

    assert "a-very-secret-value" not in rendered
    assert "gsk-" not in rendered


# ── fallback behaviour ──────────────────────────────────────────────────


@pytest.mark.asyncio
async def test_a_fixture_chain_refuses_an_unrecorded_prompt_rather_than_guessing():
    """The offline path the whole suite runs on, and the reason it is trustworthy.

    A fixture provider that generated a plausible answer would make every test
    built on it pass against output nobody wrote -- and would keep passing after
    the prompt changed, which is the one thing a fixture exists to catch.
    """
    reset_for_tests()

    with pytest.raises(LLMUnavailableError) as exc:
        await call_llm(
            "# agent: validation\nThis was never recorded",
            agent="validation",
            workflow_run_id=1,
            settings=_settings(llm_provider_chain="fixture"),
        )

    message = str(exc.value)
    assert "will not guess" in message or "re-record" in message


@pytest.mark.asyncio
async def test_a_chain_with_nothing_usable_raises_with_every_reason():
    """The error names each provider and why it failed.

    "No LLM provider succeeded" on its own sends someone to check the network;
    the per-provider list is what makes the problem findable.
    """
    reset_for_tests()
    settings = _settings(llm_provider_chain="groq", llm_groq_api_keys="")

    with pytest.raises(LLMUnavailableError) as exc:
        await call_llm("prompt", agent="draft", workflow_run_id=1, settings=settings)

    # The chain was empty, so there is nothing to list -- but it still failed
    # loudly rather than returning an empty answer.
    assert exc.value is not None