# Proposals API

## Overview
Freelancers submit proposals to open projects. Clients review, accept, or reject them. Freelancers can withdraw pending proposals.

**Currency:** All monetary amounts are in USD (`decimal(18,2)`).

---

## Endpoints

### POST `/api/proposals`
**Auth:** Freelancer

Create a proposal for an open project.

**Request:**
```json
{
  "projectId": 1,
  "packageId": "uuid",
  "coverLetter": "Your detailed cover letter here...",
  "bidAmount": 75000.00,
  "deliveryDays": 14
}
```

**Rules:**
- One proposal per freelancer per project (unique constraint)
- `packageId` must be owned by the authenticated freelancer
- Clients cannot submit proposals (403)
- `coverLetter` min 10, max 4000 chars
- `bidAmount` > 0 (USD)
- `deliveryDays` ≥ 1

**Response:** `201 Created` with `ProposalResponse`

---

### GET `/api/proposals/my`
**Auth:** Freelancer

Returns all proposals submitted by the authenticated freelancer.

**Response:** `200 OK` — array of `ProposalResponse`

---

### GET `/api/proposals/project/{projectId}`
**Auth:** Client (project owner only)

Returns all proposals for the specified project.

**Response:** `200 OK` — array of `ProposalResponse`

---

### PATCH `/api/proposals/{id}/reject`
**Auth:** Client (project owner only)

Reject a pending proposal.

**Response:** `204 No Content`

**Errors:**
- `404` — Proposal not found
- `403` — Not the project owner
- `400` — Proposal is not pending

---

### DELETE `/api/proposals/{id}`
**Auth:** Freelancer (proposal owner only)

Withdraw a pending proposal.

**Response:** `204 No Content`

**Errors:**
- `404` — Proposal not found
- `403` — Not the proposal owner
- `400` — Proposal is not pending

---

## ProposalResponse Schema
```json
{
  "id": 1,
  "projectId": 1,
  "projectTitle": "Logistics ERP for United States",
  "freelancerId": "uuid",
  "freelancerName": "Jane Doe",
  "packageId": "uuid",
  "coverLetter": "...",
  "bidAmount": 75000.00,
  "deliveryDays": 14,
  "status": "Pending | Accepted | Rejected | Withdrawn",
  "createdAt": "2026-09-21T00:00:00Z"
}
```

## Status Values
| Value       | Description                    |
|-------------|-------------------------------|
| `Pending`   | Default; awaiting client action|
| `Accepted`  | Client accepted proposal       |
| `Rejected`  | Client rejected proposal       |
| `Withdrawn` | Freelancer withdrew proposal   |
