# Briefs API

## Create Brief
POST /api/briefs

**Authentication:** Required (Client role)

**Request Body:**
`json
{
  "text": "Need a web application...",
  "budgetHint": 50000.00,
  "deadlineHint": "2026-12-31T00:00:00Z"
}
`

**Response (200 OK):**
Returns the created brief object.

---

## Get My Briefs
GET /api/briefs/my

**Authentication:** Required (Client role)

**Response (200 OK):**
Returns a list of briefs owned by the current client.

---

## Get Brief Detail
GET /api/briefs/{id}

**Authentication:** Required (Client role)

**Response (200 OK):**
Returns the brief detail.

**Errors:**
- 404 Not Found: Brief does not exist.
- 403 Forbidden: Client does not own the brief.
