"""The LLM provider interface and the exception taxonomy.

An agent's authority is bounded by what runs *outside* the model. Every rule that
matters -- the score formula, the validation gate, which ids are real -- is
computed in Python. The model's job is the part that genuinely needs judgement:
reading text and describing it. Keeping that split visible in the type system is
the point of this module.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Protocol, runtime_checkable


class LLMError(RuntimeError):
    """Base class for anything that stops a model completing."""


class ProviderError(LLMError):
    """The provider was reachable but failed.

    Carries whether the key should be retried: a 429 is worth another key, a 400
    is not, and treating them the same either wastes the remaining keys or hides
    a permanent misconfiguration behind a chain of retries.

    ``transient`` separates *busy* from *gone*, which need opposite handling.

    A 503 means the provider is overloaded and will likely answer shortly. A
    timeout or a connection failure means the endpoint is not answering at all.
    The first wants a short nap and another key; the second wants the provider
    taken out of rotation for minutes.

    Conflating them is not academic. Live: Gemini answered a run of 503s, and
    because 5xx was counted as a hard failure, three of them opened a 300-second
    circuit breaker per key -- on a provider that was working, and which has ten
    keys. The breaker would have taken out the whole account over load.
    """

    def __init__(
        self, message: str, *, retryable: bool = True, transient: bool = False
    ) -> None:
        super().__init__(message)
        self.retryable = retryable
        #: True for "busy, try again shortly" (5xx). False for "not answering"
        #: (timeout, connection error) and for request-level rejections.
        self.transient = transient


class RateLimitError(ProviderError):
    """The provider rejected this key for rate limiting.

    Carries ``retry_after`` when the provider said so. Most do not -- Mistral
    answers a 429 with no ``Retry-After`` at all, verified live -- so the value is
    a hint to *shorten* our own backoff when present, never the reason to skip it.
    Ignoring a provider that told us when to come back is how a self-imposed
    cooldown becomes longer than the quota window it was avoiding.
    """

    def __init__(
        self, message: str = "rate limited", *, retry_after: float | None = None
    ) -> None:
        super().__init__(message, retryable=True)
        self.retry_after = retry_after


class LLMUnavailableError(LLMError):
    """Every provider in the chain failed.

    Carries the per-provider failures so the log says *why* rather than just
    "unavailable", which is the difference between a five-minute fix (one key
    expired) and an afternoon (which endpoint is down).
    """

    def __init__(self, errors: list[tuple[str, str]]) -> None:
        self.errors = errors
        detail = "; ".join(f"{name}: {message}" for name, message in errors) or "no providers configured"
        super().__init__(f"No LLM provider succeeded. {detail}")


class FixtureMissingError(LLMError):
    """The fixture provider had no recorded response for this prompt.

    Deliberately fatal. A fixture provider that invented output would let the
    whole test suite pass against responses nobody wrote, and the first real
    failure would appear in production instead.
    """


@dataclass
class LLMResponse:
    """One completion."""

    text: str
    provider: str
    #: Which key produced it, for the audit trail. Never the key itself.
    key_index: int = 0
    cached: bool = False
    prompt_tokens: int | None = None
    completion_tokens: int | None = None

    def json(self) -> Any:
        """Parse the completion as JSON, tolerating a fenced code block.

        Models wrap JSON in ```json fences often enough that refusing to parse it
        is a reliability problem rather than strictness. The first ``{`` and last
        ``}`` are taken when a fence is present, so a sentence of preamble does
        not break the parse.

        Raises:
            json.JSONDecodeError: when there is no JSON object in the text at all.
        """
        import json

        raw = self.text.strip()
        if raw.startswith("```"):
            # Drop the opening fence and whatever language tag it carries.
            newline = raw.find("\n")
            if newline != -1:
                raw = raw[newline + 1 :]
            if raw.rstrip().endswith("```"):
                raw = raw.rstrip()[:-3]
            raw = raw.strip()

        start = raw.find("{")
        end = raw.rfind("}")
        if start != -1 and end > start:
            raw = raw[start : end + 1]

        return json.loads(raw)


@dataclass(frozen=True)
class ToolCall:
    """One tool the model has asked the runtime to execute.

    The model *requests*. It does not execute -- that distinction is the whole
    point of native tool calling, and Lecture 05 states it directly: "The model
    returns a structured request... The application validates the tool name,
    inputs, permissions, and business rules before execution."
    """

    #: The runtime's tool name. Not trusted: it is looked up against an allow-list,
    #: because a model can hallucinate a tool that does not exist.
    name: str
    #: Already parsed by the provider against the tool's JSON schema.
    arguments: dict[str, Any]
    #: Correlation id. A result must carry this back or the provider cannot pair
    #: request with response.
    id: str
    #: Opaque provider state that must ride back to the provider next turn.
    #:
    #: Not every protocol is stateless, and Gemini is the proof. Its REST API
    #: rejects a follow-up request whose `functionCall` part lacks the
    #: `thoughtSignature` the model returned alongside it -- a hard 400, verified
    #: live: *"Function call is missing a thought_signature in functionCall parts.
    #: This is required for tools to work correctly."*
    #:
    #: That signature exists nowhere in the OpenAI chat-completions shape, so
    #: without a place to park it the choice is to lose Gemini's tool calling
    #: entirely or to pretend the protocol is uniform. It rides here and is copied
    #: onto the assistant message by the tool loop, which treats it as opaque.
    provider_metadata: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class ToolCallResponse:
    """One turn of a model-directed turn: prose, or a request to call tools.

    Both can be present. A model may emit a tool call *and* say something, and the
    prose is not necessarily a final answer.
    """

    #: Prose the model produced alongside any tool calls. Empty string is normal
    #: when the model's next useful act was a function call.
    text: str
    #: What it wants run. Empty means the model is finished.
    tool_calls: tuple[ToolCall, ...] = ()
    provider: str = ""
    prompt_tokens: int | None = None
    completion_tokens: int | None = None

    @property
    def wants_tools(self) -> bool:
        return bool(self.tool_calls)


@runtime_checkable
class Provider(Protocol):
    """What every provider must offer.

    A ``Protocol`` rather than an ABC so a provider is defined by its methods and
    a test can substitute a one-method stub without inheriting anything.
    """

    name: str

    def available(self) -> bool:
        """Whether this provider can be used right now.

        Checked before any request, so an unconfigured provider is skipped rather
        than attempted and failed.
        """

    async def complete(self, prompt: str, *, system: str | None = None) -> LLMResponse:
        """Produce a completion. Raises a subclass of ``LLMError`` on failure."""

    async def complete_with_tools(
        self,
        messages: list[dict],
        tools: list[dict],
    ) -> ToolCallResponse:
        """Produce either prose or a set of tool calls.

        Optional on the protocol: a provider that cannot do native tool calling
        should raise :class:`ProviderError` with ``retryable=False`` so the chain
        moves on rather than pretending it produced no tool calls, which the agent
        loop would read as "the model decided it was finished".
        """


@dataclass
class ProviderSpec:
    """A provider plus the keys it may use.

    ``keys`` is a list rather than a single value because rotation is the cheapest
    defence against a per-key rate limit: try the next one rather than retrying
    the same exhausted one.
    """

    name: str
    keys: list[str] = field(default_factory=list)
    base_url: str | None = None
