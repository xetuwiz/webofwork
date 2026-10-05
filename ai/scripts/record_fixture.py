"""Record a fixture for the offline provider.

The fixture provider fails loudly on a missing entry, on purpose: it would
otherwise be a provider that invents answers, and every test built on it would
pass against output nobody wrote.

That makes recording a necessary chore, and this is the chore.

    # see exactly which prompts the suite wants
    pytest -q 2>&1 | grep "No recorded response for agent"

    # from a real model, record what it actually said
    python -m ai.scripts.record_fixture draft "<the prompt text>"

    # from a manual answer, when the real model is not to be trusted
    python -m ai.scripts.record_fixture draft --file answer.json

The key is ``sha256`` of the prompt **exactly as the chain sends it** -- prefixed
with ``# agent: <name>`` -- so re-recording is idempotent and editing a prompt
invalidates its fixture rather than silently serving a stale answer.
"""

from __future__ import annotations

import argparse
import asyncio
import hashlib
import json
import sys
from pathlib import Path

from ..config import _ENV_FILE
from ..llm.chain import FIXTURE_DIR  # noqa: F401  (re-exported for convenience)


def key_for(prompt: str) -> str:
    return hashlib.sha256(prompt.encode("utf-8")).hexdigest()


def stamped(agent: str, prompt: str) -> str:
    """The prompt as the chain sends it.

    Must match ``llm.chain.call_llm`` byte for byte, or the recorded key will not
    match the one looked up at test time.
    """
    return f"# agent: {agent}\n{prompt}"


def append_fixture(agent: str, prompt: str, response: str) -> str:
    """Add or replace one entry. Returns the key."""
    path = Path(FIXTURE_DIR) / f"{agent}.jsonl"
    path.parent.mkdir(parents=True, exist_ok=True)

    key = key_for(stamped(agent, prompt))
    record = {
        "prompt_sha256": key,
        "response": response,
        # Recorded alongside so a fixture's age is visible without opening the
        # file, and so a model change is a deliberate act rather than a silent
        # divergence from what the system used to do.
        "agent": agent,
    }

    entries: list[dict] = []
    if path.exists():
        for line in path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line or line.startswith("//"):
                continue
            try:
                entries.append(json.loads(line))
            except json.JSONDecodeError:
                # A malformed line is kept out of the rewrite rather than fatal.
                # Rewriting the file must not lose someone else's fixture.
                print(f"  skipping malformed line in {path.name}", file=sys.stderr)

    replaced = False
    for index, entry in enumerate(entries):
        if entry.get("prompt_sha256") == key:
            entries[index] = record
            replaced = True
            break
    if not replaced:
        entries.append(record)

    # Sorted so a diff between two runs is readable, rather than append-order.
    entries.sort(key=lambda e: e.get("prompt_sha256", ""))
    path.write_text(
        "\n".join(json.dumps(e, ensure_ascii=False) for e in entries) + "\n",
        encoding="utf-8",
    )
    return key


async def _record_from_model(agent: str, prompt: str) -> int:
    from ..llm.chain import call_llm, reset_for_tests
    from ..config import get_settings

    reset_for_tests()
    settings = get_settings()
    print(f"  provider chain: {settings.providers()}", file=sys.stderr)

    response = await call_llm(
        prompt, agent=agent, workflow_run_id=0, settings=settings
    )
    key = append_fixture(agent, prompt, response.text)
    print(f"  recorded via {response.provider}: {key[:12]}")
    print(f"  -> {Path(FIXTURE_DIR) / f'{agent}.jsonl'}")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("agent", help="draft | planning | matching | screening | validation | outreach")
    parser.add_argument("prompt", nargs="?", help="the prompt, exactly as the agent composes it")
    parser.add_argument("--file", help="read the response from a file instead of calling a model")
    args = parser.parse_args(argv)

    if args.file:
        if not args.prompt:
            parser.error("--file needs the prompt as the second argument")
        response = Path(args.file).read_text(encoding="utf-8")
        key = append_fixture(args.agent, args.prompt, response)
        print(f"  recorded from file: {key[:12]}")
        return 0

    if not args.prompt:
        parser.error("a prompt is required, or use --file")

    return asyncio.run(_record_from_model(args.agent, args.prompt))


if __name__ == "__main__":
    raise SystemExit(main())
