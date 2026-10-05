"""Providers.

Two shapes:

* ``openai_compatible`` -- one implementation covering OpenAI, Groq and
  OpenRouter, because all three speak the same HTTP API. Three near-identical
  classes would be three places for a shared bug to hide.
* ``fixture`` -- serves recorded responses from a file. This is what makes the
  system runnable and testable with no API keys and no network.
"""

from __future__ import annotations

import hashlib
import json
import logging
from pathlib import Path
from typing import Any

import httpx

from .base import (
    FixtureMissingError,
    LLMResponse,
    ProviderError,
    RateLimitError,
    ToolCall,
    ToolCallResponse,
)

log = logging.getLogger(__name__)

#: Key under which per-call provider state rides on an assistant message.
#:
#: Namespaced and opaque so the tool loop can copy it without knowing which
#: provider needed it -- today only Gemini does, and a second provider needing
#: this should not require editing the loop again.
TOOL_METADATA_KEY = "_provider_tool_metadata"


def _parse_retry_after(value: str | None) -> float | None:
    """Seconds from a ``Retry-After`` header, or ``None``.

    Only the delta-seconds form is honoured. The HTTP-date form is legal but no
    provider in this chain sends it, and parsing it to be thorough would be code
    with no test and no observed need.
    """
    if not value:
        return None
    try:
        seconds = float(value.strip())
    except ValueError:
        return None
    return seconds if seconds >= 0 else None


def _gemini_headers(key: str) -> dict[str, str]:
    """The API key as a header, not a query parameter.

    Gemini accepts either, and the query form is the one every tutorial shows --
    which is how the key ended up in every log line. httpx logs the full request
    URL at INFO, and uvicorn configures httpx's logger at INFO, so every call
    printed `?key=<the actual key>`. Ten keys, all live, written to stdout and to
    anything capturing it.

    The header form is identical to the API and keeps the key out of URLs, which
    are the part that gets logged, cached and pasted into bug reports.
    """
    return {"x-goog-api-key": key, "Content-Type": "application/json"}


def _rate_limited(name: str, response: httpx.Response) -> RateLimitError:
    """A `RateLimitError` carrying whatever the provider said about waiting."""
    retry_after = _parse_retry_after(response.headers.get("retry-after"))
    message = f"{name} rate limited this key"
    if retry_after is not None:
        message += f" (retry after {retry_after:g}s)"
    return RateLimitError(message, retry_after=retry_after)

#: Where recorded responses live. Overridable so a test can point at its own file.
FIXTURE_DIR = Path(__file__).resolve().parent.parent / "tests" / "fixtures" / "llm"


def _wire_messages(messages: list[dict]) -> list[dict]:
    """The transcript as an OpenAI-shaped provider will accept it.

    Underscore-prefixed keys are stripped, and that is not tidiness.

    The tool loop parks per-call provider state on the assistant message under
    `TOOL_METADATA_KEY` so Gemini's `thoughtSignature` survives to the next turn.
    Those messages are protocol-shaped, and this is where they get handed to
    *every other* provider too. Sending the field on is a 400 from groq and
    cerebras -- an unknown property on a message object -- while openrouter
    happened to ignore it, which made the failure look provider-specific and
    intermittent rather than structural.

    Caught live: a draft that Gemini drove for five turns then failed on a 503,
    and whose follow-up was rejected by both fallbacks with a 400 that no amount
    of rotation could get past.
    """
    return [
        {k: v for k, v in message.items() if not str(k).startswith("_")}
        for message in messages or []
    ]


class OpenAICompatibleProvider:
    """OpenAI, Groq and OpenRouter over one HTTP client.

    The three differ only in base URL and which env keys they read, both of which
    are configuration rather than behaviour.
    """

    def __init__(
        self,
        name: str,
        keys: list[str],
        base_url: str,
        model: str,
        timeout: float = 30.0,
    ) -> None:
        self.name = name
        self._keys = keys
        self._base_url = base_url.rstrip("/")
        self._model = model
        self._timeout = timeout

    def available(self) -> bool:
        # A provider with no keys is not "unavailable", it is not configured --
        # and the chain should skip it silently rather than log a failure for
        # every call.
        return bool(self._keys)

    def _resolve_key_index(self, key_index: int) -> int:
        """Normalises ``-1`` (the default) to a real index.

        Defined once because the sentinel appears in three places across two
        providers, and a caller that forgets it would either index off the end of
        the list or record the wrong key on the response.
        """
        if key_index < 0:
            return len(self._keys) - 1
        return min(key_index, len(self._keys) - 1)

    def _key_at(self, key_index: int) -> str:
        return self._keys[self._resolve_key_index(key_index)]

    async def complete(self, prompt: str, *, system: str | None = None, key_index: int = -1) -> LLMResponse:
        if not self._keys:
            raise ProviderError(f"{self.name} has no configured keys", retryable=False)

        # `key_index` is how rotation actually happens.
        #
        # The chain iterates this provider's keys and, on a rate limit, retries
        # with the next one -- but until now it never told the provider *which*
        # one, and this method always used `_keys[-1]`. So a 429 on the last key
        # meant the chain re-sent the identical request with the identical
        # exhausted key, once per key in the list, before moving on to the next
        # provider. `ProviderSpec`'s docstring claimed rotation was "the cheapest
        # defence against a per-key rate limit"; what existed was the opposite.
        #
        # The default of -1 means "the last key", which preserves the documented
        # append-at-the-end behaviour for any caller that does not rotate.
        key = self._keys[key_index]

        messages: list[dict[str, str]] = []
        if system:
            messages.append({"role": "system", "content": system})
        messages.append({"role": "user", "content": prompt})

        payload = {
            "model": self._model,
            "messages": messages,
            # Low, because these tasks are classification and extraction. A long
            # completion here is a sign the model is explaining rather than
            # answering, and it costs money either way.
            "temperature": 0.2,
        }

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(
                    f"{self._base_url}/chat/completions",
                    headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
                    json=payload,
                )
        except httpx.TimeoutException as exc:
            raise ProviderError(
            f"{self.name} timed out after {self._timeout}s",
            retryable=True,
            transient=False,
        ) from exc
        except httpx.HTTPError as exc:
            raise ProviderError(
            f"{self.name} unreachable: {exc}", retryable=True, transient=False
        ) from exc

        if response.status_code == 429:
            raise _rate_limited(self.name, response)
        if response.status_code == 401 or response.status_code == 403:
            # Retryable, so the chain moves to the next key rather than abandoning
            # the provider. See the note on the tool-calling path: a key being
            # rejected is a fact about that key, not about the provider.
            raise ProviderError(
                f"{self.name} rejected key {self._resolve_key_index(key_index)} "
                f"of {len(self._keys)} ({response.status_code})",
                retryable=True,
            )
        if response.status_code >= 400:
            raise ProviderError(
                f"{self.name} returned {response.status_code}: {response.text[:200]}",
                retryable=response.status_code >= 500,
                # 5xx is "busy", not "gone" -- see ProviderError.transient.
                transient=response.status_code >= 500,
            )

        try:
            body = response.json()
            text = body["choices"][0]["message"]["content"]
            usage = body.get("usage", {})
        except (ValueError, KeyError, IndexError) as exc:
            raise ProviderError(f"{self.name} returned an unreadable body", retryable=True) from exc

        return LLMResponse(
            text=text,
            provider=self.name,
            key_index=self._resolve_key_index(key_index),
            prompt_tokens=usage.get("prompt_tokens"),
            completion_tokens=usage.get("completion_tokens"),
        )

    async def complete_with_tools(
        self,
        messages: list[dict],
        tools: list[dict],
        *,
        key_index: int = -1,
    ) -> ToolCallResponse:
        """Native tool calling over the OpenAI chat-completions protocol.

        Separate from :meth:`complete` rather than an extra keyword on it, because
        the two requests genuinely differ: this one carries a full message list
        including prior tool results, and advertises schemas. Sharing one method
        would mean every prose call building a message list it does not need.

        `key_index` works exactly as it does on :meth:`complete`, and for the same
        reason: the tool-calling loop in the chain is the busiest path in the
        subsystem, so it is the one where a 429 on an exhausted key was most
        expensive to retry blindly.
        """
        if not self._keys:
            raise ProviderError(f"{self.name} has no configured keys", retryable=False)

        key = self._keys[key_index]

        payload: dict[str, Any] = {
            "model": self._model,
            # Stripped, not passed through: see `_wire_messages`. These messages
            # may carry another provider's private state.
            "messages": _wire_messages(messages),
            # Zero, deliberately. An agent loop is a decision procedure, and any
            # sampling randomness in "which tool next" makes the trajectory
            # unreproducible, which is what the golden cases assert on. Prose
            # generation elsewhere in the system keeps its own temperature.
            "temperature": 0,
            "tools": tools,
            "tool_choice": "auto",
        }

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(
                    f"{self._base_url}/chat/completions",
                    headers={
                        "Authorization": f"Bearer {key}",
                        "Content-Type": "application/json",
                    },
                    json=payload,
                )
        except httpx.TimeoutException as exc:
            raise ProviderError(
                f"{self.name} timed out after {self._timeout}s", retryable=True
            ) from exc
        except httpx.HTTPError as exc:
            raise ProviderError(
            f"{self.name} unreachable: {exc}", retryable=True, transient=False
        ) from exc

        if response.status_code == 429:
            raise _rate_limited(self.name, response)
        if response.status_code in (401, 403):
            # `retryable=True` is deliberate and was not always so.
            #
            # The chain reads non-retryable as "stop trying this provider", which
            # is right for a request the provider will never accept (a retired
            # model name, a malformed payload -- those surface as a 4xx below with
            # the body attached) and wrong for a key it will never accept.
            #
            # A live run showed the difference. Gemini served five tool turns on
            # key 0, answered 503, the chain correctly moved to key 1, got a 403
            # -- and stopped. Keys 2 through 9 were never attempted. Provider keys
            # are per-project: one being revoked or disabled says nothing about
            # the rest, and abandoning them discarded the provider's whole value.
            #
            # So the signal is split: a rejected *key* earns the next key.
            raise ProviderError(
                f"{self.name} rejected key {self._resolve_key_index(key_index)} "
                f"of {len(self._keys)} ({response.status_code})",
                retryable=True,
            )
        if response.status_code >= 400:
            raise ProviderError(
                f"{self.name} returned {response.status_code}: {response.text[:200]}",
                retryable=response.status_code >= 500,
                # 5xx is "busy", not "gone" -- see ProviderError.transient.
                transient=response.status_code >= 500,
            )

        try:
            body = response.json()
            message = body["choices"][0]["message"]
            usage = body.get("usage", {})
        except (ValueError, KeyError, IndexError) as exc:
            raise ProviderError(
                f"{self.name} returned an unreadable tool-calling body", retryable=True
            ) from exc

        calls: list[ToolCall] = []
        for raw in message.get("tool_calls") or []:
            function = raw.get("function") or {}
            # `arguments` arrives as a JSON *string* on this protocol. A model
            # occasionally emits an object instead, so both are accepted rather
            # than failing a whole turn over a shape detail.
            raw_args = function.get("arguments")
            if isinstance(raw_args, str):
                try:
                    parsed = json.loads(raw_args) if raw_args.strip() else {}
                except ValueError:
                    # Preserved as a sentinel rather than dropped: the runtime
                    # validates arguments against the schema anyway, and a visible
                    # "arguments were unparseable" beats a silently empty dict.
                    parsed = {"__unparseable_arguments__": raw_args[:400]}
            elif isinstance(raw_args, dict):
                parsed = raw_args
            else:
                parsed = {}

            calls.append(
                ToolCall(
                    name=str(function.get("name") or ""),
                    arguments=parsed if isinstance(parsed, dict) else {},
                    id=str(raw.get("id") or ""),
                )
            )

        return ToolCallResponse(
            # `content` is null on a tool-call turn on several providers, so this
            # must tolerate None rather than assuming a string.
            text=message.get("content") or "",
            tool_calls=tuple(calls),
            provider=self.name,
            prompt_tokens=usage.get("prompt_tokens"),
            completion_tokens=usage.get("completion_tokens"),
        )


# ---------------------------------------------------------------------------
# Gemini translation
# ---------------------------------------------------------------------------
#
# Gemini is not the OpenAI protocol, and the differences are not cosmetic. Three
# of them are load-bearing, and all three were verified against the live API
# rather than read off a document:
#
# 1. Tools are declared as `functionDeclarations`, not a `tools` array of
#    `{"type": "function", "function": {...}}`.
# 2. A tool *result* is a `functionResponse` part on a **user**-role content, not
#    a `role: "tool"` message.
# 3. A follow-up request that replays a `functionCall` must carry the
#    `thoughtSignature` the model returned beside it, or the API answers 400.
#
# Point 3 is why this exists as an adapter rather than a flag. There is no way to
# satisfy it without remembering something the model said, which the OpenAI shape
# has nowhere to put.


def _gemini_function_declarations(tools: list[dict]) -> list[dict]:
    """OpenAI tool schemas -> Gemini ``functionDeclarations``.

    Only the subset that survives translation is carried over. Gemini rejects
    unknown JSON-Schema keywords, so passing an OpenAI schema through verbatim
    would fail on the extra properties rather than on the tool itself.
    """
    declarations: list[dict] = []

    for tool in tools or []:
        function = tool.get("function") if isinstance(tool, dict) else None
        # Already in Gemini shape: pass it along untouched.
        if function is None and isinstance(tool, dict) and "functionDeclarations" in tool:
            declarations.extend(tool["functionDeclarations"])
            continue
        if not isinstance(function, dict) or not function.get("name"):
            continue

        parameters = function.get("parameters") or {}
        properties = parameters.get("properties") or {}
        required = parameters.get("required") or []

        declaration: dict[str, Any] = {
            "name": str(function["name"]),
            "description": str(function.get("description") or ""),
            "parameters": {
                "type": "object",
                "properties": {
                    str(key): {
                        k: v
                        for k, v in (value or {}).items()
                        # The keywords Gemini understands. `enum` is included
                        # because dropping it would silently widen a closed
                        # vocabulary the agent relies on.
                        if k in ("type", "description", "enum", "items", "properties")
                    }
                    for key, value in properties.items()
                    if isinstance(value, dict)
                },
            },
        }
        if required:
            declaration["parameters"]["required"] = [str(r) for r in required]
        declarations.append(declaration)

    return [{"functionDeclarations": declarations}] if declarations else []


def _gemini_contents(messages: list[dict]) -> tuple[str | None, list[dict]]:
    """OpenAI messages -> (system instruction, Gemini contents).

    Returns the system instruction separately because Gemini carries it outside
    the conversation, not as the first turn.
    """
    system_parts: list[dict] = []
    contents: list[dict] = []

    for message in messages or []:
        role = message.get("role")

        if role == "system":
            system_parts.append({"text": str(message.get("content") or "")})
            continue

        if role == "user":
            contents.append({"role": "user", "parts": [{"text": str(message.get("content") or "")}]})
            continue

        if role == "assistant":
            parts: list[dict] = []
            text = message.get("content")
            if text:
                parts.append({"text": str(text)})

            # The model's own tool requests, replayed with the signatures it
            # attached to them. Keyed by tool-call id, which is how the runtime
            # paired them and therefore the only stable join.
            signatures = message.get(TOOL_METADATA_KEY) or {}
            for call in message.get("tool_calls") or []:
                function = call.get("function") or {}
                arguments = function.get("arguments")
                if isinstance(arguments, str):
                    try:
                        arguments = json.loads(arguments) if arguments.strip() else {}
                    except ValueError:
                        arguments = {}
                part: dict[str, Any] = {
                    "functionCall": {
                        "name": str(function.get("name") or ""),
                        "args": arguments if isinstance(arguments, dict) else {},
                    }
                }
                signature = (signatures.get(call.get("id")) or {}).get("thought_signature")
                if signature:
                    part["thoughtSignature"] = signature
                parts.append(part)

            if parts:
                contents.append({"role": "model", "parts": parts})
            continue

        if role == "tool":
            # A tool result is a user-role part, and the runtime's `tool_call_id`
            # is not something Gemini uses -- the function *name* is the join.
            name = message.get("name")
            if not name:
                name = _name_for_tool_call_id(messages, message.get("tool_call_id"))
            contents.append(
                {
                    "role": "user",
                    "parts": [
                        {
                            "functionResponse": {
                                "name": str(name or "tool"),
                                # Gemini wants an object here. The runtime stores
                                # results as text, so a bare string is wrapped
                                # rather than passed as a JSON scalar, which the
                                # API rejects.
                                "response": _as_object(message.get("content")),
                            }
                        }
                    ],
                }
            )
            continue

    system_instruction = None
    if system_parts:
        system_instruction = {"parts": system_parts}

    return system_instruction, contents


def _as_object(content: Any) -> dict[str, Any]:
    """Coerce a tool result into the object shape Gemini requires."""
    if isinstance(content, dict):
        return content
    text = "" if content is None else str(content)
    try:
        parsed = json.loads(text)
    except (ValueError, TypeError):
        return {"result": text}
    return parsed if isinstance(parsed, dict) else {"result": parsed}


def _name_for_tool_call_id(messages: list[dict], tool_call_id: Any) -> str | None:
    """Find a tool's name from the id the runtime paired it with.

    The OpenAI protocol pairs a result to a request by id and does not repeat the
    name; Gemini pairs by name. Walking the transcript is the only way to recover
    it, and it is a short walk -- a tool loop is bounded by `max_steps`.
    """
    if tool_call_id is None:
        return None
    for message in messages or []:
        if message.get("role") != "assistant":
            continue
        for call in message.get("tool_calls") or []:
            if call.get("id") == tool_call_id:
                return (call.get("function") or {}).get("name")
    return None


class GeminiProvider:
    """Google's Gemini API, which does not speak the OpenAI protocol.

    Separate rather than adapted, because the request and response shapes are
    genuinely different -- flattening this into the OpenAI client would mean
    branching on the provider name inside the request builder.
    """

    def __init__(self, keys: list[str], model: str = "gemini-2.0-flash", timeout: float = 30.0) -> None:
        self.name = "gemini"
        self._keys = keys
        self._model = model
        self._timeout = timeout

    def available(self) -> bool:
        return bool(self._keys)

    def _resolve_key_index(self, key_index: int) -> int:
        """Normalise ``-1`` (the default) to a real index.

        Defined here as well as on `OpenAICompatibleProvider` because both classes
        need it and neither inherits from the other -- Gemini genuinely speaks a
        different protocol. Sharing it would mean a base class existing only to
        hold four lines, which is worse than the duplication.
        """
        if key_index < 0:
            return len(self._keys) - 1
        return min(key_index, len(self._keys) - 1)

    async def complete(self, prompt: str, *, system: str | None = None, key_index: int = -1) -> LLMResponse:
        # Same check `OpenAICompatibleProvider` makes, and it was missing here.
        # Without it, a Gemini provider configured with no keys raised
        # `IndexError: list index out of range` from `self._keys[-1]` -- a
        # misconfiguration reported as a crash, somewhere in the middle of a
        # chain that was trying to report *something* about the model.
        if not self._keys:
            raise ProviderError("gemini has no configured keys", retryable=False)

        key = self._keys[key_index]
        # Gemini takes the system instruction as a separate field, not a message.
        body: dict[str, object] = {
            "contents": [{"parts": [{"text": prompt}]}],
            "generationConfig": {"temperature": 0.2},
        }
        if system:
            body["systemInstruction"] = {"parts": [{"text": system}]}

        url = (
            f"https://generativelanguage.googleapis.com/v1beta/models/"
            f"{self._model}:generateContent"
        )

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(url, headers=_gemini_headers(key), json=body)
        except httpx.TimeoutException as exc:
            raise ProviderError(f"gemini timed out after {self._timeout}s", retryable=True) from exc
        except httpx.HTTPError as exc:
            raise ProviderError(f"gemini unreachable: {exc}", retryable=True) from exc

        if response.status_code == 429:
            raise _rate_limited("gemini", response)
        if response.status_code in (401, 403):
            raise ProviderError(
                f"gemini rejected key {self._resolve_key_index(key_index)} "
                f"of {len(self._keys)} ({response.status_code})",
                retryable=True,
            )
        if response.status_code >= 400:
            raise ProviderError(
                f"gemini returned {response.status_code}: {response.text[:200]}",
                retryable=response.status_code >= 500,
            )

        try:
            text = response.json()["candidates"][0]["content"]["parts"][0]["text"]
        except (ValueError, KeyError, IndexError) as exc:
            raise ProviderError("gemini returned an unreadable body", retryable=True) from exc

        return LLMResponse(
            text=text,
            provider=self.name,
            # The key actually used, not `len(self._keys) - 1`.
            #
            # This hardcoded the last index, so every Gemini response claimed to
            # have come from the last key whatever was sent. With ten keys
            # configured, that made the audit trail -- the only record of which
            # credential a call was charged to -- wrong nine times out of ten,
            # and it defeated the point of recording it at all.
            key_index=self._resolve_key_index(key_index),
        )

    async def complete_with_tools(
        self,
        messages: list[dict],
        tools: list[dict],
        *,
        key_index: int = -1,
    ) -> ToolCallResponse:
        """Native tool calling for Gemini, translated from the OpenAI shape.

        This is the reason Gemini was being skipped entirely. The agent loop asks
        for ``complete_with_tools``; without it the chain records "does not
        support native tool calling" and moves on, so the one provider with ten
        keys and a generous free tier never took a turn. Verified live: Gemini
        answers a `functionDeclarations` request with a `functionCall` part.

        The translation is not cosmetic -- see the notes above
        :func:`_gemini_contents`. The one that cannot be worked around is
        `thoughtSignature`: a follow-up turn replaying a `functionCall` without
        the signature the model attached is rejected with a 400, so the signature
        is carried back through `ToolCall.provider_metadata`.
        """
        if not self._keys:
            raise ProviderError("gemini has no configured keys", retryable=False)

        key = self._keys[key_index]
        system_instruction, contents = _gemini_contents(messages)

        body: dict[str, Any] = {
            "contents": contents,
            # Zero, matching every other tool-calling path in this codebase: an
            # agent loop is a decision procedure, and sampling in "which tool
            # next" makes the trajectory unreproducible.
            "generationConfig": {"temperature": 0},
        }
        if system_instruction:
            body["systemInstruction"] = system_instruction

        declarations = _gemini_function_declarations(tools)
        if declarations:
            body["tools"] = declarations

        url = (
            f"https://generativelanguage.googleapis.com/v1beta/models/"
            f"{self._model}:generateContent"
        )

        try:
            async with httpx.AsyncClient(timeout=self._timeout) as client:
                response = await client.post(url, headers=_gemini_headers(key), json=body)
        except httpx.TimeoutException as exc:
            raise ProviderError(
                f"gemini timed out after {self._timeout}s",
                retryable=True,
                transient=False,
            ) from exc
        except httpx.HTTPError as exc:
            raise ProviderError(f"gemini unreachable: {exc}", retryable=True) from exc

        if response.status_code == 429:
            raise _rate_limited("gemini", response)
        if response.status_code in (401, 403):
            raise ProviderError(
                f"gemini rejected key {self._resolve_key_index(key_index)} "
                f"of {len(self._keys)} ({response.status_code})",
                retryable=True,
            )
        if response.status_code >= 400:
            raise ProviderError(
                f"gemini returned {response.status_code}: {response.text[:200]}",
                retryable=response.status_code >= 500,
            )

        try:
            payload = response.json()
            parts = payload["candidates"][0]["content"]["parts"]
        except (ValueError, KeyError, IndexError) as exc:
            raise ProviderError(
                "gemini returned an unreadable tool-calling body", retryable=True
            ) from exc

        texts: list[str] = []
        calls: list[ToolCall] = []
        for part in parts:
            if not isinstance(part, dict):
                continue
            if isinstance(part.get("text"), str):
                texts.append(part["text"])
            function_call = part.get("functionCall")
            if isinstance(function_call, dict) and function_call.get("name"):
                call_id = str(function_call.get("id") or f"call_{len(calls)}")
                metadata: dict[str, Any] = {}
                # Sits beside `functionCall` on the same part, not inside it --
                # confirmed by dumping the raw response, because putting it in the
                # obvious place is how it goes missing.
                signature = part.get("thoughtSignature") or part.get("thought_signature")
                if signature:
                    metadata["thought_signature"] = signature
                calls.append(
                    ToolCall(
                        name=str(function_call["name"]),
                        arguments=function_call.get("args")
                        if isinstance(function_call.get("args"), dict)
                        else {},
                        id=call_id,
                        provider_metadata=metadata,
                    )
                )

        usage = payload.get("usageMetadata") or {}

        return ToolCallResponse(
            text="".join(texts),
            tool_calls=tuple(calls),
            provider=self.name,
            prompt_tokens=usage.get("promptTokenCount"),
            completion_tokens=usage.get("candidatesTokenCount"),
        )


class FixtureProvider:
    """Serves recorded responses from ``ai/tests/fixtures/llm/{agent}.jsonl``.

    Keyed by ``sha256(prompt)``, so a prompt edit changes the key and the
    fixture is reported missing rather than silently serving a response written
    for different instructions. That is the whole point: a prompt change that
    invalidates recorded output should be a visible test failure, not a green
    suite running against a stale answer.

    When the chain is running on fixtures and a response is missing, this fails
    loudly. Generating a plausible-looking answer would make the entire system
    appear to work while testing nothing.
    """

    def __init__(self, fixture_dir: Path | None = None) -> None:
        self.name = "fixture"
        self._dir = fixture_dir or FIXTURE_DIR
        # agent -> {prompt_hash: response}
        self._cache: dict[str, dict[str, str]] = {}
        self._load_errors: dict[str, str] = {}

    def available(self) -> bool:
        # Always. Its absence is the signal that the fixtures are wrong, and that
        # should surface as a failed run rather than a silently skipped provider.
        return True

    def _load(self, agent: str) -> dict[str, str]:
        if agent in self._cache:
            return self._cache[agent]

        path = self._dir / f"{agent}.jsonl"
        if not path.exists():
            self._load_errors[agent] = f"no fixture file at {path}"
            self._cache[agent] = {}
            return {}

        table: dict[str, str] = {}
        try:
            for line_no, line in enumerate(path.read_text(encoding="utf-8").splitlines(), start=1):
                line = line.strip()
                if not line or line.startswith("//"):
                    continue
                try:
                    record = json.loads(line)
                    table[record["prompt_sha256"]] = record["response"]
                except (ValueError, KeyError) as exc:
                    # A malformed line is reported and skipped rather than
                    # aborting the load, so one bad fixture does not make every
                    # other agent in the file unusable.
                    self._load_errors[agent] = f"{path.name}:{line_no}: {exc}"
        except OSError as exc:
            self._load_errors[agent] = f"cannot read {path}: {exc}"
            self._cache[agent] = {}
            return {}

        self._cache[agent] = table
        return table

    @staticmethod
    def key_for(prompt: str) -> str:
        return hashlib.sha256(prompt.encode("utf-8")).hexdigest()

    async def complete(
        self, prompt: str, *, system: str | None = None, key_index: int = -1
    ) -> LLMResponse:
        # `key_index` is accepted and ignored.
        #
        # It is here so every provider in the chain shares one signature and the
        # chain can pass a key index unconditionally. Without it, passing one to
        # rotate keys raised `TypeError` on the fixture provider -- and the chain
        # catches broad exceptions, so rotation would have been quietly disabled
        # for every provider by a signature mismatch on the one that has no keys.
        # The agent name is threaded through the prompt's first line by the
        # chain, so the fixture file can be chosen without a second parameter.
        agent = "unknown"
        first_line = prompt.strip().splitlines()[0] if prompt.strip() else ""
        if first_line.startswith("# agent:"):
            agent = first_line.removeprefix("# agent:").strip()

        key = self.key_for(prompt)
        table = self._load(agent)

        if key not in table:
            hint = self._load_errors.get(agent)
            raise FixtureMissingError(
                f"No recorded response for agent '{agent}' and prompt hash {key[:12]}. "
                f"{hint or ''} "
                "This means either the prompt changed (re-record the fixture) or a "
                "code path is calling the model with input no test anticipated. "
                "It will not guess an answer."
            )

        return LLMResponse(text=table[key], provider=self.name, cached=True)
