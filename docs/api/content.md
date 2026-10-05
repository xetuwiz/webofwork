# Content API

Content endpoints expose public FAQ and legal-content reads, plus admin-only FAQ and CMS management. Public reads are anonymous and return only active records.

## Public endpoints

### `GET /api/content/faq`

Returns active FAQs ordered by `SortOrder`, then newest first.

Optional query parameter:

- `category`: `General`, `Freelancer`, `Client`, or `Payment`

Response shape:

```json
{
  "items": [
    {
      "id": 1,
      "question": "How does escrow work?",
      "answer": "...",
      "category": "Payment",
      "sortOrder": 1,
      "createdAt": "2026-09-22T10:00:00Z"
    }
  ],
  "totalCount": 1
}
```

### `GET /api/content/terms`

Returns active CMS sections in the `Terms` category, ordered by `SortOrder`.

### `GET /api/content/privacy`

Returns active CMS sections in the `Privacy` category, ordered by `SortOrder`.

### `GET /api/content/cms/{category}`

Equivalent category-based public read. Valid categories are `terms` and `privacy`; an unknown category returns `400 Bad Request`.

All public endpoints allow anonymous access.

## Admin FAQ endpoints

All admin routes require the `RequireAdmin` authorization policy.

- `GET /api/content/admin/faq`
- `POST /api/content/admin/faq`
- `PUT /api/content/admin/faq/{id}`
- `DELETE /api/content/admin/faq/{id}`

FAQ requests require a question up to 500 characters, an answer up to 8000 characters, and a valid category.

## Admin CMS endpoints

- `GET /api/content/admin/cms`
- `POST /api/content/admin/cms`
- `PUT /api/content/admin/cms/{id}`
- `DELETE /api/content/admin/cms/{id}`

CMS requests require a valid `Terms` or `Privacy` category, a heading up to 255 characters, and a description up to 8000 characters.

## Persistence

FAQs and CMS sections are stored in `faqs` and `cms_sections`. The Phase 15 EF migration creates both tables, their indexes, and the configured enum/string fields.
