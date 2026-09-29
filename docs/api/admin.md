# Admin API

## Overview

Administrative user and dashboard endpoints for platform moderation and analytics. All admin routes require the `RequireAdmin` authorization policy and authenticated JWT claims with the `role` claim set to `Admin`.

This mirrors the Phase 16 adminpanel reference, covering user management, self-protection rules, and dashboard analytics.

---

## Endpoints

### GET `/api/admin/users`

Returns the user list with optional search and filters.

**Auth:** Admin only

**Query params:**

- `search` — partial match against email or full name
- `role` — `Client`, `Freelancer`, or `Admin`
- `status` — `Active`, `Blocked`, `Pending`, or `Deleted`

**Response:** `200 OK` with `AdminUserResponse[]`

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "email": "client@example.com",
    "fullName": "Jane Client",
    "role": "Client",
    "status": "Active",
    "isActive": true,
    "isEmailVerified": true,
    "createdAt": "2026-09-22T00:00:00Z",
    "updatedAt": "2026-09-22T00:00:00Z"
  }
]
```

### PATCH `/api/admin/users/{id}/toggle-active`

Toggles an ordinary user from `Active` to `Blocked` or back.

**Auth:** Admin only

**Behavior:**

- Returns `400 Bad Request` if the target is an admin account
- Updates the user status
- Returns the updated `AdminUserResponse`

### DELETE `/api/admin/users/{id}/soft-delete`

Soft deletes an ordinary user account.

**Auth:** Admin only

**Behavior:**

- Sets `IsDeleted = true`
- Sets `DeletedAt` to the current UTC time
- Sets `Status = Deleted`
- Returns `400 Bad Request` if the target is an admin account

### GET `/api/admin/dashboard-stats`

Returns platform summary metrics required by the admin dashboard.

**Auth:** Admin only

**Response:** `200 OK` with `DashboardStatsResponse`

```json
{
  "totalRevenueUsd": 125000.0,
  "totalPlatformFeesUsd": 12500.0,
  "netRevenueUsd": 112500.0,
  "totalPendingPayoutsUsd": 42000.0,
  "pendingPayoutCount": 3,
  "totalUsers": 152,
  "activeUsers": 139,
  "totalProjects": 86,
  "openProjects": 28,
  "completedContracts": 41
}
```

### GET `/api/admin/revenue-chart?days=7`

Returns daily revenue totals for the last `N` days. Missing dates are padded with zero values.

**Auth:** Admin only

**Response:** `200 OK` with `RevenueChartResponse[]`

```json
[
  {
    "date": "2026-09-16T00:00:00Z",
    "revenueUsd": 1250.0,
    "totalTransactions": 4
  }
]
```

### GET `/api/admin/proposals-chart?days=7`

Returns per-day proposal counts for submitted, accepted, and rejected proposals.

**Auth:** Admin only

**Response:** `200 OK` with `ProposalsChartResponse[]`

```json
[
  {
    "date": "2026-09-16T00:00:00Z",
    "submitted": 8,
    "accepted": 2,
    "rejected": 3
  }
]
```

### GET `/api/admin/pending-payouts`

Returns pending payouts eligible for release, net of platform fee.

**Auth:** Admin only

**Response:** `200 OK` with `PaymentResponse[]`

---

## Security notes

- Admin self-protection is enforced: admin accounts cannot be toggled inactive or soft-deleted.
- Non-admin callers are rejected by the `RequireAdmin` authorization policy.
- All amount totals use USD semantics and `decimal(18,2)`-style arithmetic, consistent with the platform requirements.
