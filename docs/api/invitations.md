# Invitations API

## Overview
Clients can directly invite freelancers to their open projects by selecting a package. Freelancers accept or decline. Accepting auto-creates a draft proposal.

**Currency:** All amounts from the referenced package are in USD.

---

## Endpoints

### POST `/api/invitations`
**Auth:** Client

Invite a freelancer to a project.

**Request:**
```json
{
  "freelancerId": "uuid",
  "projectId": 1,
  "packageId": "uuid",
  "message": "We think you'd be perfect for this project."
}
```

**Rules:**
- Client must own the project
- Freelancer must not already have a proposal on this project (400)
- Client must not have already invited this freelancer to this project (400)
- `message` max 2000 chars (optional)

**Response:** `201 Created` with `InvitationResponse`

---

### GET `/api/invitations/sent`
**Auth:** Client

Returns all invitations sent by the authenticated client.

**Response:** `200 OK` — array of `InvitationResponse`

---

### GET `/api/invitations/received`
**Auth:** Freelancer

Returns all invitations received by the authenticated freelancer.

**Response:** `200 OK` — array of `InvitationResponse`

---

### PATCH `/api/invitations/{id}/status`
**Auth:** Freelancer (invitation recipient only)

Accept or decline a pending invitation.

**Request:**
```json
{ "status": 1 }
```
`1 = Accepted, 2 = Declined`

**Accept side-effect:** A draft `Proposal` is automatically created using the invited package's price and delivery days.

**Response:** `204 No Content`

**Errors:**
- `400` — Invitation is not pending, or trying to set status to `Cancelled`
- `403` — Not the invitation recipient
- `404` — Invitation not found

---

### PATCH `/api/invitations/{id}/cancel`
**Auth:** Client (invitation sender only)

Cancel a pending invitation.

**Response:** `204 No Content`

**Errors:**
- `400` — Invitation is not pending
- `403` — Not the invitation sender
- `404` — Invitation not found

---

### GET `/api/invitations/eligible-projects`
**Auth:** Client

Returns a list of the client's open, active projects eligible for sending invitations (for UI dropdowns).

**Response:** `200 OK`
```json
[{ "id": 1, "title": "Logistics ERP for United States" }]
```

---

## InvitationResponse Schema
```json
{
  "id": 1,
  "clientId": "uuid",
  "clientName": "Acme Corp",
  "freelancerId": "uuid",
  "freelancerName": "Jane Doe",
  "projectId": 1,
  "projectTitle": "Logistics ERP for United States",
  "packageId": "uuid",
  "message": "We think you'd be perfect...",
  "status": "Pending | Accepted | Declined | Cancelled",
  "createdAt": "2026-09-21T00:00:00Z"
}
```

## Status Values
| Value       | Description                        |
|-------------|-----------------------------------|
| `Pending`   | Default; awaiting freelancer action |
| `Accepted`  | Freelancer accepted (auto-proposal) |
| `Declined`  | Freelancer declined                |
| `Cancelled` | Client cancelled                   |
