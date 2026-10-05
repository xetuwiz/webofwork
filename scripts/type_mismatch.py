#!/usr/bin/env python3
"""Find client/serializer type mismatches that silently 400 at runtime.

Static analysis of the React client against the ASP.NET DTOs, focused on the
serializer constraints declared in Program.cs AddJsonOptions:

  * DecimalAsStringJsonConverter  -> every decimal / decimal? field must arrive
    as a JSON **string**. A bare number fails model binding with
    "The JSON value could not be converted to System.Nullable`1[System.Decimal]".
  * IsoDateTimeJsonConverter      -> every DateTime must arrive as a string.
  * JsonStringEnumConverter       -> enums arrive as snake_case strings.

That is exactly the class of bug behind pricing_type (int DTO vs "fixed"
string) and fixed_price (number sent, string required). Those produce a 400
before validation runs, so the message points at the wrong thing.

Method
------
1. Parse every public property out of the C# DTOs -> {dto: {snake_key: type}}.
2. Collect the keys the client actually sends, and whether the value is a
   bare literal (number/boolean) or a string.
3. Report a finding when a literal is sent for a key the DTO types as
   decimal/DateTime/enum.

This is heuristic and deliberately reports *candidates* for review -- it does
not try to be a type checker. Absence of findings is not proof of absence.
"""

from __future__ import annotations

import re
import sys
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
DTO_DIR = ROOT / "server" / "WebOfWork.Api" / "DTOs"
CLIENT_SRC = ROOT / "client" / "src"

# string-only scalars, per Program.cs converters
STRING_ONLY = ("decimal", "datetime", "dateonly", "timespan")

CS_PROP = re.compile(
    r"public\s+(?:required\s+)?"
    r"(?P<type>[A-Za-z_][\w<>,\[\]\?\.]*)\s+"
    r"(?P<name>[A-Za-z_]\w*)\s*\{\s*get;\s*set;\s*\}"
)

# client object-literal / shorthand values, e.g.  price: 500   or  "price": 500
JS_KEY = re.compile(
    r"""(?P<key>[A-Za-z_]\w*)\s*:\s*(?P<val>[-+]?\d+(?:\.\d+)?|true|false|null)"""
)


def snake(name: str) -> str:
    s1 = re.sub(r"(.)([A-Z][a-z]+)", r"\1_\2", name)
    return re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", s1).lower()


def collect_dto_fields() -> dict[str, set[str]]:
    """Map snake_case field name -> set of C# types that use it."""
    found: dict[str, set[str]] = defaultdict(set)
    for path in DTO_DIR.rglob("*.cs"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in CS_PROP.finditer(text):
            ctype, name = m.group("type"), m.group("name")
            base = ctype.rstrip("?").lower()
            found[snake(name)].add(base)
    return dict(found)


def collect_client_literals() -> list[tuple[Path, int, str, str]]:
    out = []
    for path in CLIENT_SRC.rglob("*.js*"):
        for i, line in enumerate(
            path.read_text(encoding="utf-8", errors="replace").splitlines(), 1
        ):
            for m in JS_KEY.finditer(line):
                out.append((path, i, m.group("key"), m.group("val")))
    return out


def collect_value_as_number() -> list[tuple[Path, int, str]]:
    """register("field", { valueAsNumber: true }) -> field is a JS number.

    This is the real risk pattern. react-hook-form data is usually submitted via
    a spread ({ ...data }), so the key never appears as a literal and a
    literal-scan reports nothing -- yet the value is still a bare number in the
    JSON. That is precisely how fixed_price got sent unquoted.
    """
    out = []
    pat = re.compile(r'register\(\s*["\'](?P<key>[\w.]+)["\'][^)]*valueAsNumber')
    for path in CLIENT_SRC.rglob("*.jsx"):
        text = path.read_text(encoding="utf-8", errors="replace")
        for i, line in enumerate(text.splitlines(), 1):
            for m in pat.finditer(line):
                out.append((path, i, m.group("key")))
    return out


def collect_wrapped(path: Path) -> set[str]:
    """Keys this file already String()-wraps (i.e. already handled).

    Covers the direct forms ({ price: String(x) }, data.price = String(x)) and
    the loop form used in AddProject/EditProject:
        for (const k of ["fixed_price", "min_budget", ...]) { ... String(...); }
    """
    text = path.read_text(encoding="utf-8", errors="replace")
    wrapped = set()
    # loop form: for (const k of ["a", "b", ...])
    for m in re.finditer(r'for\s*\(\s*const\s+\w+\s+of\s*\[([^\]]*)\]', text):
        for k in re.finditer(r'["\'](\w+)["\']', m.group(1)):
            wrapped.add(k.group(1))
    for m in re.finditer(r'["\'](\w+)["\']\s*\]\s*=\s*String\(', text):
        wrapped.add(m.group(1))
    for m in re.finditer(r'(\w+)\s*:\s*String\(', text):
        wrapped.add(m.group(1))
    for m in re.finditer(r'(\w+)\.(\w+)\s*=\s*String\(', text):
        wrapped.add(m.group(2))
    return wrapped


def main() -> int:
    fields = collect_dto_fields()
    if not fields:
        print("No DTO properties parsed - did DTO_DIR move?", file=sys.stderr)
        return 2

    def is_string_only(key: str) -> str | None:
        types = fields.get(key)
        if not types:
            return None
        joined = " ".join(sorted(types))
        for s in STRING_ONLY:
            if s in joined:
                return joined
        return None

    sensitive = {k for k in fields if is_string_only(k)}
    print(f"DTO fields requiring a JSON string: {len(sensitive)}")

    findings = []

    # 1. valueAsNumber inputs bound to a string-only DTO field
    for path, line, key in collect_value_as_number():
        joined = is_string_only(key)
        if not joined:
            continue
        base = key.split(".")[-1]
        if base in collect_wrapped(path):
            continue
        findings.append((path, line, key, joined, "register(..., { valueAsNumber: true })"))

    # 2. bare numeric literals for string-only DTO fields
    for path, line, key, val in collect_client_literals():
        joined = is_string_only(key)
        if not joined or val == "null":
            continue
        if key in collect_wrapped(path):
            continue
        findings.append((path, line, key, joined, f"literal {key}: {val}"))

    seen, uniq = set(), []
    for f in findings:
        sig = (f[0], f[2], f[4])
        if sig in seen:
            continue
        seen.add(sig)
        uniq.append(f)

    if not uniq:
        print("\nNo mismatches found by either heuristic.")
        print("Caveat: this is static and heuristic. Fields assembled indirectly")
        print("(e.g. axios FormData, or values computed by helper functions) are")
        print("not covered -- exercise those in the browser.")
        return 0

    print(f"\n{len(uniq)} candidate mismatch(es):\n")
    for path, line, key, joined, why in uniq:
        rel = path.relative_to(ROOT)
        print(f"  {rel}:{line}")
        print(f"    {why}")
        print(f"    DTO type: {joined}  -> must be a JSON string\n")

    print("Fix at the request boundary:")
    print("  for (const k of ['price', ...]) body[k] = String(body[k]);")
    return 1


if __name__ == "__main__":
    sys.exit(main())
