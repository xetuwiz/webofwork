# Disputes API

## Overview
Allows contract participants (Client or Freelancer) to raise a dispute and allows Admins to review and resolve it. Mirrors `server/contracts/models.py (Dispute, DisputeFile)` and `server/contracts/views.py (RaiseDisputeView, AdminDisputeViewSet)`.

---

## Endpoints

### POST `/api/disputes/contract/{contractId}`
Raise a dispute. Caller must be a participant of the contract (Client or Freelancer). Only one dispute allowed per contract.

**Auth:** Authenticated (Client or Freelancer)

**Request body:**
```json
{
  "reason": 4,
  "description": "Work quality does not match the proposal.",
  "evidenceUrls": ["https://example.com/evidence1.pdf"]
}
```

**Reason values:**
| Value | Label |
|---|---|
| 1 | ScopeCreep |
| 2 | NoFeedback |
| 3 | UnlimitedRevisions |
| 4 | PoorQuality |
| 5 | MissedDeadline |
| 6 | IncompleteWork |
| 7 | CommunicationIssue |
| 8 | Other |

**Response:** `201 Created` → `DisputeResponse`

**Errors:**
- `403 Forbidden` — caller is not a contract participant (step 11.7)
- `400 Bad Request` — dispute already exists for this contract (step 11.8)

---

### GET `/api/disputes/contract/{contractId}`
Participant views their own dispute for a contract (step 11.11a).

**Auth:** Authenticated (Client or Freelancer — participant only)

**Response:** `200 OK` → `DisputeResponse`

---

### GET `/api/disputes`
Admin: list all disputes ordered by creation date.

**Auth:** Admin only

**Response:** `200 OK` → `DisputeResponse[]`

---

### GET `/api/disputes/{id}`
Admin: get detail of a specific dispute.

**Auth:** Admin only

**Response:** `200 OK` → `DisputeResponse`

---

### POST `/api/disputes/{id}/resolve`
Admin resolves a dispute, optionally chaining contract status.

**Auth:** Admin only (step 11.14)

**Request body:**
```json
{
  "status": 3,
  "adminNotes": "Client claim is valid. Full refund applied.",
  "resolution": 1
}
```

**Status values:** `1=Pending, 2=UnderReview, 3=Resolved, 4=Rejected`

**Resolution values:**
| Value | Label | Contract Effect |
|---|---|---|
| 1 | Refund | Contract → Refunded |
| 2 | ReleaseToFreelancer | Contract → Completed + PaidToFreelancer |
| 3 | Split | Contract → Refunded |

**Response:** `200 OK` → `DisputeResponse`

---

## DisputeResponse Schema
```json
{
  "id": 1,
  "contractId": 5,
  "openerId": "guid",
  "reason": 4,
  "description": "string",
  "status": 1,
  "adminNotes": null,
  "resolution": null,
  "resolvedAt": null,
  "createdAt": "2026-09-21T00:00:00Z",
  "evidenceUrls": ["https://..."]
}
```
