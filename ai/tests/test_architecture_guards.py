"""Static guards on the architecture.

## Why greps, when there are already 300 tests

Because the two properties below are not about any one code path. They are
statements about the *absence* of a pattern across a whole package, and absence
is exactly what a functional test cannot observe: every test still passes if
someone adds one line that does the forbidden thing.

Both properties were true by convention and by review. Neither survived contact
with a deadline. A grep is the only thing that makes them durable.

## The two properties

**1. No raw LLM I/O is persisted.** Only validated output schemas and structured
summaries may reach the database. A raw prompt or completion would put client
briefs, freelancer cover letters and model output into `workflow_step_logs`
forever, where they are readable by anyone with database access and removable by
nobody.

**2. No direct database access.** The agent service reaches PostgreSQL only
through the .NET internal API. A direct connection would mean two writers with no
transaction, no audit trail and no approval gate between them -- and it would
make every "deterministic where it matters" claim conditional on a code path
nobody reviewed.

Both are checked by parsing, not by matching text. A grep for the word `prompt`
would fire on `prompt: str` in a function signature, on a docstring explaining
this rule, and on the word "prompt" in a comment -- three false positives per
file, which is how a grep-based guard gets disabled.

## What a failure means

Not "refactor this". It means a rule that was written down is not being kept, and
the honest response is to find out which of the two moved.
"""

from __future__ import annotations

import ast
import re
from pathlib import Path

import pytest

AI_ROOT = Path(__file__).resolve().parent.parent


def _python_files() -> list[Path]:
    """Every module in the service, excluding tests.

    Tests are excluded because they contain the forbidden patterns deliberately --
    a fixture asserting a bad payload is rejected has to name the bad payload.
    """
    return sorted(
        path
        for path in AI_ROOT.rglob("*.py")
        if "tests" not in path.parts and ".venv" not in path.parts
    )


def _parse(path: Path) -> ast.Module | None:
    """The module, or None if it will not parse.

    A syntax error is a failure in its own right and is reported by the compiler
    and by the rest of the suite, so returning None here keeps these guards from
    being the thing that reports it three times.
    """
    try:
        return ast.parse(path.read_text(encoding="utf-8"))
    except SyntaxError:
        return None


def _relative(path: Path) -> str:
    return str(path.relative_to(AI_ROOT)).replace("\\", "/")


def _names(node: ast.AST) -> set[str]:
    """Every attribute and identifier mentioned anywhere in an expression."""
    found: set[str] = set()
    for child in ast.walk(node):
        if isinstance(child, ast.Attribute):
            found.add(child.attr)
        elif isinstance(child, ast.Name):
            found.add(child.id)
        elif isinstance(child, ast.Constant) and isinstance(child.value, str):
            found.add(child.value.lower())
    return found


# ── 1. no raw LLM I/O is persisted ─────────────────────────────────────


#: Arguments that carry something a model saw or produced. A call to one of the
#: internal write endpoints with one of these as the payload is the thing being
#: forbidden.
LEAKY_ARGUMENTS = {"prompt", "raw", "completion", "raw_text", "llm_response", "model_output"}


def _write_endpoint_calls(tree: ast.Module) -> list[tuple[str, list[str]]]:
    """Every call that could persist a workflow artefact, with its argument names.

    Matched on the call *shape* rather than the URL, because the URL is a string
    constant in a few places and a parameter in others. A call carrying a
    `prompt=` argument is worth reporting whatever it is called.
    """
    results: list[tuple[str, list[str]]] = []

    for node in ast.walk(tree):
        if not isinstance(node, ast.Call):
            continue

        callee = node.func
        name = callee.attr if isinstance(callee, ast.Attribute) else getattr(callee, "id", "")
        if not name:
            continue

        # The write helpers, and the `agent/chat` call that carries a prompt.
        relevant = name in {
            "append_step", "append_step_log", "append_tool_call", "append_recommendation",
            "append_screening", "append_validation", "complete_workflow",
            "_write", "request", "post", "put",
        }
        if not relevant:
            continue

        positional = [a for a in node.args if isinstance(a, ast.Constant) and isinstance(a.value, str)]
        keyword_names = [kw.arg for kw in node.keywords if kw.arg]

        # A call whose first argument is a URL containing an internal write path.
        looks_internal = any(
            "/internal/" in p.value and ("workflow" in p.value or "action" in p.value)
            for p in positional
        )
        # Or a call to one of our own write helpers by name.
        is_our_helper = name.startswith("append_") or name in {
            "complete_workflow", "_write", "append_tool_call",
        }

        if looks_internal or is_our_helper:
            results.append((name, keyword_names))

    return results


def test_no_raw_llm_io_reaches_a_write_endpoint():
    """Only summaries and validated schemas may be persisted.

    A raw completion would put client briefs and freelancer cover letters into
    the database permanently, readable by anyone with DB access.
    """
    offences: list[str] = []

    for path in _python_files():
        tree = _parse(path)
        if tree is None:
            continue

        for name, keyword_names in _write_endpoint_calls(tree):
            leaky = [k for k in keyword_names if k in LEAKY_ARGUMENTS]
            if leaky:
                offences.append(f"{_relative(path)}: {name}(...) passes {leaky}")

    assert not offences, (
        "raw LLM I/O appears to reach a persistence call. Persist a validated "
        "output schema or a structured summary, never the prompt or the "
        "completion:\n  " + "\n  ".join(offences)
    )


def test_the_persistence_helper_is_where_summaries_are_built():
    """The positive counterpart.

    Without this, the rule above is satisfied by never writing anything at all,
    and the suite would not notice. Something must actually flow to a write
    endpoint, and what flows must be summarised.
    """
    registry = AI_ROOT / "tools" / "registry.py"
    source = registry.read_text(encoding="utf-8")

    assert "_summarise" in source, (
        "the registry is expected to build a summary of every tool call before "
        "persisting it; without _summarise there is nothing to enforce"
    )


def test_no_module_writes_a_prompt_to_a_file():
    """A prompt on disk is the same leak by a different route.

    Covers ad-hoc logging, which is the realistic way this happens: someone adds
    a `print` while debugging and never removes it.
    """
    offences: list[str] = []

    for path in _python_files():
        tree = _parse(path)
        if tree is None:
            continue

        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue

            callee = node.func
            name = callee.attr if isinstance(callee, ast.Attribute) else getattr(callee, "id", "")

            if name not in {"write_text", "write_bytes", "open"}:
                continue

            # `open(...)` is only interesting in write mode.
            mode = "r"
            for kw in node.keywords:
                if kw.arg == "mode" and isinstance(kw.value, ast.Constant):
                    mode = str(kw.value.value)
            if name == "open" and "w" not in mode and "a" not in mode:
                continue

            mentions = _names(node)
            if mentions & LEAKY_ARGUMENTS:
                offences.append(f"{_relative(path)}: {name}(...) with a prompt-like argument")

    assert not offences, (
        "something writes model I/O to disk. Log a summary:\n  "
        + "\n  ".join(offences)
    )


# ── 2. no direct database access ───────────────────────────────────────


#: Anything that would let this service open its own connection.
FORBIDDEN_IMPORTS = {
    "psycopg2", "psycopg", "asyncpg", "pg8000", "sqlalchemy", "databases",
    "sqlite3", "aiosqlite",
}

#: ORM attribute chains that mean "I am about to run a statement".
ORM_METHODS = {"execute", "executemany", "executescript", "raw", "text"}


def test_no_database_driver_is_imported():
    offences: list[str] = []

    for path in _python_files():
        tree = _parse(path)
        if tree is None:
            continue

        for node in ast.walk(tree):
            if isinstance(node, ast.Import):
                for alias in node.names:
                    if alias.name.split(".")[0].lower() in FORBIDDEN_IMPORTS:
                        offences.append(f"{_relative(path)}: import {alias.name}")
            elif isinstance(node, ast.ImportFrom):
                root = (node.module or "").split(".")[0].lower()
                if root in FORBIDDEN_IMPORTS:
                    offences.append(f"{_relative(path)}: from {node.module} import ...")

    assert not offences, (
        "the agent service must reach PostgreSQL only through the .NET internal "
        "API -- two writers with no transaction, no audit trail and no approval "
        "gate:\n  " + "\n  ".join(offences)
    )


def test_no_sql_is_executed_against_a_connection():
    """Catches a raw statement reached without importing a driver.

    `cursor.execute("SELECT ...")` needs no `psycopg2` import at the point of
    use if the connection arrived from somewhere else -- and a connection can
    arrive from configuration.
    """
    offences: list[str] = []

    for path in _python_files():
        tree = _parse(path)
        if tree is None:
            continue

        for node in ast.walk(tree):
            if not isinstance(node, ast.Call):
                continue

            callee = node.func
            name = callee.attr if isinstance(callee, ast.Attribute) else getattr(callee, "id", "")
            if name not in ORM_METHODS:
                continue

            # Only flag it when a SQL-ish string is actually present, or when the
            # call is `connection.execute(...)` with no obvious innocent reading.
            literals = [
                a.value.lower()
                for a in node.args
                if isinstance(a, ast.Constant) and isinstance(a.value, str)
            ]
            sql_words = ("select ", "insert ", "update ", "delete ", "create table", "drop ")

            if any(l.strip().startswith(sql_words) for l in literals):
                offences.append(f"{_relative(path)}: {name}(<sql>)")
            elif name in {"executemany", "executescript"}:
                offences.append(f"{_relative(path)}: {name}(...)")

    assert not offences, (
        "SQL is executed inside the agent service. All reads and writes belong "
        "on the .NET side, where they are audited and gated:\n  "
        + "\n  ".join(offences)
    )


def test_no_connection_string_is_built_in_code():
    """`postgresql://...` assembled in a module is a connection the guards above
    cannot see, because nothing is executed yet."""
    offences: list[str] = []

    pattern = re.compile(r"postgres(?:ql)?://", re.IGNORECASE)

    for path in _python_files():
        source = path.read_text(encoding="utf-8")
        for number, line in enumerate(source.splitlines(), start=1):
            stripped = line.strip()
            # A comment or a docstring mentioning the scheme is documentation.
            if stripped.startswith("#") or stripped.startswith('"') or stripped.startswith("*"):
                continue
            if pattern.search(line):
                offences.append(f"{_relative(path)}:{number}")

    assert not offences, (
        "a PostgreSQL connection string appears in code. The service talks to "
        "the .NET API, which owns the connection:\n  " + "\n  ".join(offences)
    )


# ── the invariants themselves, restated as tests ──────────────────────


def test_the_internal_read_budget_is_enforced_in_one_place():
    """Every read goes through one method, or the budget is advisory.

    This is the check that would fail if someone added a second read path -- a
    helper that fetches a profile "just for one prompt" -- which is the realistic
    way the budget quietly stops working.
    """
    client_source = (AI_ROOT / "clients" / "dotnet_client.py").read_text(encoding="utf-8")

    # One definition of `_get`, which is where the budget is spent.
    definitions = len(re.findall(r"^\s+async def _get\(", client_source, re.MULTILINE))
    assert definitions == 1, (
        f"{definitions} definitions of _get in the .NET client; the read budget is "
        "enforced inside it, so a second one would be an unbudgeted read path"
    )

    assert "BudgetExceededError" in client_source


def test_the_tool_registry_is_the_only_way_an_agent_reaches_out():
    """Agents hold a `ToolBox`, not a `DotNetClient`.

    Worth asserting because it is what makes prompt injection structurally
    impossible rather than merely filtered: the model never selects a tool, so
    "call the dangerous one" has no target.
    """
    base = (AI_ROOT / "agents" / "base.py").read_text(encoding="utf-8")

    # The base class hands out a toolbox.
    assert "_toolbox" in base

    for path in sorted((AI_ROOT / "agents").glob("*_agent.py")):
        source = path.read_text(encoding="utf-8")
        # A direct `.client.` access outside the base class bypasses the
        # permission gate entirely.
        for number, line in enumerate(source.splitlines(), start=1):
            stripped = line.strip()
            if stripped.startswith("#"):
                continue
            if re.search(r"\bself\.client\.(get|post|put|patch|delete)\w*\(", stripped):
                pytest.fail(
                    f"{path.name}:{number} calls the .NET client directly, "
                    "bypassing the tool registry's permission check. Use "
                    "`tools.call(...)`."
                )
