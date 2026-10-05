"""Ordering: nothing is contacted before it has been checked.

## The property

`send_invitations` is the only call in this system that contacts a person. It is
also the only irreversible one. The claim, stated as a test:

> No path exists by which `send_invitations` is called before a `validation`
> result has been recorded for the same workflow run.

That claim is worth more than it looks, because the graph that would make it false
is the *natural* one. The plan this was built from specified
`talent_match: matching → screening → validation → outreach` as a straight line,
which puts outreach two edges from validation and one reorder away from skipping
it. Nothing about that graph enforces the order; the order is a property of how
the nodes were written.

So it is asserted, per stage, from the dispatcher source rather than trusted.

## Why it reads the source rather than running the graph

Running the graph would need a model, a database and a live .NET API, and would
prove the order for one set of inputs. Reading the dispatcher proves it for every
input, costs nothing, and fails at build time. It is a weaker *test* and a
stronger *guarantee*.
"""

from __future__ import annotations

import ast
import re
from pathlib import Path

import pytest

AI_ROOT = Path(__file__).resolve().parent.parent
DISPATCHER = AI_ROOT / "graph" / "dispatcher.py"
REGISTRY = AI_ROOT / "tools" / "registry.py"


def _relative(path: Path) -> str:
    return str(path.relative_to(AI_ROOT)).replace("\\", "/")


def _stage_bodies() -> dict[str, list[str]]:
    """The source lines of each `_stage` method in the dispatcher."""
    source = DISPATCHER.read_text(encoding="utf-8")
    lines = source.splitlines()

    starts: list[tuple[str, int]] = []
    for index, line in enumerate(lines):
        match = re.match(r"\s+async def (_\w+)\(", line)
        if match:
            starts.append((match.group(1), index))

    bodies: dict[str, list[str]] = {}
    for position, (name, start) in enumerate(starts):
        end = starts[position + 1][1] if position + 1 < len(starts) else len(lines)
        bodies[name] = lines[start:end]

    return bodies


STAGES = _stage_bodies()


def test_the_dispatcher_was_parsed():
    """A sanity floor.

    Without it, a refactor that renames the stage methods would make every
    assertion below vacuously true -- passing, because it was checking nothing.
    """
    assert STAGES, "no stage methods found in the dispatcher"
    assert "_talent_match" in STAGES, f"expected _talent_match in {sorted(STAGES)}"


@pytest.mark.parametrize("stage", sorted(STAGES))
def test_no_stage_contacts_anyone(stage):
    """The dispatcher's `self.client` is legitimate; reaching the contact tool is not.

    The dispatcher holds a `DotNetClient` by design -- it is what constructs each
    agent's `ToolBox`. The restriction on *direct* client access applies to
    agents, and is asserted in `test_architecture_guards`.

    What the dispatcher must never do is reach the contact tool itself, because it
    holds no approval gate and writes no audit row for doing so.
    """
    for number, line in enumerate(STAGES[stage], start=1):
        stripped = line.strip()
        if stripped.startswith("#"):
            continue
        assert "send_invitations" not in stripped or stage == "_outreach", (
            f"{stage} references send_invitations on line {number}. The "
            "dispatcher holds no approval gate; contact belongs to the outreach "
            "stage, reached only after a client approves."
        )


def test_talent_match_runs_validation_before_it_can_contact_anyone():
    """The load-bearing assertion.

    `talent_match` plans, matches and validates, then **stops**. Outreach is not
    reached in this stage at all, so there is no edge for the contact to travel
    along before validation has run.
    """
    body = "\n".join(STAGES["_talent_match"])

    assert "validator.run" in body or "ValidationAgent" in body, (
        "talent_match must run the Validation Agent before it returns"
    )

    # The contact tool is unreachable from this stage.
    assert "send_invitations" not in body, (
        "talent_match must not send invitations: the client has not seen the "
        "shortlist yet, and approving it is a separate call"
    )

    # And validation happens after matching, not before -- otherwise the verdict
    # describes a shortlist that does not exist.
    assert body.index("MatchingAgent") < body.index("ValidationAgent"), (
        "validation must follow matching; the other order validates nothing"
    )


def test_outreach_is_only_reachable_from_its_own_stage():
    """One stage, one job.

    If outreach were reachable from `talent_match` it would run in the same pass
    as matching -- which is the bug this whole arrangement exists to prevent.
    """
    for stage, body in STAGES.items():
        if stage == "_outreach":
            continue
        assert "send_invitations" not in "\n".join(body), (
            f"{stage} references send_invitations; outreach must be reached only "
            "through the resume path after a client approval"
        )


def test_the_outreach_stage_has_no_alternative_contact_path():
    """`send_invitations` is the only tool that reaches a person.

    Asserted rather than assumed, because the natural way to add "just email them
    directly" is a new tool rather than a change to this one.
    """
    source = REGISTRY.read_text(encoding="utf-8")
    tree = ast.parse(source)

    # The handler table maps tool name to implementation, so the string keys of
    # every dict literal in the module are the tool names.
    tool_names: set[str] = set()
    for node in ast.walk(tree):
        if isinstance(node, ast.Dict):
            for key in node.keys:
                if isinstance(key, ast.Constant) and isinstance(key.value, str):
                    tool_names.add(key.value)

    acting = {
        name for name in tool_names
        if re.search(r"send|invite|email|notify|contact", name, re.IGNORECASE)
    }

    assert acting == {"send_invitations"}, (
        f"tools whose name suggests they contact a person: {sorted(acting)}. "
        "Every one must go through the approval-gated send_invitations action."
    )


def test_only_outreach_holds_the_contact_tool():
    """The permission map is the enforcement point, so it is asserted directly."""
    tree = ast.parse(REGISTRY.read_text(encoding="utf-8"))

    permissions: dict[str, set[str]] = {}
    for node in ast.walk(tree):
        if not isinstance(node, ast.AnnAssign) or not isinstance(node.target, ast.Name):
            continue
        if node.target.id != "AGENT_TOOL_PERMISSIONS":
            continue

        value = node.value
        if isinstance(value, ast.Call):  # frozenset({...})
            value = value.args[0] if value.args else None
        if not isinstance(value, ast.Dict):
            continue

        for key, val in zip(value.keys, value.values):
            if isinstance(key, ast.Constant) and isinstance(val, ast.Call):
                val = val.args[0] if val.args else None
            if isinstance(key, ast.Constant) and isinstance(val, ast.Set):
                permissions[key.value] = {
                    element.value
                    for element in val.elts
                    if isinstance(element, ast.Constant)
                }

    assert permissions, "could not read AGENT_TOOL_PERMISSIONS"

    holders = [agent for agent, tools in permissions.items() if "send_invitations" in tools]
    assert holders == ["outreach"], (
        f"send_invitations is held by {holders}; only outreach may act on the world"
    )


def test_the_agent_service_never_writes_to_the_database_itself():
    """Restated here as well as in the architecture guards.

    Duplicated on purpose: this file is about ordering, and a run that somehow
    wrote to the database directly could satisfy every ordering assertion while
    bypassing all of them. Ordering a write path that does not exist is not a
    guarantee.
    """
    from tests.test_architecture_guards import _python_files, _parse

    offenders = []
    for path in _python_files():
        tree = _parse(path)
        if tree is None:
            continue
        for node in ast.walk(tree):
            if isinstance(node, ast.ImportFrom):
                root = (node.module or "").split(".")[0].lower()
                if root in {"psycopg2", "psycopg", "asyncpg", "sqlite3", "sqlalchemy"}:
                    offenders.append(_relative(path))

    assert not offenders, f"database access found in: {offenders}"