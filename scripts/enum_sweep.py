#!/usr/bin/env python3
"""Enum/status literal sweep: do the API's values land in the sets the client checks?

Layers 1 and 2 answer "does the route resolve" and "does the response carry the
keys". Neither notices when a key is present but its *value* is in a vocabulary
the client never compares against, which is a silent class of bug:

    status === "in_progress"          but the API emits "InProgress"
    notification_type in getStyleMap  but the API emits "WorkDelivered"
    reason === "Unlimited Revisions"  but the API expects "unlimited_revisions"

Those render as a default branch: a grey badge, a missing icon, a filter that
never matches. Nothing crashes, so nothing gets noticed.

This extracts, straight from the client:
  * <field> === "<literal>"            strict comparisons
  * case "<literal>":                  switch arms
  * <key>: { ... } inside a getStyle*   lookup-table vocabularies

groups them by field, then hits the matching endpoint, collects the values the
API actually returns, and reports any value the client has no branch for.

Usage
-----
    python scripts/enum_sweep.py --extract-only
    python scripts/enum_sweep.py --base https://localhost:5001 --token <jwt>
    python scripts/enum_sweep.py --base ... --json

Exit code is non-zero when the API returns a value the client cannot render.
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

# field === "literal"   /   field !== "literal"
EQ_RE = re.compile(r"([.\w?]+)\s*[!=]==?\s*[\"']([^\"']+)[\"']")
# case "literal":
CASE_RE = re.compile(r"case\s+[\"']([^\"']+)[\"']\s*:")
# The const getStyleMap = (type) => { const maps = { key: {...}, ... } }
STYLE_MAP_RE = re.compile(
    r"(?:const|let|var)\s+(get\w*Style\w*|getStyleMap|\w*Styles\w*)\s*=\s*"
    r"\(\s*(\w+)\s*\)\s*=>\s*\{(.{0,4000}?)\n\s*\};", re.S
)
# Keys of a lookup table: identifier: { icon: ... }  (exclude default/keys)
TABLE_KEY_RE = re.compile(r"^\s*([a-zA-Z_][\w]*)\s*:\s*\{", re.M)

# A module-level status table, which is how most of the client keeps its
# vocabulary:  const styles = { offered: {...}, active: {...} }
# The field it governs is not in the table, so it is attributed to "status" --
# these tables are overwhelmingly contract/payment status maps.
STATUS_TABLE_RE = re.compile(
    r"(?:const|let|var)\s+\w*(?:STYLE|STATUS|Map|STYLES|styles|status)\w*\s*=\s*\{(.{0,3000}?)\n\s*\};",
    re.S,
)

# Fields whose literals are UI vocabulary rather than API values. Comparing
# these to server data would produce nonsense findings.
NOISE = {
    "key", "modal", "type", "value", "id", "e", "ev", "evt", "event", "name",
    "variant", "size", "color", "tone", "icon", "title", "label", "text",
    "signatureMode", "changeType", "activeTab", "range", "statusClass",
    "action", "activeModal", "subType", "selectedPackage", "invitationStatus",
    # `typeof x.category === "object"` is a type guard, not an API vocabulary.
    "category", "data", "error", "pathname", "opener", "tier", "skill",
    "deliverable_type", "revisions", "pkg", "skillId",
}

# Bare `status === "x"` is heavily overloaded in the client: most of those
# comparisons are local UI state (form validity, async lifecycle), not API data.
# A vocabulary is only trustworthy when it looks like server data, so require
# either several snake_case values or a known field name.
UI_ONLY_STATUS = {
    "invalid", "loading", "pending", "resolved", "success", "valid", "error",
}
API_FIELDS = {
    "status", "notification_type", "payment_status", "dispute_status",
    "current_role", "pricing_type", "reason", "role", "availability",
}

# "status" is not one vocabulary. A Contract has one status domain, a Project
# another, a Payment another, and the client compares each against a different
# set. Pooling them makes the API look wrong when it is right. Map each endpoint
# to the domain it belongs to and only compare within it.
STATUS_DOMAINS = {
    "notification_type": ["/communication/notifications/"],
    "pricing_type": ["/projects/", "/projects/browse/", "/profile/freelancers-list/"],
    "current_role": ["/auth/me/"],
    "payment_status": ["/payments/platform-transactions/"],
    "dispute_status": ["/payments/platform-transactions/", "/contracts/admin/disputes/"],
    "dispute_raised_by": ["/payments/platform-transactions/"],
    # Contract status is the one the client actually branches on in list views.
    "status": ["/contracts/"],
}


def read(path):
    try:
        return open(path, encoding="utf-8").read()
    except (OSError, UnicodeDecodeError):
        return ""


def walk(ext):
    for root, _d, files in os.walk(CLIENT_SRC):
        for name in files:
            if name.endswith(ext):
                yield os.path.join(root, name)


def fetch_json(base, path, token):
    url = base.rstrip("/") + API_PREFIX + (path if path.startswith("/") else "/" + path)
    req = urllib.request.Request(url, method="GET")
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


def rows_of(payload):
    """Flatten a payload into the list of row dicts it exposes."""
    if isinstance(payload, dict):
        for key in ("results", "contracts", "data", "items"):
            if isinstance(payload.get(key), list):
                return [r for r in payload[key] if isinstance(r, dict)]
        if "contracts" in payload and isinstance(payload["contracts"], dict):
            return []
        return [payload]
    if isinstance(payload, list):
        return [r for r in payload if isinstance(r, dict)]
    return []


def observed(base, token, endpoints, field, depth=0):
    """Distinct values the API returns for `field`, across every endpoint."""
    seen = set()
    if depth > 2:
        return seen
    for path in endpoints:
        payload, status = fetch_json(base, path, token)
        if payload is None or status >= 400:
            continue
        for row in rows_of(payload):
            if field in row and isinstance(row[field], (str, int)) and not isinstance(row[field], bool):
                seen.add(str(row[field]))
            # Freelancer earnings nests contracts under results.contracts.
            nested = row.get("contracts")
            if isinstance(nested, dict) and field in nested:
                seen.add(str(nested[field]))
    return seen


def walk(ext):
    for root, _d, files in os.walk(CLIENT_SRC):
        for name in files:
            if name.endswith(ext):
                yield os.path.join(root, name)


def collect():
    """field -> {"literals": set, "sites": [...], "kind": str}."""
    vocab = {}

    def add(field, literal, site, kind):
        if field in NOISE or not literal:
            return
        slot = vocab.setdefault(field, {"literals": set(), "sites": [], "kind": kind})
        slot["literals"].add(literal)
        if site not in slot["sites"]:
            slot["sites"].append(site)

    for path in walk(".jsx"):
        text = read(path)
        base = os.path.basename(path)
        for i, line in enumerate(text.splitlines(), 1):
            for m in EQ_RE.finditer(line):
                raw = m.group(1)
                # Keep only the last segment: `n.notification_type` and
                # `contract?.status` describe the same field as `notification_type`
                # and `status`, and the API returns the bare name.
                field = raw.lstrip("?.[").rstrip("]?.").split(".")[-1]
                if field:
                    add(field, m.group(2), f"{base}:{i}", "eq")

    # Lookup tables: the accepted vocabulary is the set of keys. The table is
    # parameterised (`(type) => { const maps = { key: {...} } }`), so the local
    # parameter name is useless; resolve it from the call site instead --
    # getStyleMap(n.notification_type) tells us the field is notification_type.
    for path in walk(".jsx"):
        text = read(path)
        base = os.path.basename(path)
        for m in STYLE_MAP_RE.finditer(text):
            param, body, fn = m.group(2), m.group(3), m.group(1)
            if "=>" in body or "switch" in body:
                continue
            keys = {k for k in TABLE_KEY_RE.findall(body) if k != "default"}
            if not keys:
                continue
            # Find getStyleMap(<expr>) and take the field out of <expr>.
            field = param
            for call in re.finditer(re.escape(fn) + r"\s*\(\s*([\w?.]+)", text):
                field = call.group(1).lstrip("?.").split(".")[-1]
                break
            line = text[: m.start()].count("\n") + 1
            for k in keys:
                add(field, k, f"{base}:{line}", "table")

    # Module-level status tables: const styles = { offered: {...}, ... }
    for path in walk(".jsx"):
        text = read(path)
        base = os.path.basename(path)
        for m in STATUS_TABLE_RE.finditer(text):
            body = m.group(1)
            if "=>" in body or "function" in body:
                continue
            keys = {k for k in TABLE_KEY_RE.findall(body) if k != "default"}
            # A status vocabulary is all-lowercase identifiers, not component maps.
            keys = {k for k in keys if k.islower() or "_" in k}
            if len(keys) < 2:
                continue
            line = text[: m.start()].count("\n") + 1
            for k in keys:
                add("status", k, f"{base}:{line}", "table")

    # Switch arms: the compared values belong to the switch subject.
    for path in walk(".jsx"):
        text = read(path)
        base = os.path.basename(path)
        for m in re.finditer(
            r"switch\s*\(\s*([\w?.]+)\s*\)\s*\{(.{0,2000}?)\n\s*\}", text, re.S
        ):
            subject = m.group(1).lstrip("?.").split(".")[-1]
            line = text[: m.start()].count("\n") + 1
            for c in CASE_RE.finditer(m.group(2)):
                add(subject, c.group(1), f"{base}:{line}", "case")

    # Drop form/async literals that leaked into `status`; what remains is the
    # vocabulary the client expects from the server for that field.
    for field, slot in list(vocab.items()):
        if slot["kind"] == "eq" and field not in API_FIELDS:
            if slot["literals"] <= UI_ONLY_STATUS:
                vocab.pop(field)
    return vocab


# Endpoints worth probing per field. Each is a GET the client already calls.
ENDPOINTS = [
    "/communication/notifications/",
    "/projects/",
    "/projects/browse/",
    "/contracts/",
    "/profile/freelancers-list/",
    "/payments/platform-transactions/",
    "/contracts/admin/disputes/",
    "/projects/invitations/sent/",
    "/projects/invitations/received/",
    "/profile/me/",
    "/auth/me/",
    "/payments/freelancer/transaction-history/",
]


def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--base")
    ap.add_argument("--token")
    ap.add_argument("--extract-only", action="store_true")
    ap.add_argument("--json", action="store_true")
    args = ap.parse_args()

    vocab = collect()
    if args.extract_only or not args.base:
        for field, slot in sorted(vocab.items()):
            print(f"{field:22} ({slot['kind']:5}) {sorted(slot['literals'])}")
            print(f"{'':22} {', '.join(slot['sites'][:3])}")
        print(f"\n{len(vocab)} vocabularies extracted from the client.")
        return 0

    findings = []
    for field, slot in sorted(vocab.items()):
        # Compare within the field's own domain, not across every endpoint.
        endpoints = STATUS_DOMAINS.get(field) or ENDPOINTS
        got = observed(args.base, args.token, endpoints, field)
        if not got:
            findings.append({"field": field, "expected": sorted(slot["literals"]),
                             "observed": [], "unknown": [], "sites": slot["sites"],
                             "note": "no rows exposed this field"})
            continue
        unknown = sorted(got - set(slot["literals"]))
        findings.append({
            "field": field, "expected": sorted(slot["literals"]),
            "observed": sorted(got), "unknown": unknown, "sites": slot["sites"],
            "note": None,
        })

    if args.json:
        print(json.dumps(findings, indent=2))
    else:
        for f in findings:
            if f["note"]:
                print(f"  --   {f['field']:22} {f['note']}")
            elif f["unknown"]:
                print(f"  UNK  {f['field']:22} API returns {f['unknown']}")
                print(f"       client only knows {f['expected']}")
                print(f"       at {', '.join(f['sites'][:3])}")
            else:
                print(f"  ok   {f['field']:22} {f['observed']} all known")
        bad = [f for f in findings if f["unknown"]]
        print()
        print(f"{len(bad)} fields where the API emits a value the client cannot "
              f"render, of {len(findings)} vocabularies.")
    return 1 if any(f["unknown"] for f in findings) else 0


if __name__ == "__main__":
    sys.exit(main())
