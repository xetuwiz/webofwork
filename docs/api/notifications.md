# Notifications API

## Overview

Per-user in-app notifications with optional real-time SignalR push and SMS dispatch for a whitelist of critical event types. Mirrors the private reference layers at `catacolite/demo2/.../service/notification/SmsNotificationObserver.java` and `EmailNotificationObserver.java` observer pattern via `NotificationService.PublishAsync` + the 11 SMS strategies registered in `SmsStrategyFactory`.

Every notification belongs to exactly one `Recipient (User)`. Cross-user reads/deletes → 403 Forbidden (enforced at the service layer via `FindOwnOrThrow`).

Two integration surfaces exist for creating notifications:

1. **Direct publish**: `INotificationPublisher.PublishAsync(recipientId, type, message, relatedEntityType?, relatedEntityId?, smsArgs[])` — used by `ChatService.SendMessageAsync` for `NewMessage`.
2. **Domain-event publish**: `INotificationPublisher.TriggerFromDomainEventAsync<TEvent>(typedEvent)` — dispatches 10 typed event records (see below).

SMS dispatch is gated by two conditions inside PublishAsync:

- The `NotificationType` must be a member of `SmsEnabledTypes` (11 types).
- The recipient must have a non-empty `User.Phone` value.

If both conditions are true the appropriate `ISmsMessageStrategy` resolves via `SmsStrategyFactory` (fallback: `GenericNotificationSmsStrategy`), SMS text is generated using the `smsArgs` params array, and dispatched through `ISmsSender.SendSmsAsync(recipient.Phone, smsText)`. Any SMS failure is logged via `ILogger<NotificationService>` as an error but never **never** prevents the DB notification-row from being committed.

---

## Notification types (`NotificationType` enum)

Registered SMS-enabled set (exactly the 11 types wired in `SmsEnabledTypes` and `SmsStrategyFactory`.

| Value                | Typical SMS / strategy                | Related entity   |
| -------------------- | ------------------------------------- | ---------------- |
| `ContractStarted`    | ContractStartedSmsStrategy            | Contract         |
| `WorkDelivered`      | WorkDeliveredSmsStrategy              | Contract         |
| `ContractCompleted`  | ContractCompletedSmsStrategy          | Contract         |
| `PaymentCredited`    | PaymentCreditedSmsStrategy(amount)    | Payment          |
| **`NewMessage`**     | **NewMessageSmsStrategy(senderName)** | **ChatRoom**     |
| `PayoutReleased`     | PayoutReleasedSmsStrategy(amount)     | Contract         |
| `DisputeOpened`      | DisputeOpenedSmsStrategy              | Dispute          |
| `RevisionRequested`  | RevisionRequestedSmsStrategy          | ContractRevision |
| `OfferReceived`      | OfferReceivedSmsStrategy              | Invitation       |
| `InvitationReceived` | InvitationReceivedSmsStrategy         | Invitation       |
| `ProposalAccepted`   | ProposalAcceptedSmsStrategy           | Proposal         |

Additional types may be added; unknown types fall back to `GenericNotificationSmsStrategy` via `TryGetValueFallback`.

---

## Domain-event records for `TriggerFromDomainEventAsync`

All inherit from `DomainNotificationEvent(RecipientId)`.

| C# record                                              | Produces NotificationType |
| ------------------------------------------------------ | ------------------------- |
| `ContractStartedEvent(RecipientId, ContractId)`        | `ContractStarted`         |
| `WorkDeliveredEvent(RecipientId, ContractId)`          | `WorkDelivered`           |
| `ContractCompletedEvent(RecipientId, ContractId)`      | `ContractCompleted`       |
| `PaymentCreditedEvent(RecipientId, PaymentId, Amount)` | `PaymentCredited`         |
| `PayoutReleasedEvent(RecipientId, ContractId, Amount)` | `PayoutReleased`          |
| `DisputeOpenedEvent(RecipientId, DisputeId)`           | `DisputeOpened`           |
| `RevisionRequestedEvent(RecipientId, RevisionId)`      | `RevisionRequested`       |
| `OfferReceivedEvent(RecipientId, InvitationId)`        | `OfferReceived`           |
| `InvitationReceivedEvent(RecipientId, InvitationId)`   | `InvitationReceived`      |
| `ProposalAcceptedEvent(RecipientId, ProposalId)`       | `ProposalAccepted`        |

---

## Endpoints

### GET `/api/notifications`

Return the current user's notifications ordered by creation-date desc, with header-level counters.

**Auth:** Authenticated (any role).

**Query params:**

- `isRead` nullable bool — filter rows to only-read or only-unread (omit = both).
- `limit` int (default 50, cap 200).

**Response `200 OK`:**

```json
{
  "data": {
    "unreadCount": 3,
    "totalCount": 12,
    "items": [
      {
        "id": 7,
        "type": "NewMessage",
        "message": "New message from Alice Client",
        "relatedEntityType": "ChatRoom",
        "relatedEntityId": 1,
        "isRead": false,
        "createdAt": "2026-09-22T10:15:00Z"
      }
    ]
  }
}
```

---

### PATCH `/api/notifications/{id:int}/read`

Mark a \*\*single notification as read. Ownership enforced.

**Auth:** Authenticated recipient only.

**Response `200 OK`:**

```json
{ "data": { "id": 7, "type": "NewMessage", "...": "...", "isRead": true } }
```

Errors:

- `403 Forbidden` — belongs to another user.
- `404 Not Found` — notification id does not exist.

---

### PATCH `/api/notifications/read-all`

Bulk mark-all of the current user's unread notifications. Returns number of rows updated.

**Auth:** Authenticated.

**Response `200 OK`:**

```json
{ "marked": 3 }
```

---

### DELETE `/api/notifications/{id:int}`

Hard-delete a notification row. Recipient-only.

**Auth:** Authenticated recipient only.

**Response:** `204 No Content`

**Errors:**

- `403 Forbidden` — not the recipient.
- `404 Not Found` — does not exist.

---

## SignalR Hub — `/hubs/notifications`

Real-time push channel. Each connection joins the SignalR group `$"user_{userId:N}"` on `OnConnectedAsync` (three claim fallbacks: `UserIdentifier` → `NameIdentifier` → `sub`).

Publishers push notifications via:

```csharp
_hubContext.Clients.Group($"user_{userId:N}").SendAsync("Notification", notification);
```

**Client snippet:**

```js
nh = new signalR.HubConnectionBuilder()
  .withUrl("/hubs/notifications", { accessTokenFactory: () => JWT })
  .configureLogging(signalR.LogLevel.Information)
  .build();
nh.on("Notification", (n) => console.log("[NOTIFICATION]", n));
await nh.start();
```

---

## Indexing

- `IX_notifications_recipient_read_created` — composite `(RecipientId, IsRead, CreatedAt)` — covers the inbox list query.
- Second partial filtered index on `(RelatedEntityType, RelatedEntityId)` where `related_entity_type IS NOT NULL AND related_entity_id IS NOT NULL`.

---

## Notes on SMS dispatch

1. SMS attempts are wrapped in try/catch; a failure logs an error but does not affect the notification being committed. The user still sees an in-app notification row even when the SMS gateway is unreachable.
2. SMS is **only** sent for the 11-type whitelist AND the user explicitly has a `Phone` configured. No `Phone` → skip SMS silently.
3. Strategy args are type-safe from the caller (e.g., `ChatService` passes `senderName` as `smsArgs[0]` → `NewMessageSmsStrategy.GenerateMessage(senderName)`).
