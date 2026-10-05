"""Configuration for the WebOfWork AI service.

Every setting comes from the environment, and every one has a default that is
safe in development. The one exception is ``INTERNAL_API_KEY``, which has no
default: an agent service with no shared secret would accept any caller, and the
whole internal surface is trusted to re-check its own preconditions against the
database.
"""

from __future__ import annotations

import os
from functools import lru_cache
from pathlib import Path

from pydantic import Field, field_validator, model_validator
from pydantic_settings import BaseSettings, SettingsConfigDict

#: The ``ai/`` directory, which is the package root.
_PACKAGE_ROOT = Path(__file__).resolve().parent

#: Resolved absolutely rather than left as ``".env"``.
#:
#: A bare relative ``env_file`` is resolved against the *current working
#: directory*, so the same service finds its configuration when started from
#: ``ai/`` and silently does not when started from the repository root or from a
#: container entrypoint. That failure is quiet: the provider keys are simply
# absent, so the chain falls back to recorded fixtures and the service looks
# healthy while using no model at all.
#:
#: An absolute path next to the code is the only version of this that behaves the
#: same however it is launched.
_ENV_FILE = _PACKAGE_ROOT / ".env"


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=str(_ENV_FILE),
        env_file_encoding="utf-8",
        extra="ignore",
    )

    # ── identity ──────────────────────────────────────────────────────────
    app_env: str = "development"

    # ── how to reach the .NET API ─────────────────────────────────────────
    # The base URL of the WebOfWork API. Trailing slashes are stripped so
    # path joins do not produce a double slash.
    dotnet_base_url: str = "https://localhost:5001"

    #: Shared secret, checked in both directions: .NET sends it on every
    #: ``agent/chat`` call and this service sends it on every internal read.
    #:
    #: Deliberately required. There is no default, so starting the service
    #: without one fails immediately and visibly rather than running open.
    internal_api_key: str = Field(..., min_length=1)

    #: Read budget per workflow. Mirrors ``AgenticAiOptions.MaxInternalReadsPerWorkflow``
    #: on the .NET side. Both enforce it, because each is the only one that can
    #: see its own call count.
    max_internal_reads_per_workflow: int = 20

    #: Per-request timeout for calls *out* to .NET. Short on purpose: these are
    #: database reads, and a read that has not returned in five seconds is a
    #: problem worth surfacing rather than waiting out.
    dotnet_read_timeout_seconds: float = 5.0

    #: Timeout for the .NET -> this service call, enforced by .NET, not here.
    #: Declared so both sides can be read side by side.
    dotnet_call_timeout_seconds: int = 30

    #: Whether to verify the .NET server's TLS certificate.
    #:
    #: Defaults to true, and must stay that way outside development. The .NET
    #: project uses the ASP.NET development certificate, which is self-signed,
    #: so a local run fails every internal read with
    #: ``CERTIFICATE_VERIFY_FAILED`` unless this is turned off.
    #:
    #: Turning it off means any network position between this service and .NET can
    #: read and alter every response -- including the shared key, which travels in
    #: a header on every call. So it is a named, logged, development-only switch
    #: rather than something discovered and worked around in code.
    #:
    #: The correct production fix is to point ``DOTNET_BASE_URL`` at a certificate
    #: the system trusts, or to install the dev certificate's authority.
    dotnet_verify_tls: bool = True

    #: Path to a CA bundle, for a private certificate authority. Overrides the
    #: system store without disabling verification.
    dotnet_ca_bundle: str | None = None

    # ── LLM chain ─────────────────────────────────────────────────────────
    #: Comma-separated provider order. The first that answers wins.
    llm_provider_chain: str = "fixture"

    #: Keys per provider, comma-separated. Rotated on rate-limit so one exhausted
    #: key does not fail the workflow -- which matters most for Gemini and Cerebras,
    #: where an account commonly holds several keys against separate projects.
    llm_gemini_api_keys: str = ""
    llm_cerebras_api_keys: str = ""
    llm_groq_api_keys: str = ""
    llm_nvidia_api_keys: str = ""
    llm_mistral_api_keys: str = ""
    llm_cohere_api_keys: str = ""
    llm_openrouter_api_keys: str = ""
    llm_openai_api_keys: str = ""
    llm_openai_base_url: str = "https://api.openai.com/v1"

    #: Model per provider, overriding the built-in default.
    #:
    #: Every hardcoded model name in this project was wrong on its first contact
    #: with a real API -- six 404s, because providers rename and retire models
    #: constantly and a name that was correct last quarter is not correct now.
    #:
    #: So each provider's model is a setting, the defaults are a starting point
    #: rather than a claim, and :func:`ai.llm.chain.describe_providers` reports
    #: what is actually configured. Verify with:
    #:
    #:     curl -H "Authorization: Bearer $KEY" https://api.cerebras.ai/v1/models
    #:
    #: An empty value means "use the built-in default".
    llm_gemini_model: str = ""
    llm_cerebras_model: str = ""
    llm_groq_model: str = ""
    llm_nvidia_model: str = ""
    llm_mistral_model: str = ""
    llm_cohere_model: str = ""
    llm_openrouter_model: str = ""
    llm_openai_model: str = ""

    #: When true and no real provider is configured, the fixture provider serves
    #: recorded responses instead of failing. This is what lets the whole system
    #: be exercised end to end -- CI, a demo, a laptop -- with no API keys.
    llm_allow_fixture_fallback: bool = True

    llm_cache_enabled: bool = True
    llm_cache_ttl_seconds: int = 3600
    llm_max_calls_per_workflow: int = 15
    llm_request_timeout_seconds: int = 30

    #: Floor on the gap between two requests to the same provider, in seconds.
    #:
    #: Proactive, where `llm_cooldown_base_seconds` is reactive. Backoff only
    #: starts once a provider has already said 429, so a run that keeps asking in
    #: between keeps getting told so: one live draft retried a single Gemini key
    #: four times at 1.7s, 3.0s, 7.2s and 14.5s, each delay longer than the last
    #: and each one still ending in another 429.
    #:
    #: Set per provider rather than globally, because the free tiers do not share
    #: a budget: a 50/day allowance and a per-minute allowance need different
    #: spacing.
    #:
    #: `None` means "inherit `llm_min_interval_seconds`". It is deliberately not
    #: `0.0`, because an unset provider override that reads as zero would silence
    #: the pacer for every provider at once -- a default that disables the feature
    #: it configures. An explicit `0` still means "no wait", which is the
    #: difference between "I did not choose" and "I chose none".
    llm_min_interval_seconds: float = 1.5
    llm_min_interval_gemini: float | None = None
    llm_min_interval_cerebras: float | None = None
    llm_min_interval_groq: float | None = None
    llm_min_interval_cohere: float | None = None
    llm_min_interval_openrouter: float | None = None
    llm_min_interval_mistral: float | None = None
    llm_min_interval_nvidia: float | None = None

    #: Backoff applied to a key that answered 429, doubling per consecutive
    #: strike up to the ceiling.
    #:
    #: Exists because key rotation alone does not prevent rate limiting. Rotating
    #: answers "use a different key *now*"; it says nothing about the next
    #: request, which arrives milliseconds later and finds the same exhausted key
    #: first in line. Observed live before this existed: six calls to one provider
    #: in four seconds, five of them 429, every one on the same single key --
    #: groq had only one, so there was nothing to rotate to.
    #:
    #: Deliberately small. This service is not the only client of these free
    #: tiers, and a demo that sleeps for a minute is worse than one that takes an
    #: honest fallback.
    llm_rate_limit_base_delay_seconds: float = 2.0
    llm_rate_limit_max_delay_seconds: float = 60.0

    #: Randomisation of the backoff, as a fraction of the computed delay.
    #:
    #: Not optional in practice: a fixed delay puts every concurrent workflow on
    #: the same retry instant, which is the thundering herd that caused the limit
    #: in the first place. The delay is drawn from [delay/2, delay] -- full jitter,
    #: but never below half, because a retry landing at zero is no backoff at all.
    llm_rate_limit_jitter: float = 1.0

    #: Circuit breaker for providers that fail for reasons a rate-limit backoff
    #: cannot fix.
    #:
    #: nvidia was probed live at both a 20s and a 120s timeout and timed out on
    #: both, while sitting mid-chain -- so every draft paid a full timeout for a
    #: provider that was never going to answer. A timeout is not a 429, so the
    #: backoff above never applied to it. After this many consecutive failures a
    #: key is taken out of rotation for the cooldown below.
    llm_provider_failure_threshold: int = 3
    llm_provider_failure_cooldown_seconds: float = 300.0

    @field_validator("dotnet_base_url")
    @classmethod
    def _strip_trailing_slash(cls, v: str) -> str:
        # Path joins below use f"{base}/internal/...", so a trailing slash would
        # produce a double slash. Harmless in most servers, not in all.
        return v.rstrip("/")

    @model_validator(mode="after")
    def _fail_closed(self) -> "Settings":
        # No checkpoint backend requirement here, because this service holds no
        # checkpoints: .NET owns the run state machine. See README.
        # Every provider named, not just the literal string "fixture".
        #
        # `== "fixture"` only caught one spelling of the same problem. An empty or
        # whitespace chain passed this guard and left `provider_chain` empty, so
        # nothing could be called at all -- every run either fell through to a
        # recorded fixture or failed one call at a time, which is a slower and
        # noisier way to learn the service is misconfigured. Two tests asserting
        # this were failing, and they were right to.
        if self.app_env == "production":
            providers = [
                part.strip() for part in self.llm_provider_chain.split(",") if part.strip()
            ]
            if not providers:
                raise ValueError(
                    "Production needs at least one LLM provider. Set "
                    "LLM_PROVIDER_CHAIN to a comma-separated list such as "
                    "'groq,gemini', or set APP_ENV=development to acknowledge "
                    "this is a demo."
                )
            if "fixture" in providers:
                raise ValueError(
                    "Production must not run on the fixture provider. Set "
                    "LLM_PROVIDER_CHAIN to a real provider, or set "
                    "APP_ENV=development to acknowledge this is a demo."
                )

        # Disabling certificate verification means anything on the path can read
        # the shared key, which is sent in a header on every single call. Allowed
        # in development because the ASP.NET dev certificate is self-signed;
        # refused in production, where it would be a silent downgrade nobody
        # asked for.
        if not self.dotnet_verify_tls and self.app_env == "production":
            raise ValueError(
                "DOTNET_VERIFY_TLS=false is refused in production: it would let "
                "any network position read the shared API key. Point "
                "DOTNET_BASE_URL at a trusted certificate, or set DOTNET_CA_BUNDLE."
            )

        return self

    # ── derived helpers ───────────────────────────────────────────────────

    def keys_for(self, provider: str) -> list[str]:
        """The configured keys for ``provider``, in order.

        Read from a single comma-separated string rather than a list so the value
        round-trips through a plain environment variable without quoting
        gymnastics. Empty segments are dropped, so a trailing comma is harmless.
        """
        raw = getattr(self, f"llm_{provider}_api_keys", "") or ""
        return [k.strip() for k in raw.split(",") if k.strip()]

    def providers(self) -> list[str]:
        return [p.strip() for p in self.llm_provider_chain.split(",") if p.strip()]


@lru_cache
def get_settings() -> Settings:
    """Cached settings.

    ``lru_cache`` so the environment is read once and every caller shares one
    validated instance. Clearing the cache is the supported way to reload in a
    test; calling the constructor directly bypasses validation ordering, which is
    how a half-configured service ends up running.
    """
    return Settings()


def reload_settings() -> Settings:
    """Drop the cache and re-read the environment. For tests."""
    get_settings.cache_clear()
    return get_settings()
