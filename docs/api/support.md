# Support API

Support tickets are authenticated user requests. Each user may have at most five tickets with `Open` status. The sixth open-ticket creation attempt returns RFC 7807 `403 Forbidden`.

## User endpoints

### `POST /api/support/tickets`

Creates an open support ticket.

Request:

```json
{
  "subject": "Unable to submit work",
  "message": "The submission form is failing."
}
```

Requires authentication. `subject` is required and limited to 255 characters. `message` is required and limited to 4000 characters.

Responses:

- `200 OK` with `SupportTicketResponse`
- `400 Bad Request` for validation errors
- `403 Forbidden` when the user already has five open tickets

### `GET /api/support/tickets`

Returns the authenticated user's tickets, newest first, with `TotalCount` and `OpenCount`. Users cannot view another user's tickets.

### `PATCH /api/support/tickets/{id}/cancel`

Cancels the authenticated user's open ticket.

Responses:

- `200 OK` with the cancelled ticket
- `403 Forbidden` when the ticket belongs to another user
- `400 Bad Request` when the ticket is not open
- `404 Not Found` when the ticket does not exist

## Admin endpoints

All admin routes require the `RequireAdmin` authorization policy.

- `GET /api/support/admin/tickets?status=Open&senderRole=Client` lists tickets with optional filters.
- `GET /api/support/admin/tickets/{id}` returns a ticket detail.
- `PATCH /api/support/admin/tickets/{id}/reply` stores the reply, resolves the ticket, and enqueues `SendSupportReplyJob` when the user has an email address.

Admin replies require a non-empty reply of at most 4000 characters. Resolved tickets cannot be modified.

## Persistence and email

Support tickets are stored in `support_tickets` with user and resolving-admin relationships. The Phase 15 migration adds the table and indexes. Reply email delivery uses the existing SMTP/MailKit integration and Hangfire; no SMS is sent for support replies.
