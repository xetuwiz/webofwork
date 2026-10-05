#!/usr/bin/env python3
"""Key-coverage check: does every response contain the keys the client reads?

parity_check.py diffs against the reference own field lists. That answers "does this
match Django?", which is not the question that matters -- the question is "does
this render?". A response can be byte-different from the reference and still be
correct, or identical and still break, if the React tree reads a key that is
absent. Almost every render crash in this codebase was that class:

    profile_photo   on the admin user table      (undefined -> no avatar)
    categories      on the client profile        (category_ids -> undefined)
    total_earning   on the freelancer earnings   (flat array -> undefined)
    skills[]        as strings                   (objects -> [object Object])

So this traces the client's own data flow and asserts coverage:

    api/*.js        axiosInstance.<verb>("/path")   -> endpoint
    *Queries.js     useQuery({queryFn: <fetcher>})  -> hook name
    *.jsx           use<Hook>()                    -> data alias, e.g. "profile"
    *.jsx           const { a, b } = profile        -> required keys

For each endpoint it probes the live API and reports any key the client reads
that the response does not contain. Extra keys are ignored on purpose.

Usage
-----
    python scripts/key_coverage.py --base https://localhost:5001 --token <jwt>
    python scripts/key_coverage.py --base ... --extract-only
    python scripts/key_coverage.py --base ... --json

Exit code is non-zero when the client reads a key the API never returns.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import ssl
import sys
import urllib.error
import urllib.request

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CLIENT_SRC = os.path.join(REPO, "client", "src")
API_PREFIX = "/api"

# axiosInstance.get("/path") / axiosInstance.get(`/path/${id}/`)
AXIOS_RE = re.compile(
    r"axiosInstance\.(get|post|patch|put|delete)\s*\(\s*[`\"']([^`\"']+)"
)
# export const fetchThing = async (...) => { ... }
EXPORT_FN_RE = re.compile(r"export\s+const\s+(\w+)\s*=")
# import { a, b } from "./module";
IMPORT_RE = re.compile(r"import\s*\{([^}]*)\}\s*from\s*[\"']([^\"']+)[\"']")
# useQuery({ ... queryFn: <name> ... })  and  useMutation({ mutationFn: <name> })
# Non-greedy to the first "})" would stop at a nested object (queryKey arrays
# with objects, nested options). Instead capture a generous window and let the
# queryFn regex find the binding.
HOOK_BLOCK_RE = re.compile(r"use(Query|Mutation)\s*\(\s*\{(.{0,600}?)\n\s*\}\s*\)", re.S)
QUERY_FN_RE = re.compile(r"(?:queryFn|mutationFn)\s*:\s*(?:async\s*)?\(?[^=;{)]*\)?\s*(?:=>\s*)?(\w+)")
# const { data: profile } = useProfile();   /   const {data: x} = useY(...)
USE_HOOK_RE = re.compile(
    r"(?:const|let|var)\s*\{([^}]*)\}\s*=\s*use(\w+)\s*\(", re.S
)
# const { a, b, c } = alias;      (destructuring the data object)
DESTRUCTURE_RE = re.compile(
    r"(?:const|let|var)\s*\{([^}]*)\}\s*=\s*(\w+)\s*[;\n]", re.S
)



def read(path: str) -> str:
    try:
        return open(path, encoding="utf-8").read()
    except (OSError, UnicodeDecodeError):
        return ""


def walk(ext: str):
    for root, _dirs, files in os.walk(CLIENT_SRC):
        for name in files:
            if name.endswith(ext):
                yield os.path.join(root, name)


def strip_query(path: str) -> str:
    path = path.split("?")[0]
    return re.sub(r"\$\{[^}]+\}", "{id}", path)


def build_index() -> dict:
    """fetcher name -> (verb, path), across every module that calls axios."""
    endpoints = {}
    for path in walk(".js"):
        text = read(path)
        if "axiosInstance" not in text:
            continue
        # Split into function scopes so each fetcher is matched to the axios
        # call it actually contains; a module usually holds several.
        parts = re.split(r"(?=\bexport\s+const\s+\w+\s*=)", text)
        for part in parts:
            m = EXPORT_FN_RE.search(part)
            if not m:
                continue
            call = AXIOS_RE.search(part)
            if call:
                endpoints[m.group(1)] = (
                    call.group(1).upper(), strip_query(call.group(2))
                )
    return endpoints


def build_hooks(endpoints: dict) -> dict:
    """hook name -> fetcher name, from useQuery/useMutation blocks."""
    hooks = {}
    for path in walk(".js"):
        text = read(path)
        if "useQuery" not in text and "useMutation" not in text:
            continue
        for m in HOOK_BLOCK_RE.finditer(text):
            fn = QUERY_FN_RE.search(m.group(2))
            if not fn or fn.group(1) not in endpoints:
                continue
            start = text.rfind("export const", 0, m.start())
            if start == -1:
                continue
            name_m = re.match(r"export\s+const\s+(\w+)", text[start:])
            if name_m:
                hooks.setdefault(name_m.group(1), fn.group(1))
    return hooks



def build_requirements(hooks: dict) -> dict:
    """(verb, path) -> keys the client reads off that response.

    Follows use<Hook>() in a component, learns which local alias holds the
    payload (const { data: profile } = useProfile()), then collects every
    property read off that alias -- including the row shape inside .map().
    """
    required = {}
    for path in walk(".jsx"):
        text = read(path)
        if "use" not in text:
            continue
        for m in USE_HOOK_RE.finditer(text):
            names, hook = m.group(1), m.group(2)
            if hook not in hooks:
                # The regex captures the name without its "use" prefix.
                hook = "use" + hook
            if hook not in hooks:
                continue
            alias = None
            for entry in names.split(","):
                if "data" in entry:
                    alias = entry.split(":")[-1].strip()
                    break
            if not alias:
                continue
            line_no = text[: m.start()].count("\n") + 1
            keys = collect_keys(text, alias)
            for ep in [hooks[hook]]:
                slot = required.setdefault(ep, {"keys": set(), "aliases": set(), "sites": []})
                slot["keys"] |= keys
                slot["aliases"].add(alias)
                slot["sites"].append(f"{os.path.basename(path)}:{line_no}")
    return required


def collect_keys(text: str, alias: str) -> set:
    """Property names read off `alias`: destructured, dotted, or inside .map()."""
    keys = set()
    for m in DESTRUCTURE_RE.finditer(text):
        if m.group(2) != alias:
            continue
        for part in m.group(1).split(","):
            name = part.strip().split(":")[0].split("=")[0].strip()
            if name and re.fullmatch(r"\w+", name):
                keys.add(name)
    for pattern in (re.escape(alias) + r"\.(\w+)", re.escape(alias) + r"\?\.\s*(\w+)"):
        for m in re.finditer(pattern, text):
            keys.add(m.group(1))
    # alias.map((x) => x.field) -- the row shape matters as much as the envelope
    for m in re.finditer(re.escape(alias) + r"\.map\s*\(\s*\(?\s*\w+\s*\)?\s*=>", text):
        for f in re.finditer(r"\b\w+\.(\w+)", text[m.end(): m.end() + 400]):
            keys.add(f.group(1))
    # A paged payload is usually re-bound before use:
    #   const rows = alias?.results || [];  then  rows.map(r => r.title)
    # Follow that one hop so nested reads are not lost.
    for m in re.finditer(
        r"(?:const|let|var)\s+(\w+)\s*=\s*" + re.escape(alias) + r"\s*\??\.\s*(\w+)", text
    ):
        child, field = m.group(1), m.group(2)
        keys.add(field)
        for m2 in re.finditer(r"\b" + re.escape(child) + r"\.(\w+)", text):
            keys.add(m2.group(1))
    return {k for k in keys if k not in ("length", "map", "filter", "then", "catch", "forEach", "some", "slice", "find", "sort", "reduce")}


def fetch_json(base: str, verb: str, path: str, token):
    """(payload, status). Path keeps its {id} placeholder; caller substitutes."""
    url = base.rstrip("/") + API_PREFIX + (path if path.startswith("/") else "/" + path)
    req = urllib.request.Request(url, method=verb)
    req.add_header("Accept", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    ctx = ssl._create_unverified_context()
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
            return json.loads(resp.read().decode("utf-8")), resp.status
    except urllib.error.HTTPError as exc:
        try:
            return json.loads(exc.read().decode("utf-8")), exc.code
        except Exception:  # noqa: BLE001
            return None, exc.code
    except Exception:  # noqa: BLE001
        return None, 0


def keys_of(payload, alias_hint: str):
    """Actual keys at the level the component consumes, plus row keys for lists."""
    if payload is None:
        return set(), None
    node = payload
    # paged envelope -> results[]
    if isinstance(node, dict) and isinstance(node.get("results"), list):
        rows = node["results"]
        row_keys = set(rows[0].keys()) if rows and isinstance(rows[0], dict) else set()
        return set(node.keys()) | row_keys, "paged"
    if isinstance(node, list):
        return (set(node[0].keys()) if node and isinstance(node[0], dict) else set()), "list"
    if isinstance(node, dict):
        return set(node.keys()), "object"
    return set(), None


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--base", help="API base, e.g. https://localhost:5001")
    ap.add_argument("--token", help="JWT")
    ap.add_argument("--extract-only", action="store_true", help="print the derived map and exit")
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    args = ap.parse_args()

    endpoints = build_index()
    hooks = build_hooks(endpoints)
    by_fetcher = build_requirements(hooks)
    # key the map by endpoint so probes hit a real route
    required = {}
    for fetcher, slot in by_fetcher.items():
        ep = endpoints.get(fetcher)
        if not ep:
            continue
        merged = required.setdefault(ep, {"keys": set(), "aliases": set(), "sites": []})
        merged["keys"] |= slot["keys"]
        merged["aliases"] |= slot["aliases"]
        merged["sites"] += slot["sites"]

    if args.extract_only or not args.base:
        for ep, slot in sorted(required.items()):
            verb, path = ep
            print(f"{verb:6} {path}")
            print(f"       aliases: {', '.join(sorted(slot['aliases']))}")
            print(f"       reads   : {', '.join(sorted(slot['keys'])) or '(none detected)'}")
            print(f"       sites   : {', '.join(slot['sites'][:4])}")
        print(f"\n{len(endpoints)} fetchers, {len(hooks)} hooks, "
              f"{len(required)} endpoints with detected reads.")
        return 0

    findings = []
    for ep, slot in sorted(required.items()):
        verb, path = ep
        # A detail endpoint needs a real row id; without one the probe returns an
        # app-level NotFound and says nothing about key coverage. Report it as
        # SKIP rather than a false failure.
        if "{id}" in path:
            findings.append({
                "endpoint": f"{verb} {path}", "shape": "detail", "status": None,
                "required": sorted(slot["keys"]), "actual": [], "missing": [],
                "sites": slot["sites"], "skipped": "needs a real id",
            })
            continue
        payload, status = fetch_json(args.base, verb, path, args.token)
        if payload is None:
            findings.append({"endpoint": f"{verb} {path}", "reason": f"HTTP {status}",
                             "keys": [], "missing": [], "sites": slot["sites"]})
            continue
        actual, kind = keys_of(payload, next(iter(slot["aliases"]), ""))
        # An empty result set exposes only the envelope, so row-level keys cannot
        # be checked. That is a data gap, not a shape failure -- report it as
        # unverifiable rather than claiming the rows are missing those keys.
        if kind == "paged" and not payload.get("results"):
            findings.append({
                "endpoint": f"{verb} {path}", "shape": kind, "status": status,
                "required": sorted(slot["keys"]), "actual": sorted(actual),
                "missing": [], "sites": slot["sites"],
                "skipped": "no rows to check row keys",
            })
            continue
        # A nested alias (data.results.contracts) is handled by the caller alias
        # being a direct member; if nothing matched, report the observed keys.
        missing = sorted(k for k in slot["keys"] if k not in actual)
        findings.append({
            "endpoint": f"{verb} {path}", "shape": kind, "status": status,
            "required": sorted(slot["keys"]), "actual": sorted(actual),
            "missing": missing, "sites": slot["sites"],
        })

    if args.json:
        print(json.dumps(findings, indent=2))
    else:
        for f in findings:
            if "reason" in f:
                print(f"  ??   {f['endpoint']:52} {f['reason']}")
            elif f.get("skipped"):
                print(f"  skip {f['endpoint']:52} {f['skipped']} ({len(f['required'])} keys read)")
            elif f["missing"]:
                print(f"  MISS {f['endpoint']:52} missing {f['missing']}")
                print(f"       read at {', '.join(f['sites'][:3])}")
            else:
                print(f"  ok   {f['endpoint']:52} {len(f['required'])} keys covered")
        bad = [f for f in findings if f.get("missing") or "reason" in f]
        skipped = [f for f in findings if f.get("skipped")]
        print()
        print(f"{len(bad)} endpoints with unreadable or uncovered keys, "
              f"{len(skipped)} skipped (detail routes), of {len(findings)} traced.")
    return 1 if any(f.get("missing") or "reason" in f for f in findings) else 0


if __name__ == "__main__":
    sys.exit(main())
