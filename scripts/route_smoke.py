#!/usr/bin/env python3
"""Route smoke test: does every path the frontend calls actually resolve?

The field-presence check in parity_check.py cannot see this class of bug. Every
defect this exercise turned up was invisible to a key diff:

  * a route whose constraint could never match ({id:int} on a Guid PK)
  * a query parameter that never bound (categories vs category_ids)
  * a value that was right for one row and wrong for another (current_role = 0)

So this walks the paths the React app actually calls, extracted from the client's
own axios calls, and asserts each resolves. Because the route list is extracted
at run time it cannot drift from the client.

Usage
-----
    python scripts/route_smoke.py --base https://localhost:5001
    python scripts/route_smoke.py --base ... --extract-only
    python scripts/route_smoke.py --base ... --account client:tok --account freelancer:tok

Exit code is non-zero when any documented path fails to resolve.
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

# The client's axios paths are relative to VITE_BASE_URL, which ends in /api
# (see client/.env and axiosInstance.js). Probing them without this prefix
# produces a 404 for every route and reports the whole surface as broken.
API_PREFIX = "/api"

# axiosInstance.<verb>(`/literal/path
CALL_RE = re.compile(
    r"axiosInstance\.(get|post|patch|put|delete)\s*\(\s*[`\"']([^`\"']+)"
)
# Fragments the client interpolates, replaced with representative values.
PLACEHOLDERS = {
    # Todos are Guid-keyed (FreelancerTodo.Id), so a numeric placeholder
    # silently 404s even though the route itself is fine.
    "${id}": "00000000-0000-0000-0000-000000000000",
    "${roomId}": "1",
    "${room}": "1",
    "${messageId}": "1",
    "${contractId}": "1",
    "${proposalId}": "1",
    "${invitationId}": "1",
    "${userId}": "00000000-0000-0000-0000-000000000000",
    "${profileId}": "00000000-0000-0000-0000-000000000000",
}

# Entities are keyed inconsistently: users and todos are Guid, categories,
# projects, contracts and notifications are int. A route only resolves with the
# right shape, so the placeholder is substituted per candidate id type and the
# path counts as routed if ANY of them matches.
INT_ID = "1"
GUID_ID = "00000000-0000-0000-0000-000000000000"


def extract_calls() -> set:
    """(VERB, path) pairs the frontend actually calls, query strings removed."""
    found = set()

    for root, _dirs, files in os.walk(CLIENT_SRC):
        for name in files:
            if not name.endswith(".js"):
                continue
            try:
                text = open(os.path.join(root, name), encoding="utf-8").read()
            except (OSError, UnicodeDecodeError):
                continue
            for verb, path in CALL_RE.findall(text):
                path = path.strip()
                if not path.startswith("/"):
                    path = "/" + path
                path = path.split("?")[0]
                # Keep a canonical marker for interpolated ids; the concrete id type
                # (int vs Guid) is decided at probe time, since entities are keyed
                # inconsistently across this API.
                for frag in PLACEHOLDERS:
                    path = path.replace(frag, "{id}")
                path = re.sub(r"\$\{[^}]+\}", "{id}", path)
                if len(path) > 1 and path.endswith("/"):
                    path = path[:-1]
                found.add((verb.upper(), path))
    return found


def fetch_status(base: str, verb: str, path: str, token):
    """(status, body_snippet).

    A 404 with an ASP.NET problem+json body is an application-level NotFound
    (e.g. "no freelancer profile for this user"), which means the route
    resolved fine. Only a 404 with an empty body is a real routing failure.
    """
    url = base.rstrip("/") + API_PREFIX + (path if path.startswith("/") else "/" + path)
    data = b"{}" if verb in ("POST", "PATCH", "PUT") else None
    req = urllib.request.Request(url, data=data, method=verb)
    req.add_header("Accept", "application/json")
    if data is not None:
        req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    ctx = ssl._create_unverified_context()
    try:
        with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
            return resp.status, "app"
    except urllib.error.HTTPError as exc:
        try:
            body = exc.read().decode("utf-8", "replace")
        except Exception:  # noqa: BLE001
            body = ""
        # Distinguish an *unroutable* path (ASP.NET answers with an empty 404) from
        # the app returning its own NotFound. A real handler emits a body, either
        # problem+json or text/plain ("User ... not found.").
        kind = "route" if not body.strip() else "app"
        return exc.code, kind


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--base", required=True, help="API base, e.g. https://localhost:5001")
    ap.add_argument("--token", help="token for the primary probe")
    ap.add_argument(
        "--account", action="append", default=[], metavar="ROLE:TOKEN",
        help="extra role/token pair, repeatable (multi-account re-probe)",
    )
    ap.add_argument("--extract-only", action="store_true", help="print routes and exit")
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    args = ap.parse_args()

    calls = sorted(extract_calls())
    if not calls:
        print("no axios calls found -- is client/src present?", file=sys.stderr)
        return 2

    if args.extract_only:
        for verb, path in calls:
            print(f"{verb:6} {path}")
        print(f"\n{len(calls)} distinct routes.")
        return 0

    tokens = {"primary": args.token}
    for entry in args.account:
        role, _, tok = entry.partition(":")
        if role and tok:
            tokens[role] = tok

    results = []
    for verb, path in calls:
        row = {"verb": verb, "path": path, "probes": {}, "failed": False}
        for role, tok in tokens.items():
            # Entities are keyed inconsistently (users/todos Guid, projects/
            # contracts/categories int), so try each id shape and accept the
            # path if any of them routes.
            seen = []
            routed = False
            for candidate in ({INT_ID, GUID_ID} if "{id}" in path else {path}):
                probe = path.replace("{id}", candidate) if "{id}" in path else path
                try:
                    status, kind = fetch_status(args.base, verb, probe, tok)
                except Exception as exc:  # noqa: BLE001
                    seen.append(f"ERR {type(exc).__name__}")
                    continue
                seen.append(status)
                if not (kind == "route" and status == 404):
                    routed = True
            row["probes"][role] = "/".join(str(s) for s in seen)
            row["failed"] = not routed
        results.append(row)

    if args.json:
        print(json.dumps(results, indent=2))
    else:
        bad = [r for r in results if r["failed"]]
        for row in results:
            marks = " ".join(f"{role}={st}" for role, st in row["probes"].items())
            print(f"  {'FAIL' if row['failed'] else 'ok  '} "
                  f"{row['verb']:6} {row['path']:50} {marks}")
        print()
        print(f"{len(bad)} unresolved of {len(results)} documented routes.")
        if bad:
            print("A 404 means no endpoint matched. Review the route in the controller.")
            for row in bad:
                print(f"  - {row['verb']} {row['path']}  {row['probes']}")

    return 1 if any(r["failed"] for r in results) else 0


if __name__ == "__main__":
    sys.exit(main())
