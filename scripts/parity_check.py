"""
the reference <-> WebOfWork response-compatibility check.

Self-contained: the expected field lists are inlined below. This script reads
nothing from disk, so it keeps working if the reference checkout is
deleted.

Provenance
----------
Each FIELD list was transcribed from the reference serializers while a reference
copy was available. When re-validating against the reference, re-read these from
server/**/serializers.py and update the list in the same commit:

    ProjectListSerializer            projects/serializers.py    Meta.fields
    InvitationSerializer             projects/serializers.py    Meta.fields
    ProposalsListSerializer         projects/serializers.py    Meta.fields
    ContractListSerializer          contracts/serializers.py   Meta.fields
    AdminTransactionSerializer      payments/serializers.py    Meta.fields
    ReviewSerializer                profiles/serializers.py    Meta.fields
    FreelancerReviewListSerializer  profiles/serializers.py    Meta.fields
    FreelancerToDoSerializer        profiles/serializers.py    Meta.fields
    NotificationSerializer          communication/serializers.py Meta.fields
    MessageSerializer               communication/serializers.py Meta.fields

the reference DRF PageNumberPagination always emits count/next/previous/results
alongside each result list; those are added automatically, not listed below.

Usage
-----
    python scripts/parity_check.py --base https://localhost:5001 --token <jwt>
    python scripts/parity_check.py --list

Exit code is non-zero when any required field is missing, so it can gate a build.
"""

from __future__ import annotations

import argparse
import json
import ssl
import sys
import urllib.error
import urllib.request

# DRF PageNumberPagination envelope, present on every "paged" endpoint.
DRF_PAGE_FIELDS = {"count", "next", "previous", "results"}

# (serializer, fields) - transcribed from the reference Meta.fields.
PROJECT_LIST = [
    "id", "title", "description", "category_name", "pricing_type", "fixed_price",
    "min_budget", "max_budget", "delivery_days", "status", "created_at",
    "skills", "contract_id", "proposals_count", "category",
]

INVITATION = [
    "id", "client", "freelancer", "project", "package", "message", "status",
    "created_at", "client_name", "project_title", "package_type",
    "freelancer_name", "freelancer_id", "freelancer_image",
    "freelancer_tagline", "package_amount", "contract_info",
]

PROPOSALS_LIST = [
    "id", "freelancer_id", "freelancer_name", "freelancer_photo",
    "freelancer_tagline", "freelancer_ratings", "cover_letter", "bid_amount",
    "delivery_days", "status", "created_at", "contract_info",
]

CONTRACT_LIST = [
    "id", "project_title", "freelancer_name", "status", "total_amount",
    "client_signed_at", "freelancer_earnings",
]

ADMIN_TRANSACTION = [
    "id", "payment_id", "client_name", "freelancer_name", "amount_total",
    "platform_fee", "date", "status",
]

REVIEW = [
    "id", "reviewer", "reviewer_name", "reviewer_photo", "reviewee",
    "review_type", "rating", "comment", "contract", "created_at",
]

FREELANCER_REVIEW_LIST = [
    "id", "rating", "comment", "created_at", "reviewer_name",
    "reviewer_photo", "project_title",
]

FREELANCER_TODO = ["id", "todo", "is_completed"]

NOTIFICATION = [
    "id", "notification_type", "message", "is_read", "created_at", "related_id",
]

MESSAGE = [
    "id", "sender", "sender_role", "text", "is_read", "created_at", "is_me",
]

# Freelancer earnings: note `results` is an OBJECT here, not a list of rows.
# EarningsOverview.jsx reads results.total_earning, results.total_projects and
# then results.contracts.map(...). Each contract row carries contract status.
FREELANCER_EARNINGS = [
    "total_earning", "total_projects", "contracts",
]

FREELANCER_EARNINGS_CONTRACT = [
    "id", "project_title", "freelancer_name", "status", "total_amount",
    "client_signed_at", "freelancer_earnings",
]

# Admin dashboard, transcribed from AdminDashboardView in
# ref/the reference-main/server/adminpanel/views.py. That view returns a plain dict
# (not a serializer), so the key set is exactly the literal below.
DASHBOARD_STATS = [
    "total_users", "users_change", "total_projects", "projects_change",
    "active_contracts", "contracts_change", "total_proposals",
    "proposals_change", "total_disputes", "dispute_change",
    "platform_revenue", "revenue_change", "pending_withdrawals",
    "pending_withdrawals_count",
]

# AdminRevenueChartView returns [{"month": "Jan", "revenue": 0.0}, ...] -- two keys.
REVENUE_CHART = ["month", "revenue"]

# AdminProposalsChartView returns [{"date": "Jan-30", "projects": 5, "proposals": 12}].
PROPOSALS_CHART = ["date", "projects", "proposals"]

# path: the WebOfWork route. kind: paged | list | object.
ENDPOINTS: list[dict] = [
    {"name": "ProjectListSerializer", "fields": PROJECT_LIST,
     "path": "/api/projects/", "kind": "paged"},
    {"name": "InvitationSerializer", "fields": INVITATION,
     "path": "/api/invitations/sent", "kind": "paged"},
    {"name": "ProposalsListSerializer", "fields": PROPOSALS_LIST,
     "path": "/api/proposals/project/{id}", "kind": "paged"},
    {"name": "ContractListSerializer", "fields": CONTRACT_LIST,
     "path": "/api/contracts/", "kind": "list"},
    {"name": "AdminTransactionSerializer", "fields": ADMIN_TRANSACTION,
     "path": "/api/payments/platform-transactions", "kind": "paged"},
    {"name": "ReviewSerializer", "fields": REVIEW,
     "path": "/api/profile/reviews/client/{id}/", "kind": "paged"},
    {"name": "FreelancerReviewListSerializer", "fields": FREELANCER_REVIEW_LIST,
     "path": "/api/profile/freelancers/{id}/reviews/", "kind": "list"},
    {"name": "FreelancerToDoSerializer", "fields": FREELANCER_TODO,
     "path": "/api/profile/todos/", "kind": "list"},
    {"name": "NotificationSerializer", "fields": NOTIFICATION,
     "path": "/api/communication/notifications/", "kind": "list"},
    {"name": "MessageSerializer", "fields": MESSAGE,
     "path": "/api/communication/rooms/{room}/messages/", "kind": "paged"},
    {"name": "FreelancerEarnings", "fields": FREELANCER_EARNINGS,
     "path": "/api/payments/freelancer/transaction-history", "kind": "paged_object",
     "row_fields": FREELANCER_EARNINGS_CONTRACT, "row_path": "results.contracts"},
    {"name": "AdminDashboardSerializer", "fields": DASHBOARD_STATS,
     "path": "/api/admin/dashboard-stats", "kind": "object"},
    {"name": "RevenueChartResponse", "fields": REVENUE_CHART,
     "path": "/api/admin/revenue-chart?range=6m", "kind": "list"},
    {"name": "ProposalsChartResponse", "fields": PROPOSALS_CHART,
     "path": "/api/admin/proposals-chart?range=6m", "kind": "list"},
]

# Routes that address one resource: resolve a real id from the caller's own data.
# (source path, source kind, key to read). None = not resolvable automatically.
ID_SOURCES = {
    "ProposalsListSerializer": ("/api/projects/", "paged", "id"),
    # reviews are keyed by USER id, not project id
    "ReviewSerializer": ("/auth/me/", "object", "user.id"),
    # needs another user's freelancer profile; no generic source
    "FreelancerReviewListSerializer": None,
    "MessageSerializer": ("/api/communication/rooms/", "list", "id"),
}

# Endpoints a client token cannot reach. Reported separately from real failures.
ROLE_HINTS = {
    "AdminTransactionSerializer": "admin",
    "FreelancerToDoSerializer": "freelancer",
    "FreelancerReviewListSerializer": "freelancer (another user's profile id)",
    "FreelancerEarnings": "freelancer",
    "AdminDashboardSerializer": "admin",
    "RevenueChartResponse": "admin",
    "ProposalsChartResponse": "admin",
}


def fetch(base: str, path: str, token: str | None) -> object:
    req = urllib.request.Request(base.rstrip("/") + path)
    if token:
        req.add_header("Authorization", f"Bearer {token}")
    req.add_header("Accept", "application/json")
    ctx = ssl._create_unverified_context()  # dev certificate is self-signed
    with urllib.request.urlopen(req, context=ctx, timeout=20) as resp:
        return json.loads(resp.read().decode("utf-8"))


def sample_items(payload: object, kind: str) -> list[dict]:
    if kind == "paged":
        return [r for r in (payload.get("results") or []) if isinstance(r, dict)]
    if kind == "list":
        return [r for r in (payload or []) if isinstance(r, dict)]
    if kind == "object":
        return [payload] if isinstance(payload, dict) else []
    return []


def node_at(payload: object, path: str) -> object:
    """Read a dotted path, e.g. 'results.contracts'."""
    node = payload
    for part in path.split("."):
        if not isinstance(node, dict):
            return None
        node = node.get(part)
    return node


def resolve_id(base: str, token: str | None, ep: dict) -> str:
    src = ID_SOURCES.get(ep["name"])
    if src is None:
        raise LookupError("no automatic id source; pass the id explicitly")
    path, kind, field = src

    payload = fetch(base, path, token)
    if "." in field:
        outer, inner = field.split(".")
        node = payload.get(outer) or {}
        return str(node[inner])

    items = sample_items(payload, kind)
    if not items:
        raise LookupError("no source rows")
    return str(items[0][field])


def check(base: str, token: str | None) -> tuple[int, int]:
    failures = 0
    role_blocked = 0

    for ep in ENDPOINTS:
        path = ep["path"]
        label = f"{ep['name']:34} {path}"
        item_fields = set(ep["fields"]) - DRF_PAGE_FIELDS

        if "{" in path:
            try:
                for token_name in ("{id}", "{room}"):
                    path = path.replace(token_name, str(resolve_id(base, token, ep)))
            except Exception as exc:  # noqa: BLE001
                print(f"SKIP  {label}  (unresolved id: {exc})")
                continue

        try:
            payload = fetch(base, path, token)
        except urllib.error.HTTPError as exc:
            if exc.code in (401, 403):
                hint = ROLE_HINTS.get(ep["name"], "a role this token lacks")
                print(f"ROLE  {label}  HTTP {exc.code}  [needs {hint}]")
                role_blocked += 1
                continue
            print(f"FAIL  {label}  HTTP {exc.code}")
            failures += 1
            continue
        except Exception as exc:  # noqa: BLE001
            print(f"FAIL  {label}  {type(exc).__name__}: {exc}")
            failures += 1
            continue

        if ep["kind"] in ("paged", "paged_object"):
            top = set(payload.keys()) if isinstance(payload, dict) else set()
            missing_env = DRF_PAGE_FIELDS - top
            if missing_env:
                print(f"FAIL  {label}  envelope missing: {sorted(missing_env)}")
                failures += 1

        if ep["kind"] == "paged_object":
            # `results` is an object here, not a list of rows. Diff it against
            # the summary fields, then diff its `contracts` rows separately.
            results = payload.get("results") if isinstance(payload, dict) else None
            if not isinstance(results, dict):
                print(f"FAIL  {label}  results is {type(results).__name__}, expected object")
                failures += 1
                continue

            missing = sorted(item_fields - set(results.keys()))
            if missing:
                print(f"FAIL  {label}  results missing: {missing}")
                failures += 1
                continue

            rows = [r for r in (results.get("contracts") or []) if isinstance(r, dict)]
            if not rows:
                print(f"SKIP  {label}  (no contract rows to diff)")
                continue

            row_missing = sorted(set(ep["row_fields"]) - set(rows[0].keys()))
            if row_missing:
                print(f"FAIL  {label}  contract rows missing: {row_missing}")
                failures += 1
            else:
                print(f"OK    {label}")
            continue

        items = sample_items(payload, ep["kind"])
        if not items:
            print(f"SKIP  {label}  (no rows to diff)")
            continue

        actual = set(items[0].keys())
        missing = sorted(item_fields - actual)

        if missing:
            print(f"FAIL  {label}")
            print(f"        missing: {missing}")
            failures += 1
        else:
            extra = sorted(actual - item_fields)
            note = f"  (extra: {extra})" if extra else ""
            print(f"OK    {label}{note}")

    print()
    print(f"{failures} parity failure(s), {role_blocked} blocked by role, "
          f"out of {len(ENDPOINTS)} endpoints.")
    if failures == 0:
        print("No field-name mismatches on the endpoints that could be checked.")
    return failures, role_blocked


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--base", default="https://localhost:5001")
    ap.add_argument("--token", default=None)
    ap.add_argument("--list", action="store_true",
                    help="print the expected field lists and exit")
    args = ap.parse_args()

    if args.list:
        for ep in ENDPOINTS:
            print(f"{ep['name']}  ->  {ep['path']}")
            print(f"    {ep['fields']}")
        return 0

    failures, _ = check(args.base, args.token)
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
