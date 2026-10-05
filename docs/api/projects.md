# Projects API

All monetary values are in **USD** using `decimal(18,2)`.

## Endpoints

| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/projects` | Client | Create a new project |
| GET | `/api/projects` | Public | List open projects (filter by `?search=`) |
| GET | `/api/projects/{id}` | Public | Get project detail |
| PUT | `/api/projects/{id}` | Client (owner) | Update project |
| DELETE | `/api/projects/{id}` | Client (owner) | Delete project |
| GET | `/api/projects/my` | Client | List own projects |
| GET | `/api/projects/my/{id}` | Client (owner) | Own project detail |
| GET | `/api/projects/recommended` | Freelancer | Skill-matched recommendations |

---

## POST /api/projects

**Body:**
```json
{
  "title": "Build a logistics platform",
  "description": "We need a scalable ERP system for a logistics company.",
  "pricingType": 1,
  "fixedPrice": 150000,
  "minPrice": null,
  "maxPrice": null,
  "expectedDays": 30,
  "skillIds": [1, 3]
}
```

**Pricing Type:**
- `1` = Fixed — requires `fixedPrice > 0`, `minPrice` and `maxPrice` must be null.
- `2` = Range — requires `minPrice > 0`, `maxPrice > minPrice`, `fixedPrice` must be null.

**Response (200):**
```json
{
  "id": 1,
  "clientId": "...",
  "title": "Build a logistics platform",
  "description": "...",
  "pricingType": "Fixed",
  "fixedPrice": 150000.00,
  "minPrice": null,
  "maxPrice": null,
  "expectedDays": 30,
  "status": "Open",
  "createdAt": "2026-09-21T00:00:00Z",
  "updatedAt": "2026-09-21T00:00:00Z",
  "skills": [
    { "skillId": 1, "name": "React" }
  ]
}
```

---

## GET /api/projects?search=

Returns all `Open` projects. Optional `?search=keyword` filters by title or description.

---

## PUT /api/projects/{id}

Owner-only. Returns `403 Forbidden` if the authenticated client does not own the project.

**Body:**
```json
{
  "title": "Updated title",
  "description": "Updated description for the project.",
  "expectedDays": 45,
  "skillIds": [2]
}
```

---

## DELETE /api/projects/{id}

Owner-only. Returns `204 No Content` on success, `403 Forbidden` if not the owner.

---

## GET /api/projects/recommended

Requires `Freelancer` role. Returns up to 20 open projects matched against the freelancer's skill set. Falls back to recent open projects if no skills are registered.

---

## Validation Rules

| Field | Rule |
|-------|------|
| `title` | Required, 5–255 chars |
| `description` | Required, min 20 chars |
| `pricingType` | Must be 1 or 2 |
| `fixedPrice` | Required and > 0 when `pricingType == 1`; null otherwise |
| `minPrice` | Required and > 0 when `pricingType == 2`; null otherwise |
| `maxPrice` | Required and > `minPrice` when `pricingType == 2`; null otherwise |
| `expectedDays` | Must be > 0 |
