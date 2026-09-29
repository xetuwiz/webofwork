# Reviews API

## Overview
Allows contract participants to review each other after contract completion. Mirrors `server/profiles/models.py (Review)` and `server/profiles/views.py (ReviewCreateView, FreelancerReviewListView, ClientReviewListView)`. Rating recalculation mirrors `server/profiles/signals.py`.

---

## Endpoints

### POST `/api/reviews`
Submit a review for a completed contract.

**Auth:** Authenticated (Client or Freelancer — participant only)

**Rules:**
- Contract must be `Completed` or `Refunded` (step 11.21)
- Cannot review yourself (step 11.23)
- One review per (reviewer, contract, type) — no duplicates (step 11.20)
- Client reviewing → creates `Freelancer` review; Freelancer reviewing → creates `Client` review
- Rating must be 1–5 (step 11.22)

**Request body:**
```json
{
  "contractId": 5,
  "rating": 5,
  "comment": "Excellent work, delivered ahead of schedule."
}
```

**Response:** `201 Created` → `ReviewResponse`

---

### GET `/api/reviews/me/freelancer`
Get all reviews received by the current user as a freelancer.

**Auth:** Authenticated

**Response:** `200 OK` → `ReviewResponse[]`

---

### GET `/api/reviews/me/client`
Get all reviews received by the current user as a client.

**Auth:** Authenticated

**Response:** `200 OK` → `ReviewResponse[]`

---

### GET `/api/reviews/freelancer/{profileId}`
Get all freelancer reviews for a specific freelancer profile (public endpoint).

**Auth:** Anonymous

**Response:** `200 OK` → `ReviewResponse[]`

---

### DELETE `/api/reviews/{id}`
Admin: delete a review and recalculate ratings (step 11.28a).

**Auth:** Admin only (step 11.28b — non-admin gets `403 Forbidden`)

**Response:** `204 No Content`

---

## ReviewResponse Schema
```json
{
  "id": 1,
  "contractId": 5,
  "reviewerId": "guid",
  "revieweeId": "guid",
  "type": 2,
  "rating": 5,
  "comment": "Great work!",
  "createdAt": "2026-09-21T00:00:00Z"
}
```

**Type values:** `1 = Client review, 2 = Freelancer review`

---

## Rating Recalculation
After every review creation or deletion, `AverageRating` and `ReviewCount` on the corresponding profile (`FreelancerProfile` or `ClientProfile`) is recalculated in-line. This mirrors the Django `post_save`/`post_delete` signal in `profiles/signals.py`. A periodic `RecalculateRatingsJob` (Hangfire) is also registered for data consistency.
