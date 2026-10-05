"""The LLM chain: caching, budget, and fallback across providers.

One function, ``call_llm``, is the only way anything in this service reaches a
model. That is deliberate -- the guarantees below (never exceed the budget, never
persist a raw prompt, always try the next provider) hold for every agent because
they are enforced here rather than remembered at each call site.
"""

from __future__ import annotations

import asyncio
import hashlib
import logging
import random
import time
from collections import OrderedDict

from ..config import Settings, get_settings
from .base import (
    LLMResponse,
    LLMUnavailableError,
    ProviderError,
    ProviderSpec,
    RateLimitError,
    ToolCallResponse,
)
from .providers import FIXTURE_DIR, FixtureProvider, GeminiProvider, OpenAICompatibleProvider

#: Re-exported so the recorder does not have to know which module owns it.
__all__ = ["FIXTURE_DIR"]

log = logging.getLogger(__name__)

#: Provider name -> (default base URL, default model).
#:
#: Configurable per provider via ``LLM_<NAME>_API_KEYS``; a provider with no keys
#: is skipped silently, so a chain can be written for the providers you have and
#: extended without editing code.
#:
#: Everything here except Gemini speaks the OpenAI chat-completions protocol, so
#: they all resolve to :class:`OpenAICompatibleProvider`. That is the whole reason
#: there is one of those and nine entries in this table.
#:
#: Model choice is the one thing worth being opinionated about, and the opinion is
#: "cheap model for the bulk, expensive one for planning". Every agent here does
#: classification, extraction or summarisation except the planner, and a 1B-class
#: model does all of that adequately. A workflow that turns one user action into
#: twenty model calls is dominated by its cheapest call, not its hardest one.
#: Provider name -> (default base URL, default model).
#:
#: Configurable per provider via ``LLM_<NAME>_API_KEYS`` and
#: ``LLM_<NAME>_MODEL``; a provider with no keys is skipped silently.
#:
#: Everything here except Gemini speaks the OpenAI chat-completions protocol, so
#: they all resolve to :class:`OpenAICompatibleProvider`. That is the whole reason
#: there is one of those and nine entries in this table.
#:
#: ## These model names are a starting point, not a fact
#:
#: Every default here was verified against a live `/models` listing, and they
#: will drift -- Cerebras went from "Llama 3.1 8B and Qwen3 235B" to two models
#: in the time between writing the original plan and running this. That is not a
#: reason to hardcode less; it is a reason to make them settings.
#:
#: Model choice follows one rule: **the cheap model does the bulk**. Every agent
#: in this service does classification, extraction or summarisation except the
#: planner, and a 20-27B model handles all of that. A workflow turns one user
#: action into several model calls, so cost is dominated by the cheapest call
#: repeated many times, not by the hardest one.
PROVIDER_DEFAULTS: dict[str, tuple[str, str]] = {
    # Primary. Free tier, very large context, and positioned for high-volume
    # agentic work, which is this service's exact shape.
    # Not 2.5: the API answers `404 - this model is no longer available to new
    # users` for accounts created recently, and names 3.8 as the replacement. That
    # is the error the chain surfaced the first time a real call was made, and it
    # is why the model is a setting rather than a constant.
    #
    # For the cheap bulk role, set LLM_GEMINI_MODEL=gemini-3.5-flash-lite.
    "gemini": ("https://generativelanguage.googleapis.com", "gemini-3.8-flash"),

    # Fallbacks for volume. All OpenAI-compatible, all with a recurring free
    # allowance, all independent quota pools -- which is the point of having more
    # than one. A single provider's rate limit is a single point of failure.
    "cerebras": ("https://api.cerebras.ai/v1", "qwen-3.8-27b"),
    "groq": ("https://api.groq.com/openai/v1", "qwen/qwen3.8-27b"),
    "nvidia": ("https://integrate.api.nvidia.com/v1", "deepseek-ai/deepseek-v4.1-flash"),
    "mistral": ("https://api.mistral.ai/v1", "magistral-small-latest"),
    "cohere": ("https://api.cohere.com/compatibility/v1", "command-r-plus-08-2024"),

    # Last resort. 50 requests/day without credits, so it is a floor rather than
    # a provider to design around.
    # One of the 16 ids that actually carry the `:free` suffix today. OpenRouter
    # rotates these, so expect to revisit it; `GET /models` and filter on
    # `.endswith(":free")` is how to find a current one.
    "openrouter": ("https://openrouter.ai/api/v1", "qwen/qwen3.8-27b:free"),

    # Paid, listed for completeness.
    "openai": ("https://api.openai.com/v1", "gpt-4o-mini"),
}


class ResponseCache:
    """Bounded in-process LRU cache.

    Bounded because the keys are prompt hashes: an unbounded map in a long-lived
    process is a slow memory leak, and a workflow that runs twice with the same
    prompt is a test or a retry, not the steady state.

    In-process and not Redis, on purpose. A cache shared across replicas would
    make a developer's local run depend on what production cached, which is the
    opposite of reproducible.
    """

    def __init__(self, max_entries: int = 256, ttl_seconds: int = 3600) -> None:
        self._max = max_entries
        self._ttl = ttl_seconds
        self._data: OrderedDict[str, tuple[float, LLMResponse]] = OrderedDict()
        self.hits = 0
        self.misses = 0

    def get(self, key: str) -> LLMResponse | None:
        entry = self._data.get(key)
        if entry is None:
            self.misses += 1
            return None

        stored_at, response = entry
        if time.monotonic() - stored_at > self._ttl:
            # Expired. Dropped rather than returned, so a stale answer is never
            # served after a prompt or model change.
            del self._data[key]
            self.misses += 1
            return None

        self._data.move_to_end(key)
        self.hits += 1
        # A copy, so a caller mutating the response cannot corrupt the cache for
        # everyone else.
        return LLMResponse(
            text=response.text,
            provider=response.provider,
            key_index=response.key_index,
            cached=True,
        )

    def set(self, key: str, response: LLMResponse) -> None:
        self._data[key] = (time.monotonic(), response)
        self._data.move_to_end(key)
        while len(self._data) > self._max:
            self._data.popitem(last=False)

    def clear(self) -> None:
        self._data.clear()
        self.hits = 0
        self.misses = 0


class Pacer:
    """A floor on how often any one provider may be called.

    ## The gap this fills

    `CooldownRegistry` is *reactive*: it learns a key is exhausted after the 429
    arrives. Nothing stopped the next request going out 40ms later. A live draft
    showed the shape -- one provider, ten keys, and `key=0` retried four times
    with the gap between attempts set entirely by the backoff it had just been
    given:

        key=0; cooling down 1.7s (attempt 1)
        key=0; cooling down 3.0s (attempt 2)
        key=0; cooling down 7.2s (attempt 3)
        key=0; cooling down 14.5s (attempt 4)

    Each delay is longer than the last and each one still ends in another 429. The
    provider was never given a chance to recover, because backoff only ever starts
    *after* the failure that provoked it.

    ## Why per provider and not per key

    Per-key pacing would have changed nothing here. All ten Gemini keys belong to
    one Google Cloud project, so they share one quota: skipping from `key=0` to
    `key=1` is the same request to the same bucket. The evidence is in the log --
    a 429 on `key=0` is followed by attempts on `key=0` again, not spread across
    the set, because the registry had already put the rest to sleep.

    So the unit is the provider, and a key rotation inside it is free.

    ## Why this is not simply a bigger backoff

    A larger `base_delay` would also space the calls out, but it would do so by
    punishing the first failure -- the common case stays slow forever, because the
    delay is a property of the provider rather than of how hard it is being
    driven. Here the floor is unconditional and small, so a quiet run is
    unaffected and a hammering run is the only one that waits.

    ## Not a hard rate limit

    This is a floor on the interval between two requests, not a quota. It cannot
    stop a burst it has already committed to, and it deliberately does not try:
    the ceiling belongs to the provider, and the honest response to being told
    "slow down" is to slow down before being told again.
    """

    def __init__(self, *, min_interval: float = 0.0, overrides: dict[str, float] | None = None) -> None:
        self._min = max(0.0, min_interval)
        self._overrides = {k: max(0.0, float(v)) for k, v in (overrides or {}).items()}
        #: provider -> monotonic time of its last *dispatched* request.
        self._last: dict[str, float] = {}
        #: Every wait this pacer imposed, so the cost is visible in the logs
        #: rather than being an unexplained gap in the timeline.
        self.waits: int = 0
        self.waited_seconds: float = 0.0

    def interval_for(self, provider: str) -> float:
        return self._overrides.get(provider, self._min)

    async def wait(self, provider: str) -> float:
        """Block until this provider may be called again. Returns seconds slept."""
        floor = self.interval_for(provider)
        if floor <= 0.0:
            self._last[provider] = time.monotonic()
            return 0.0

        previous = self._last.get(provider)
        now = time.monotonic()
        if previous is None:
            # First call to this provider: nothing to space it against.
            self._last[provider] = now
            return 0.0

        delay = previous + floor - now
        # Stamped before sleeping, not after. A request that fails must still
        # count against the interval -- otherwise a provider that 429s on every
        # call would be retried instantly forever, which is the exact behaviour
        # this exists to remove.
        self._last[provider] = previous + floor
        if delay <= 0.0:
            return 0.0

        self.waits += 1
        self.waited_seconds += delay
        await asyncio.sleep(delay)
        return delay


class CooldownRegistry:
    """Per-key backoff, so a rate-limited key is *skipped* rather than retried.

    Rotation on its own is not enough, and this exists because the difference
    matters. Rotating means "when this key says 429, use a different one". It says
    nothing about the *next request*, which arrives milliseconds later from a
    different workflow and finds the same exhausted key still first in line.

    Observed live before this existed: a draft run issued six calls to groq in
    about four seconds and five of them were 429. Every one of them hit the same
    single key, because the chain looped over a one-element key list and then fell
    through to the next provider with no delay. The fix that matters is not "try
    another key" -- groq had one key -- it is "remember that this key is
    exhausted, and stop asking for a few seconds".

    Three behaviours, deliberately:

    * **Exponential with jitter.** A fixed delay synchronises every workflow onto
      the same retry instant and reproduces the thundering herd that caused the
      limit. Full jitter spreads them.
    * **`Retry-After` shortens the wait when the provider names one.** Mistral
      sends none (verified: three consecutive 429s, no header), so this is a
      refinement and never the mechanism.
    * **A circuit breaker for non-rate-limit failures.** A provider that times out
      is not rate limited, so the rate-limit path does not help it. nvidia timed
      out at both 20s and 120s in live probing, and sat mid-chain burning a full
      timeout on every draft. After repeated failures it is taken out of rotation
      for minutes rather than re-probed on every request.

    Process-local and in-memory, deliberately: this is a laptop-and-demo
    deployment, and a shared store would make a developer's behaviour depend on
    what production saw.
    """

    def __init__(
        self,
        *,
        base_delay: float = 2.0,
        max_delay: float = 120.0,
        max_attempts: int = 6,
        failure_threshold: int = 3,
        failure_cooldown: float = 300.0,
        jitter: float = 1.0,
    ) -> None:
        self._base = max(0.0, base_delay)
        self._max = max(self._base, max_delay)
        self._max_attempts = max(1, max_attempts)
        self._failure_threshold = max(1, failure_threshold)
        self._failure_cooldown = max(0.0, failure_cooldown)
        self._jitter = max(0.0, jitter)
        #: (provider, key_index) -> seconds until which it must not be used.
        self._until: dict[tuple[str, int], float] = {}
        #: (provider, key_index) -> consecutive *rate limits*, for backoff growth.
        #:
        #: Separate from `_failures` on purpose. They were one counter at first,
        #: which meant consecutive 429s could not ratchet -- each one cleared the
        #: streak it was supposed to be extending -- and, worse, a provider being
        #: rate-limited reset the breaker that was counting its real failures. A
        #: quota and a broken endpoint are different faults and must not share
        #: state.
        self._strikes: dict[tuple[str, int], int] = {}
        #: (provider, key_index) -> consecutive non-rate-limit failures.
        self._failures: dict[tuple[str, int], int] = {}

    def available_for(self, provider: str, key_index: int) -> bool:
        return time.monotonic() >= self._until.get((provider, key_index), 0.0)

    def remaining(self, provider: str, key_index: int) -> float:
        return max(0.0, self._until.get((provider, key_index), 0.0) - time.monotonic())

    def penalise_rate_limit(
        self, provider: str, key_index: int, *, retry_after: float | None = None
    ) -> float:
        """Put one key to sleep after a 429. Returns the delay applied."""
        attempt = self._strikes.get((provider, key_index), 0) + 1
        delay = min(self._base * (2 ** (attempt - 1)), self._max)

        if retry_after is not None:
            # The provider knows its own window better than our doubling does.
            delay = min(max(delay, retry_after), self._max)

        if self._jitter:
            # Full jitter over [delay/2, delay]. Not [0, delay]: a retry landing
            # at zero would defeat the backoff entirely.
            delay = delay * (0.5 + random.random() * 0.5 * self._jitter)

        self._until[(provider, key_index)] = time.monotonic() + delay
        self._strikes[(provider, key_index)] = attempt
        log.warning(
            "llm rate limit provider=%s key=%s; cooling down %.1fs (attempt %d)",
            provider, key_index, delay, attempt,
        )
        return delay

    def penalise(self, provider: str, key_index: int, *, transient: bool) -> bool:
        """Route a provider failure to the treatment it deserves.

        "Busy" and "gone" are separated here rather than at each call site,
        because getting it wrong is expensive in both directions. Counting a 503
        as a hard failure opened a 300-second breaker on a provider that was
        working -- live, on Gemini, which has ten keys, so three overloaded
        responses would have taken out the whole account over ordinary load.
        Counting a timeout as transient would instead re-probe a dead endpoint on
        every request, which is what nvidia was doing.
        """
        if transient:
            # Short, doubling nap. The provider said "busy"; it did not say "gone".
            self.penalise_rate_limit(provider, key_index)
            return False
        return self.penalise_failure(provider, key_index)

    def penalise_failure(self, provider: str, key_index: int) -> bool:
        """Count a non-rate-limit failure. Returns True when the breaker opens."""
        slot = (provider, key_index)
        count = self._failures.get(slot, 0) + 1
        self._failures[slot] = count
        if count >= self._failure_threshold:
            self._until[slot] = time.monotonic() + self._failure_cooldown
            log.warning(
                "llm provider=%s key=%s failed %d times; breaker open for %.0fs",
                provider, key_index, count, self._failure_cooldown,
            )
            return True
        return False

    def succeed(self, provider: str, key_index: int) -> None:
        """A success clears both streaks, so the backoff never ratchets upward."""
        self._strikes.pop((provider, key_index), None)
        self._failures.pop((provider, key_index), None)
        self._until.pop((provider, key_index), None)

    def reset(self) -> None:
        self._until.clear()
        self._strikes.clear()
        self._failures.clear()

    def snapshot(self) -> list[dict[str, object]]:
        """What is cooling down and for how long. For a health endpoint."""
        now = time.monotonic()
        return [
            {
                "provider": provider,
                "key_index": key_index,
                "cooldown_remaining_seconds": round(until - now, 1),
                "consecutive_failures": self._failures.get((provider, key_index), 0),
            }
            for (provider, key_index), until in sorted(self._until.items())
            if until > now
        ]


class BudgetTracker:
    """Per-workflow LLM call allowance.

    Keyed by ``workflow_run_id`` so one expensive workflow cannot exhaust the
    allowance of every other run in flight. Entries are bounded and pruned by age
    because runs are not closed explicitly -- there is no signal from .NET that
    a run is finished.
    """

    def __init__(self, max_per_workflow: int = 15, max_entries: int = 512) -> None:
        self._max = max_per_workflow
        self._counts: dict[int, int] = {}
        self._max_entries = max_entries

    def used(self, workflow_run_id: int) -> int:
        return self._counts.get(workflow_run_id, 0)

    def remaining(self, workflow_run_id: int) -> int:
        return max(0, self._max - self.used(workflow_run_id))

    def check(self, workflow_run_id: int) -> None:
        """Raise before spending, not after.

        Checking after the call would mean the breaching call still happened and
        was still paid for.
        """
        if self.used(workflow_run_id) >= self._max:
            raise LLMUnavailableError(
                [(f"budget", f"Workflow {workflow_run_id} has used all "
                             f"{self._max} LLM calls. This is a runaway-loop guard, "
                             f"not a quota.")]
            )
        self._prune()

    def increment(self, workflow_run_id: int) -> None:
        self._counts[workflow_run_id] = self.used(workflow_run_id) + 1

    def _prune(self) -> None:
        # Evicting the lowest run id approximates oldest-first, which is right
        # because .NET allocates run ids monotonically.
        while len(self._counts) >= self._max_entries:
            oldest = min(self._counts)
            del self._counts[oldest]


_cache = ResponseCache()
_budget = BudgetTracker()
#: Rebuilt from settings on first use; see `get_cooldown`.
_cooldown: CooldownRegistry | None = None
_pacer: Pacer | None = None


def get_cache() -> ResponseCache:
    return _cache


def get_budget() -> BudgetTracker:
    return _budget


def get_cooldown(settings: Settings | None = None) -> CooldownRegistry:
    """The process-wide cooldown registry, built from settings on first use.

    Lazy rather than constructed at import because the settings are not read at
    import time, and a module-level `Settings()` would freeze the environment
    before the test harness has finished arranging it.
    """
    global _cooldown
    if _cooldown is None:
        s = settings or get_settings()
        _cooldown = CooldownRegistry(
            base_delay=float(s.llm_rate_limit_base_delay_seconds),
            max_delay=float(s.llm_rate_limit_max_delay_seconds),
            failure_threshold=int(s.llm_provider_failure_threshold),
            failure_cooldown=float(s.llm_provider_failure_cooldown_seconds),
            jitter=float(s.llm_rate_limit_jitter),
        )
    return _cooldown


def get_pacer(settings: Settings | None = None) -> Pacer:
    """The process-wide request pacer, built from settings on first use.

    Process-wide for the same reason the cooldown registry is: the whole point is
    that a *second* workflow's request is spaced against the first one's, so a
    per-call instance would recreate the problem it exists to fix.
    """
    global _pacer
    if _pacer is None:
        s = settings or get_settings()
        overrides = {
            name: value
            for name, value in (
                (provider, getattr(s, f"llm_min_interval_{provider}", None))
                for provider in PROVIDER_DEFAULTS
            )
            if value is not None
        }
        _pacer = Pacer(min_interval=float(s.llm_min_interval_seconds), overrides=overrides)
        log.info(
            "llm pacing %s (overrides: %s)",
            f"{float(s.llm_min_interval_seconds):.2f}s",
            overrides or "none",
        )
    return _pacer


def reset_for_tests() -> None:
    """Clear the module-level cache, budget and cooldowns. For tests."""
    global _cooldown
    _cache.clear()
    _budget._counts.clear()
    _cooldown = None


def describe_providers(settings: Settings | None = None) -> list[dict[str, object]]:
    """What the chain will actually do, for logs and a health endpoint.

    The failure this exists to prevent: a provider is misconfigured -- no key, or
    a model name the provider has retired -- and nothing says so until a workflow
    reaches that provider and gets a 404. At that point the error reads as "the
    model is unavailable", which sends someone looking at the provider rather than
    at their own configuration.

    Reported without key material: only the count of keys per provider.
    """
    settings = settings or get_settings()
    rows: list[dict[str, object]] = []

    for name in settings.providers():
        keys = settings.keys_for(name)
        configured_model = getattr(settings, f"llm_{name}_model", "")
        default_model = PROVIDER_DEFAULTS.get(name, ("", ""))[1]

        rows.append({
            "provider": name,
            "keys_configured": len(keys),
            "model": configured_model or default_model,
            "model_is_default": not configured_model,
            "usable": bool(keys) or name == "fixture",
        })

    return rows


def log_configuration(settings: Settings | None = None) -> None:
    """Log the chain as configured, and flag anything that will not work."""
    settings = settings or get_settings()

    for row in describe_providers(settings):
        if row["provider"] == "fixture":
            continue
        if not row["usable"]:
            log.warning(
                "LLM provider %r is in the chain with no API key; it will be skipped.",
                row["provider"],
            )
        else:
            log.info(
                "LLM provider %-11s keys=%s model=%s%s",
                row["provider"], row["keys_configured"], row["model"],
                " (default)" if row["model_is_default"] else "",
            )

    if not any(r["usable"] for r in describe_providers(settings)):
        log.warning(
            "No LLM provider in the chain is usable. Workflows will fall back to "
            "recorded fixtures, or fail if none match the prompt."
        )


def build_providers(settings: Settings) -> list[object]:
    """Instantiate the configured providers, in chain order.

    An unknown provider name is skipped with a warning rather than raising. One
    typo in a comma-separated environment variable should not stop the service
    from starting, and the other providers in the chain may be perfectly good.
    """
    providers: list[object] = []

    for name in settings.providers():
        if name == "fixture":
            providers.append(FixtureProvider())
            continue

        keys = settings.keys_for(name)

        if name == "gemini":
            _, default_model = PROVIDER_DEFAULTS["gemini"]
            providers.append(
                GeminiProvider(
                    keys,
                    # Same reason as below: a setting beats a hardcoded name that
                    # 404s the day a provider renames something.
                    model=getattr(settings, "llm_gemini_model", "") or default_model,
                    timeout=float(settings.llm_request_timeout_seconds),
                )
            )
            continue

        if name in PROVIDER_DEFAULTS:
            base_url, default_model = PROVIDER_DEFAULTS[name]
            # The model is a setting, because providers rename and retire models
            # and a name baked into the code 404s silently when it goes.
            model = getattr(settings, f"llm_{name}_model", "") or default_model
            # A per-provider base URL override, for gateways and proxies.
            override = getattr(settings, f"llm_{name}_base_url", None)
            providers.append(
                OpenAICompatibleProvider(
                    name=name,
                    keys=keys,
                    base_url=override or base_url,
                    model=model,
                    timeout=float(settings.llm_request_timeout_seconds),
                )
            )
            continue

        log.warning(
            "LLM_PROVIDER_CHAIN names unknown provider %r; skipping. Known: %s",
            name, ", ".join([*PROVIDER_DEFAULTS, "fixture"]),
        )

    return [p for p in providers if p.available()]


async def call_llm(
    prompt: str,
    *,
    agent: str,
    workflow_run_id: int,
    system: str | None = None,
    settings: Settings | None = None,
) -> LLMResponse:
    """Produce one completion, falling back across the provider chain.

    Args:
        prompt: The full prompt. The agent name is prepended as a ``# agent:``
            line so the fixture provider can pick its file without a second
            parameter -- see ``FixtureProvider.complete``.
        agent: Which agent is asking. Used for the fixture file and the log.
        workflow_run_id: Scopes the budget.
        system: Optional system instruction.
        settings: Injected in tests; the cached instance otherwise.

    Returns:
        The first successful response, possibly from cache.

    Raises:
        LLMUnavailableError: every provider failed. Carries per-provider detail.
    """
    settings = settings or get_settings()

    # Budget is checked before the cache lookup, so a cached response still
    # counts. Otherwise a workflow could loop indefinitely on a cache hit without
    # ever tripping the runaway guard.
    _budget.check(workflow_run_id)

    stamped = f"# agent: {agent}\n{prompt}"
    cache_key = hashlib.sha256(
        f"{agent}|{system or ''}|{stamped}".encode("utf-8")
    ).hexdigest()

    if settings.llm_cache_enabled:
        cached = _cache.get(cache_key)
        if cached is not None:
            # A cache hit is not charged. No provider was called, so there is no
            # cost to attribute to the run.
            return cached

    errors: list[tuple[str, str]] = []
    cooldown = get_cooldown(settings)

    for provider in build_providers(settings):
        name = getattr(provider, "name", "?")
        key_indexes = list(range(len(getattr(provider, "_keys", []) or []))) or [0]

        for key_index in key_indexes:
            # Skipped, not slept on. Blocking would hold this request open for a
            # key already known to be exhausted; moving on lets another provider
            # answer now while this one recovers in the background.
            if not cooldown.available_for(name, key_index):
                log.info(
                    "llm skipping provider=%s key=%s in cooldown (%.1fs left)",
                    name, key_index, cooldown.remaining(name, key_index),
                )
                continue

            try:
                # `key_index` is passed. It used to be computed by this loop and
                # then discarded, so every iteration re-sent the identical request
                # on `_keys[-1]` -- the same exhausted key, once per key in the
                # list. Rotation was documented in three places and never happened.
                delayed = await get_pacer(settings).wait(name)
                if delayed > 0:
                    log.info(
                        "llm paced provider=%s key=%s (%.2fs, floor %.2fs)",
                        name, key_index, delayed, get_pacer(settings).interval_for(name),
                    )
                response = await provider.complete(
                    stamped, system=system, key_index=key_index
                )
                cooldown.succeed(name, key_index)
                _budget.increment(workflow_run_id)

                if settings.llm_cache_enabled:
                    _cache.set(cache_key, response)

                log.info(
                    "llm call agent=%s run=%s provider=%s key=%s tokens=%s/%s",
                    agent, workflow_run_id, response.provider, key_index,
                    response.prompt_tokens, response.completion_tokens,
                )
                return response

            except RateLimitError as exc:
                # A rate limit is a statement about this key, not about the model.
                # Put the key to sleep so the *next* request skips it too, then
                # try the next key.
                errors.append((name, str(exc)))
                cooldown.penalise_rate_limit(name, key_index, retry_after=exc.retry_after)
                continue

            except ProviderError as exc:
                errors.append((name, str(exc)))
                # Routed, not counted blindly: a 503 gets a short nap, a timeout
                # gets the long breaker. See `CooldownRegistry.penalise`.
                cooldown.penalise(name, key_index, transient=exc.transient)
                if exc.retryable:
                    # Another key of the same provider is worth trying.
                    continue
                # Non-retryable: the provider will never accept this request. Its
                # remaining keys would produce the same rejection.
                break

            except Exception as exc:  # noqa: BLE001
                # Last resort. A provider raising something outside the taxonomy
                # should degrade the chain, not take the request down.
                #
                # Treated as non-transient: an exception outside the taxonomy is
                # more likely a broken client than a provider having a bad minute,
                # and the long breaker is the right answer for that.
                errors.append((name, f"{type(exc).__name__}: {exc}"))
                cooldown.penalise(name, key_index, transient=False)
                break

    # The chain fell through to the fixture provider without being asked to.
    # Configured chains that name a real provider should not silently start
    # serving recorded answers, or a broken API looks like a working system.
    if not any(getattr(p, "name", "") == "fixture" for p in build_providers(settings)):
        if settings.llm_allow_fixture_fallback:
            log.warning(
                "all configured providers failed; falling back to recorded fixtures. "
                "This is a degraded mode, not a success."
            )
            fixture = FixtureProvider()
            try:
                response = await fixture.complete(stamped, system=system)
                _budget.increment(workflow_run_id)
                return response
            except Exception as exc:  # noqa: BLE001
                errors.append(("fixture", str(exc)))

    raise LLMUnavailableError(errors)


async def call_llm_with_tools(
    messages: list[dict],
    tools: list[dict],
    *,
    agent: str,
    workflow_run_id: int,
    settings: Settings | None = None,
) -> ToolCallResponse:
    """One model turn that may request tool calls, falling back across providers.

    The tool-calling twin of :func:`call_llm`, and it keeps every guarantee that
    function has -- budget before the call, never a raw prompt persisted, always
    the next provider on failure. Those live here rather than in the agent loop so
    that a loop built on a different transport cannot accidentally skip them.

    Deliberately **not cached**. A cache entry is keyed on one prompt string; a
    tool-calling turn is a function of the whole message list, the advertised tool
    schemas, and the model's sampling. Keying it correctly is a different problem
    from the prose case, and a wrong key would replay a trajectory that never
    happened -- which is worse than spending a token.

    Args:
        messages: The transcript so far, including any ``role: tool`` results.
        tools: Tool schemas in the OpenAI function-calling shape.
        agent: Which agent is asking. For the log.
        workflow_run_id: Scopes the budget.
        settings: Injected in tests; the cached instance otherwise.

    Returns:
        The first successful turn, prose or tool calls.

    Raises:
        LLMUnavailableError: every provider failed, or none supports tool calling.
    """
    settings = settings or get_settings()

    _budget.check(workflow_run_id)

    errors: list[tuple[str, str]] = []
    cooldown = get_cooldown(settings)

    for provider in build_providers(settings):
        completer = getattr(provider, "complete_with_tools", None)
        if completer is None:
            # The fixture provider has no tool-calling path. Saying so plainly
            # beats returning an empty turn, which the loop would read as "the
            # model decided it was finished" and report as a completed run.
            errors.append(
                (getattr(provider, "name", "?"), "does not support native tool calling")
            )
            continue

        name = getattr(provider, "name", "?")
        key_indexes = list(range(len(getattr(provider, "_keys", []) or []))) or [0]
        pacer = get_pacer(settings)

        for key_index in key_indexes:
            # This is the busiest path in the subsystem, which is why it is also
            # the one where repeatedly re-asking an exhausted key cost the most:
            # a single draft makes several turns back to back.
            if not cooldown.available_for(name, key_index):
                log.info(
                    "llm skipping provider=%s key=%s in cooldown (%.1fs left)",
                    name, key_index, cooldown.remaining(name, key_index),
                )
                continue

            try:
                # Before the dispatch, and once per attempt -- not once per
                # provider. A provider with ten keys makes ten attempts, and it is
                # the tenth that needs spacing from the first.
                delayed = await pacer.wait(name)
                if delayed > 0:
                    log.info(
                        "llm paced provider=%s key=%s (%.2fs, floor %.2fs)",
                        name, key_index, delayed, pacer.interval_for(name),
                    )
                response = await completer(messages, tools, key_index=key_index)
                cooldown.succeed(name, key_index)
                _budget.increment(workflow_run_id)

                log.info(
                    "llm tool turn agent=%s run=%s provider=%s key=%s tools=%d tokens=%s/%s",
                    agent,
                    workflow_run_id,
                    response.provider,
                    key_index,
                    len(response.tool_calls),
                    response.prompt_tokens,
                    response.completion_tokens,
                )
                return response

            except RateLimitError as exc:
                errors.append((name, str(exc)))
                cooldown.penalise_rate_limit(name, key_index, retry_after=exc.retry_after)
                continue

            except ProviderError as exc:
                errors.append((name, str(exc)))
                # Same routing as the prose path: 5xx is a nap, a timeout is the
                # breaker. The tool loop makes several calls back to back, so
                # treating a busy provider as a dead one here is what used to
                # knock the whole chain over mid-run.
                cooldown.penalise(name, key_index, transient=exc.transient)
                if exc.retryable:
                    continue
                break

            except (KeyError, ValueError) as exc:
                errors.append((provider.name, f"unreadable tool turn: {exc}"))
                break

    raise LLMUnavailableError(errors)
