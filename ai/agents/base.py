"""Shared agent scaffolding.

Every agent follows the same shape: take typed input, gather facts through the
toolbox, ask the model for the part that needs judgement, validate the result
deterministically, write artefacts back to .NET, return a typed output.

The base class owns the parts that must not vary, so an individual agent only
contains what is actually specific to it.
"""

from __future__ import annotations

import logging
import time
from typing import Any, Generic, TypeVar

from pydantic import BaseModel, ValidationError

from ..clients.dotnet_client import DotNetClient
from ..llm.base import LLMError
from ..llm.chain import call_llm
from ..tools.registry import ToolBox

log = logging.getLogger(__name__)

In = TypeVar("In", bound=BaseModel)
Out = TypeVar("Out", bound=BaseModel)


def _unknown_keys(payload: Any, model: type[BaseModel], prefix: str = "") -> list[str]:
    """Every field in `payload` that `model` does not declare, at any depth.

    Written as a walk rather than `extra="forbid"` on each model, for two
    reasons. There are fifteen-odd models across the agents, and a per-model
    `model_config` is fifteen-odd chances to forget one -- a missing
    configuration on a single model is invisible, and the model that matters
    most is not obviously the one you would check. And this is the enforcement
    point that cannot be bypassed: every model response in the system arrives
    through `BaseAgent._parse`, so one implementation here covers all of them,
    including models added later.

    ## Why this matters at all, given Pydantic's default is to ignore extras

    Silently dropping an unknown key is *safe* in the sense that nothing
    unexpected reaches the database. What it is not is *loud*, and the case that
    matters is prompt drift: if a prompt is edited to ask for a field the schema
    does not have, the model dutifully returns it, Pydantic discards it, and the
    agent produces an output that is missing a field nobody noticed was
    requested. The failure surfaces much later, as an empty field in the UI, and
    by then the cause is a prompt edit nobody connects to it.

    Raising here turns that into an error naming the unknown key and the model
    that produced it, at the point where the prompt is still the most recent
    change.
    """
    from pydantic import BaseModel as _BaseModel

    if isinstance(model, type) and issubclass(model, _BaseModel):
        fields = model.model_fields
    else:  # pragma: no cover - defensive; all outputs are BaseModel subclasses
        return []

    if not isinstance(payload, dict):
        return []

    found: list[str] = []
    for key, value in payload.items():
        path = f"{prefix}.{key}" if prefix else key

        if key not in fields:
            found.append(path)
            continue

        found.extend(_walk_unknown(value, fields[key].annotation, path))

    return found


def _walk_unknown(value: Any, annotation: Any, prefix: str) -> list[str]:
    """Descend into nested models and lists of models.

    Args come from `model_fields`, so the annotation is resolved and the
    `None` default is already applied by Pydantic -- there is no `Optional`
    wrapper left to unwrap beyond what `get_args` exposes.
    """
    from typing import get_args, get_origin

    from pydantic import BaseModel as _BaseModel

    origin = get_origin(annotation)

    # list[Model] / set[Model] / tuple[Model, ...]
    if origin in (list, set, tuple, frozenset):
        args = get_args(annotation)
        if args and isinstance(value, (list, set, tuple)):
            item_type = args[0]
            found: list[str] = []
            for index, item in enumerate(value):
                found.extend(_walk_unknown(item, item_type, f"{prefix}[{index}]"))
            return found
        return []

    if origin is dict:
        args = get_args(annotation)
        if len(args) == 2 and isinstance(value, dict) and isinstance(args[1], type):
            found = []
            for key, item in value.items():
                found.extend(
                    _walk_unknown(item, args[1], f"{prefix}[{key!r}]")
                )
            return found
        return []

    if isinstance(annotation, type) and issubclass(annotation, _BaseModel):
        return _unknown_keys(value, annotation, prefix)

    return []


class AgentError(RuntimeError):
    """The agent could not produce a valid result.

    Carries the reason, because "the draft agent failed" is not actionable and
    "the model's category id was not in the allowed set" is.
    """

    def __init__(self, message: str, *, agent: str, partial_output: Any = None) -> None:
        super().__init__(message)
        self.agent = agent
        self.partial_output = partial_output


class BaseAgent(Generic[In, Out]):
    """Base class for the six agents."""

    #: Registry key, which is also the permission-set key and the prompt's agent
    #: line. Subclasses must override it; they cannot inherit it, because a base
    #: value would silently grant the base class's permissions.
    name: str = ""

    def __init__(
        self,
        client: DotNetClient,
        *,
        categories: list[dict] | None = None,
    ) -> None:
        self._client = client
        self._categories = categories or []

    def _toolbox(self, workflow_run_id: int) -> ToolBox:
        return ToolBox(
            agent=self.name,
            workflow_run_id=workflow_run_id,
            client=self._client,
            categories=self._categories,
        )

    async def _ask_model(
        self,
        prompt: str,
        *,
        workflow_run_id: int,
        system: str | None = None,
    ) -> str:
        """One completion, or an ``AgentError``.

        Wrapped so every agent reports a model failure the same way, and so the
        workflow run's step log records which agent hit it.
        """
        try:
            response = await call_llm(
                prompt, agent=self.name, workflow_run_id=workflow_run_id, system=system
            )
            return response.text
        except LLMError as exc:
            raise AgentError(
                f"{self.name} could not reach a model: {exc}", agent=self.name
            ) from exc

    async def _parse(self, raw: str, model: type[Out], *, workflow_run_id: int) -> Out:
        """Parse and validate model output.

        A model that returns prose instead of JSON is a normal failure, not an
        exception: the message says what was wrong, and the caller decides
        whether to fall back or give up.
        """
        import json

        text = raw.strip()
        if text.startswith("```"):
            newline = text.find("\n")
            if newline != -1:
                text = text[newline + 1 :]
            if text.rstrip().endswith("```"):
                text = text.rstrip()[:-3]
            text = text.strip()

        start = text.find("{")
        end = text.rfind("}")
        if start != -1 and end > start:
            text = text[start : end + 1]

        try:
            payload = json.loads(text)
        except json.JSONDecodeError as exc:
            raise AgentError(
                f"{self.name} returned output that is not JSON: {exc}. "
                f"First 200 characters were: {raw[:200]!r}",
                agent=self.name,
            ) from exc

        try:
            # Checked before Pydantic, because Pydantic ignores what it does not
            # recognise and the whole point is to not ignore it. Nested models
            # are covered too -- an unknown key inside one recommendation is as
            # much a schema drift as one at the top level.
            unknown = _unknown_keys(payload, model)
            if unknown:
                raise AgentError(
                    f"{self.name} returned fields that are not in the schema: "
                    f"{', '.join(unknown[:5])}"
                    f"{f' (+{len(unknown) - 5} more)' if len(unknown) > 5 else ''}. "
                    f"Either the prompt asks for something the schema does not "
                    f"define, or the model invented it.",
                    agent=self.name,
                    partial_output=payload,
                )

            # `model_validate` rather than `model_validate_json`, so the
            # validation error names the field that failed.
            return model.model_validate(payload)
        except ValidationError as exc:
            raise AgentError(
                f"{self.name} returned output that failed schema validation: "
                f"{exc.error_count()} error(s), first: {exc.errors()[0]['loc']} "
                f"{exc.errors()[0]['msg']}",
                agent=self.name,
                partial_output=payload,
            ) from exc

    async def _record_step(
        self,
        *,
        workflow_run_id: int,
        step_name: str,
        input_summary: dict[str, Any],
        output_summary: dict[str, Any],
        status: str,
        started: float,
        error: str | None = None,
    ) -> None:
        """Write the step to .NET.

        A failure to record is logged and swallowed: the work happened, and
        refusing to report it because an audit row did not save would be worse
        than a missing row.
        """
        duration_ms = int((time.monotonic() - started) * 1000)
        if error:
            output_summary = {**output_summary, "error": error}

        await self._client.append_step(
            workflow_run_id,
            step_name,
            self.name,
            input_summary,
            output_summary,
            status,
            duration_ms,
        )

    @staticmethod
    def _summary(value: Any, *, max_items: int = 8) -> dict[str, Any]:
        """A small description of an input, for the step log.

        Inputs can be a client's entire typed brief. The log records that it was
        that long, not what it said.
        """
        if isinstance(value, BaseModel):
            dumped = value.model_dump()
            return {
                "fields": sorted(dumped.keys()),
                "partial_text_chars": len(dumped.get("partial_text", "")) or None,
                "ids": {k: v for k, v in dumped.items() if k.endswith("_id") and v is not None},
            }
        return {"type": type(value).__name__}

    def read_prompt(self, name: str) -> str:
        """Load a prompt template from ``ai/prompts/``.

        Prompts live in files rather than in string literals so they can be
        versioned and reviewed on their own, and so a change to one is a visible
        diff instead of a line inside a class.
        """
        from pathlib import Path

        path = Path(__file__).resolve().parent.parent / "prompts" / f"{name}.txt"
        try:
            return path.read_text(encoding="utf-8")
        except OSError as exc:
            raise AgentError(
                f"Prompt {name!r} could not be read from {path}: {exc}", agent=self.name
            ) from exc
