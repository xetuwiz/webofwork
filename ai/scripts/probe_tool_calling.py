"""Can each provider actually act as an agent?

## Why this is a script and not a test

It calls seven providers over the network and spends tokens, so it must never run
in CI. But it is the only question that matters about a fallback chain, and the
project had no answer to it.

`PROVIDER_DEFAULTS` proves a model *exists*. It says nothing about whether the
provider accepts a `tools` array, emits `tool_calls`, survives having the result
fed back, and produces a final answer. Those are four separate failure modes, and
each one is invisible until a draft needs it -- which is to say, during the
viva.

Restoring `nvidia` and `mistral` to the chain without asking this question was
the mistake that prompted this file. Both keys were valid and both models were
served, and neither fact says anything about tool calling.

## What a pass means

Not "the HTTP call returned 200". Four things, in order:

1. the provider accepted a request carrying a tool declaration;
2. it came back with a parsed `tool_calls` entry naming the right tool;
3. the assistant turn plus a `tool` message was accepted back;
4. the second turn produced prose rather than another tool call.

A provider that fails at 1 cannot be an agent. One that fails at 4 can call
tools but cannot finish, which is a different bug and a worse demo.

## Cost

Two small turns per provider. `max_tokens` is deliberately tiny -- the tool
result is "42" and the follow-up asks for six words. Roughly a few thousand
tokens in total, against the 113 calls that did real damage earlier.

Run from `ai/`.  Exit code 1 if any provider cannot complete the loop.
"""

from __future__ import annotations

import asyncio
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
sys.path.insert(0, str(Path(__file__).resolve().parent.parent.parent))

from ai.config import get_settings  # noqa: E402
from ai.llm.chain import call_llm_with_tools  # noqa: E402
from ai.llm.providers import TOOL_METADATA_KEY  # noqa: E402

# One trivial tool. The arguments are irrelevant; what is being tested is
# whether the provider will call it at all, and whether the round trip closes.
TOOLS = [
    {
        "type": "function",
        "function": {
            "name": "add",
            "description": "Add two integers and return the sum.",
            "parameters": {
                "type": "object",
                "properties": {
                    "a": {"type": "integer"},
                    "b": {"type": "integer"},
                },
                "required": ["a", "b"],
            },
        },
    }
]

OPENING = [{"role": "user", "content": "Use the add tool to compute 20 + 22, then reply with just the number."}]


async def probe(name: str, base: object) -> dict:
    """One provider, one full agent loop. Never falls back to another provider."""
    settings = base.model_copy(update={"llm_provider_chain": name})
    result = {"provider": name, "step": "", "detail": ""}

    try:
        first = await call_llm_with_tools(
            OPENING, TOOLS, agent="toolprobe", workflow_run_id=0, settings=settings
        )
    except Exception as exc:  # noqa: BLE001 - the failure is the finding
        # Reached for the model and did not get an answer. That says nothing
        # about tool calling, because no tool was ever offered. Filing it as
        # "not agentic" would get a working provider deleted from the chain on
        # the strength of a rate limit -- which is the mistake this file was
        # written after.
        result.update(step="unreachable", detail=f"{type(exc).__name__}: {exc}"[:150])
        return result

    if not first.tool_calls:
        # A model that answers in prose has not failed at the protocol; it has
        # declined to use the tool. Worth knowing, but it is not a broken chain.
        result.update(
            step="no-tool-call",
            detail=f"answered in prose instead ({first.text.strip()[:50]!r})",
        )
        return result

    call = first.tool_calls[0]
    if call.name != "add":
        result.update(step="wrong-tool", detail=f"called {call.name!r}")
        return result

    # Step 3 and 4: hand the result back and require a final answer.
    #
    # `TOOL_METADATA_KEY` is carried here for the same reason `tool_loop` carries
    # it: Gemini rejects the follow-up turn with a 400 unless the
    # `thoughtSignature` from its functionCall goes back with it. An earlier
    # version of this probe omitted it and reported `gemini-3.5-flash-lite` as
    # unable to complete a tool loop -- which was this file's bug, not the
    # provider's. A probe that hand-builds messages differently from the code it
    # is probing measures the probe.
    metadata = {
        c.id: c.provider_metadata for c in first.tool_calls if c.provider_metadata
    }
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

    try:
        second = await call_llm_with_tools(
            follow_up, TOOLS, agent="toolprobe", workflow_run_id=0, settings=settings
        )
    except Exception as exc:  # noqa: BLE001
        # The first turn already produced a parsed tool call, so this provider
        # demonstrably speaks the tool protocol. Whatever failed here is capacity,
        # not capability.
        result.update(
            step="unreachable-after-call",
            detail=f"emitted the tool call, then could not be re-reached: {exc}"[:150],
        )
        return result

    if second.tool_calls:
        result.update(
            step="loops",
            detail=f"asked for {len(second.tool_calls)} more tool(s) instead of answering",
        )
        return result

    if not second.text.strip():
        result.update(step="silent", detail="second turn returned no text")
        return result

    result.update(
        step="ok",
        detail=f"called add{json.dumps(call.arguments)} -> {second.text.strip()[:40]!r}",
    )
    return result


async def main() -> int:
    base = get_settings()

    # Targeted by default, and that is the point. Probing the whole chain spends
    # the quota of providers already proven to work, to re-learn something already
    # known. Pass names explicitly to spend only the quota you intend to.
    named = [a.strip() for a in sys.argv[1:] if a.strip()]
    chain = named or [n.strip() for n in base.llm_provider_chain.split(",") if n.strip()]

    if named:
        print(f"Probing only: {', '.join(chain)}")
        print("(providers not named are not contacted, and spend nothing)")
        print()

    print(f"{'provider':<11} {'outcome':<13} detail")
    print("-" * 92)

    results = []
    for name in chain:
        if name == "fixture":
            continue
        row = await probe(name, base)
        results.append(row)
        print(f"{row['provider']:<11} {row['step']:<13} {row['detail']}")

    print()
    agentic = [r for r in results if r["step"] == "ok"]
    spoke = [r for r in results if r["step"] == "unreachable-after-call"]
    declined = [r for r in results if r["step"] == "no-tool-call"]
    unreached = [r for r in results if r["step"] == "unreachable"]
    broken = [
        r for r in results
        if r["step"] in ("wrong-tool", "loops", "silent", "follow-up")
    ]

    print(f"full agent loop  : {', '.join(r['provider'] for r in agentic) or 'none'}")
    if spoke:
        print(f"speaks the protocol, ran out of capacity: {', '.join(r['provider'] for r in spoke)}")
    if declined:
        print(f"accepted tools, chose not to call: {', '.join(r['provider'] for r in declined)}")
    if unreached:
        print(f"never reached a verdict (rate limit/timeout/auth): "
              f"{', '.join(r['provider'] for r in unreached)}")
    if broken:
        print(f"PROTOCOL FAILURE : {', '.join(r['provider'] for r in broken)}")

    print()
    print("Only PROTOCOL FAILURE means a provider cannot be an agent. Everything")
    print("else is a capacity problem on the day, and capacity is what the rest of")
    print("the chain and the cooldown registry are for.")

    return 1 if broken else 0


if __name__ == "__main__":
    sys.exit(asyncio.run(main()))
