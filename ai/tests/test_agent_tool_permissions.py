"""Can every agent's own direct tool calls actually pass the gate?

## The bug this exists for

Run 23 failed in 0ms:

    agent tool gate rejected a call: Agent 'matching' may not use 'get_project'.
    It may use: get_freelancer, get_freelancer_packages, get_freelancer_reviews,
    search_freelancers.

Read the obvious way and that looks like the model misbehaving -- it asked for a
tool it was not allowed. It was not. `MatchingAgent.run` calls `get_project`
itself, outside the LLM loop, to read the project it is about to match against:

    project = await tools.call("get_project", project_id=payload.project_id)

The gate refused our own code. So `talent_match` was not degraded, it was dead,
and it had never once succeeded, because that call is on the only path through
`run()`.

## Why every other check missed it

The existing guard compared each agent's *advertised tool schemas* against its
allow-list. That catches a model being offered a tool it cannot use. It cannot
catch this, because `get_project` was never advertised to `matching` -- the model
never saw it. Only the direct call used it.

And the two failure modes look nothing alike from outside:

  * inside the loop  -> `REFUSED: ...` becomes a tool result, the run continues,
                        and the planner's earlier mismatches were quiet for
                        exactly this reason;
  * a direct call    -> propagates, and the stage dies.

So the invariant worth testing is not "is everything advertised permitted" but
"does every tool any agent calls in its own code fall inside its own allow-list".
The allow-list is checked against the code, statically, with no LLM involved.
"""

from __future__ import annotations

import ast
import re
from pathlib import Path

import pytest

from ai.tools.registry import AGENT_TOOL_PERMISSIONS

AGENT_DIR = Path(__file__).resolve().parent.parent / "agents"


def _agent_module(name: str) -> Path | None:
    """The module implementing an agent, if the name maps to a file."""
    for candidate in (f"{name}_agent.py", f"{name}.py"):
        path = AGENT_DIR / candidate
        if path.exists():
            return path
    return None


def _direct_tool_calls(path: Path) -> set[str]:
    """Tool names passed to `.call(...)` in this file, however the object is named.

    Deliberately syntactic rather than clever: the failure this guards against is
    a *missing allow-list entry*, so the check has to read the source the same way
    the gate does at runtime. Anything that reaches `Toolbox.call` counts,
    including calls made through a local alias.
    """
    tree = ast.parse(path.read_text(encoding="utf-8"))

    names: set[str] = set()
    for node in ast.walk(tree):
        if not isinstance(node, ast.Call):
            continue
        func = node.func
        if not isinstance(func, ast.Attribute) or func.attr != "call":
            continue
        if not node.args:
            continue
        first = node.args[0]
        # `tools.call("get_project", ...)` and
        # `toolbox.call(name="get_project", ...)` are both real usage.
        if isinstance(first, ast.Constant) and isinstance(first.value, str):
            names.add(first.value)
        for kw in node.keywords:
            if kw.arg == "name" and isinstance(kw.value, ast.Constant):
                if isinstance(kw.value.value, str):
                    names.add(kw.value.value)
    return names


class TestDirectCallsArePermitted:
    """The guard that would have caught run 23 before it reached a demo."""

    @pytest.mark.parametrize("agent", sorted(AGENT_TOOL_PERMISSIONS))
    def test_direct_calls_are_permitted(self, agent: str):
        module = _agent_module(agent)
        if module is None:
            pytest.skip(f"no module found for agent {agent!r}")

        called = _direct_tool_calls(module)
        permitted = AGENT_TOOL_PERMISSIONS[agent]
        forbidden = called - permitted

        assert forbidden == set(), (
            f"{module.name} calls {sorted(forbidden)} directly, but "
            f"{agent!r} may only use {sorted(permitted)}. "
            "A direct call raises out of the loop and kills the stage."
        )

    def test_the_matching_agent_really_does_read_the_project(self):
        # Guards the guard. If `get_project` were removed from the allow-list and
        # the call removed from the code together, the test above would pass
        # vacuously -- so this pins the behaviour the fix exists to enable.
        module = _agent_module("matching")
        assert module is not None
        assert "get_project" in _direct_tool_calls(module)
        assert "get_project" in AGENT_TOOL_PERMISSIONS["matching"]


class TestTheGateItself:
    def test_every_permitted_tool_exists(self):
        from ai.tools.registry import TOOL_ALLOWLIST

        for agent, tools in AGENT_TOOL_PERMISSIONS.items():
            unknown = set(tools) - set(TOOL_ALLOWLIST)
            assert unknown == set(), f"{agent!r} permits non-existent tools {unknown}"

    def test_every_agent_permits_at_least_one_tool(self):
        # An agent with an empty allow-list cannot run at all, which is the same
        # class of failure as run 23 wearing a different hat.
        for agent, tools in AGENT_TOOL_PERMISSIONS.items():
            assert tools, f"{agent!r} may use nothing"

    def test_only_outreach_may_send_invitations(self):
        # The one genuinely privileged action in the system. Worth pinning
        # explicitly, because it is the reason the gate exists at all.
        allowed = [a for a, t in AGENT_TOOL_PERMISSIONS.items() if "send_invitations" in t]

        assert allowed == ["outreach"]


class TestTheCatalogueHasNotDrifted:
    def test_tool_schemas_are_still_declared_in_one_place(self):
        # A cheap tripwire. The earlier planner bug was caused by schemas and the
        # allow-list being maintained separately; this only says the registry is
        # the thing that owns both, which is what makes the checks above possible.
        source = (AGENT_DIR.parent / "tools" / "registry.py").read_text(encoding="utf-8")

        assert re.search(r"^AGENT_TOOL_PERMISSIONS", source, re.MULTILINE)
        assert re.search(r"^TOOL_ALLOWLIST", source, re.MULTILINE)
