"""Try other models on a provider that is currently unusable.

## Why this exists

`nvidia` and `mistral` are both valid keys with both models served, and both were
still dropped from the chain:

    nvidia   timed out after 20.0s, three times, at 20s / 120s / 20s
    mistral  429 on the very first request, twice

That evidence is about *a model*, not about *a provider*. nvidia serves 81 models
and mistral serves 46, and a 20-second timeout on a 550B model says nothing
about a 4B one. Discarding 127 available models on the strength of two bad
guesses is the same mistake as deleting a provider because its key looked wrong.

So: try the fast tiers.

## Cost

Only the providers named on the command line are contacted. Their own quota is
spent, which is the point -- nvidia's quota is worthless while nvidia is out of
the chain. Verified providers are never touched unless named, and there is a
test-shaped reason to think about that: re-probing cerebras or groq would spend
a known-good provider's allowance to re-confirm what is already known.

## The timeout is short on purpose

Ten seconds, not the configured twenty. A candidate that cannot answer a two-word
arithmetic question in ten seconds is not going to become the fallback for a
48-second draft, and finding that out cheaply is worth more than finding it out
properly.

Run from `ai/`, naming pairs:

    python scripts/probe_alternate_models.py nvidia:nvidia/nemotron-3-nano-30b-a3b
"""

from __future__ import annotations

import asyncio
import json
import sys
import time
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))

from ai.config import get_settings  # noqa: E402
import ai.llm.chain as chain_module  # noqa: E402
from ai.llm.chain import PROVIDER_DEFAULTS, call_llm_with_tools  # noqa: E402
from ai.llm.providers import TOOL_METADATA_KEY  # noqa: E402

TIMEOUT = 10.0

TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "add",
            "description": "Add two integers and return the sum.",
            "parameters": {
                "type": "object",
                "properties": {"a": {"type": "integer"}, "b": {"type": "integer"}},
                "required": ["a", "b"],
            },
        },
    }
]

OPENING = [{"role": "user", "content": "Use the add tool to compute 20 + 22, then reply with just the number."}]


async def probe(base, provider: str, model: str) -> dict:
    row = {"provider": provider, "model": model, "outcome": "", "detail": ""}
    if provider not in PROVIDER_DEFAULTS:
        row["outcome"] = "unknown provider"
        return row

    # Each candidate starts from a clean slate.
    #
    # The cooldown registry is process-wide, and that is correct for serving
    # traffic and wrong for measuring candidates. The first nvidia model here
    # failed three times, which opened its breaker for 300s -- and every model
    # after it on the same provider then reported "no providers configured",
    # because the one key was asleep. Those later rows would have read as "this
    # model does not work" when the truth was "the previous model's failure was
    # still counting".
    #
    # A probe that inherits the previous candidate's verdict is not measuring the
    # candidate. Clearing it is the difference between a finding and an artefact.
    chain_module._cooldown = None
    chain_module._pacer = None

    # `llm_request_timeout_seconds` is what makes nvidia structurally unreachable:
    # it times out at exactly the budget it is given. Every candidate is tried at a
    # tenth of that, so the finding is about the model rather than the ceiling.
    settings = base.model_copy(
        update={
            "llm_provider_chain": provider,
            f"llm_{provider}_model": model,
            "llm_request_timeout_seconds": TIMEOUT,
            "llm_min_interval_seconds": 0.0,
        }
    )

    started = time.monotonic()
    try:
        first = await call_llm_with_tools(
            OPENING, TOOLS, agent="modelprobe", workflow_run_id=0, settings=settings
        )
    except Exception as exc:  # noqa: BLE001 - the failure is the finding
        row["outcome"] = "unreachable"
        row["detail"] = f"{time.monotonic() - started:.1f}s  {str(exc)[:90]}"
        return row

    first_ms = (time.monotonic() - started) * 1000

    if not first.tool_calls:
        row["outcome"] = "no-tool-call"
        row["detail"] = f"{first_ms:.0f}ms  answered in prose: {first.text.strip()[:40]!r}"
        return row

    call = first.tool_calls[0]
    # Carried for the same reason as in `probe_tool_calling.py`: Gemini's API
    # rejects the follow-up turn with a 400 unless its `thoughtSignature` returns
    # with the functionCall, so a probe that omits it reports a working model as
    # broken.
    metadata = {c.id: c.provider_metadata for c in first.tool_calls if c.provider_metadata}
    assistant_message: dict = {
        "role": "assistant",
        "content": first.text or "",
        "tool_calls": [
            {
                "id": call.id,
                "type": "function",
                "function": {"name": call.name, "arguments": json.dumps(call.arguments)},
            }
        ],
    }
    if metadata:
        assistant_message[TOOL_METADATA_KEY] = metadata

    follow_up = [
        *OPENING,
        assistant_message,
        {"role": "tool", "tool_call_id": call.id, "name": call.name, "content": "42"},
    ]

    started = time.monotonic()
    try:
        second = await call_llm_with_tools(
            follow_up, TOOLS, agent="modelprobe", workflow_run_id=0, settings=settings
        )
    except Exception as exc:  # noqa: BLE001
        row["outcome"] = "unreachable-after-call"
        row["detail"] = f"called {call.name} at {first_ms:.0f}ms, then {str(exc)[:60]}"
        return row

    total_ms = first_ms + (time.monotonic() - started) * 1000
    if second.tool_calls:
        row["outcome"] = "loops"
        row["detail"] = f"{total_ms:.0f}ms  asked for more tools instead of answering"
        return row

    row["outcome"] = "ok"
    row["detail"] = (
        f"{total_ms:6.0f}ms total  called {call.name}{json.dumps(call.arguments)}"
        f" -> {second.text.strip()[:20]!r}"
    )
    return row


async def main() -> int:
    base = get_settings()
    pairs: list[tuple[str, str]] = []
    for arg in sys.argv[1:]:
        if ":" not in arg:
            print(f"skipping {arg!r}: expected provider:model")
            continue
        provider, model = arg.split(":", 1)
        pairs.append((provider.strip(), model.strip()))

    if not pairs:
        print(__doc__)
        return 2

    print(f"timeout per candidate: {TIMEOUT}s   (configured budget is "
          f"{base.llm_request_timeout_seconds}s)")
    print()
    print(f"{'provider':<9} {'outcome':<21} {'model':<40} detail")
    print("-" * 118)

    results = []
    for provider, model in pairs:
        row = await probe(base, provider, model)
        results.append(row)
        print(f"{row['provider']:<9} {row['outcome']:<21} {row['model']:<40} {row['detail']}")

    print()
    winners = [r for r in results if r["outcome"] == "ok"]
    if winners:
        print("Usable as a fallback:")
        for r in winners:
            print(f"  {r['provider']}  {r['model']}")
    else:
        print("No candidate completed the loop. The provider is out for a reason")
        print("that is about capacity, not about the key.")

    return 0 if winners else 1


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
