# Chat API

## Overview

Real-time contract chat between the Client and Freelancer participants of an **Active**, **Submitted**, or **Disputed** contracts. Mirrors the private reference layers at `server/communication/models.py (ChatRoom, Message)` and consumers.py (ChatConsumer)` and the observer-pattern SMS/email dispatch in `catacolite/demo2/.../observer/* (SmsNotificationObserver.java, EmailNotificationObserver.java)` and Lancy audit `messaging.gateway.ts`, `messaging.service.ts` (with the Lancy ascending+take pagination bug explicitly NOT ported — see Pagination note below).

- **Status gate (CRITICAL): A contract must be in status `Active`, `Submitted`, or `Disputed` before any participant may send messages.** All other statuses (Draft, Cancelled, PendingPayment, Completed, etc.) return 400 with the explanation `Chat is not available for contracts in status …`.

- **Contact-information filtering: All inbound text is scanned by regex patterns for phone numbers, email addresses, social-media keywords and domain/URLs and rejected with 400 `blockedContactInfo=true`. Enforced both in the HTTP DTO validator AND deep in the service layer so SignalR Hub sends cannot bypass it.

- **Sender-only message deletion: Any participant may only delete her own messages. Deletion is a soft-delete tombstone (`text = "[deleted]" which still appears in the message list.** Other participant attempts → 403 Forbidden.

- **Per-participant room archive: `ClientArchived` and `FreelancerArchived` are independent boolean columns on `ChatRoom`. Freelancer can archive the room without affecting what Freelancer sees.** Admin has no archive state (PATCH archive → 400 Bad Request).

- **Read receipts via two channels:** (1) SignalR `Read` hub method pushes `Clients.Group(room).SendAsync("MessagesRead", …) and (2) REST PATCH `/api/chat/rooms/{id}/read` for browser clients that cannot hold a WebSocket.

- **Pagination (Lancy bug NOT ported: `GET /messages` intentionally uses desc + take + reverse (oldest-first display). Lancy's ascending + take which returns oldest not newest.

---

## Endpoints

### GET `GET `/api/chat/rooms`
List my rooms ordered by most-recent-activity first. Participant only their own rooms via role-filtered Client/Freelancer.

**Auth:** Authenticated (Client, Freelancer, Admin)

**Response `200 OK`:
```json
{
  "data": [
    {
      "id": 1,
      "contractId": 12,
      "client":   { "id": "guid", "name": "Alice Client",   "role": "Client" },
      "freelancer": { "id": "guid", "name": "Ben Freelancer", "role": "Freelancer" },
      "lastMessagePreview": "okay thanks",
      "lastMessageAt": "2026-09-22T10:15:00Z",
      "unreadCount": 3,
      "isArchived": false,
      "contractStatus": "Active",
      "createdAt": "2026-09-20T08:00:00Z"
    }
  ]
}
```

---

### POST `/api/chat/rooms`
Get or lazily-create a Chat Room for a given Contract. Returns the existing room if already exists; creates one if not. Only a participant (or Admin) may create/get a room.

**Auth:** Authenticated.

**Request:**
```json
{ "contractId": 12 }
```

**Response:** `200 OK` → `ChatRoomResponse` (same shape as the list-item above).

**Errors:**
- `403 Forbidden — caller not a participant on the contract (or Admin allowed).
- `404 Not Found — contract does not exist.
- `400 Bad Request — contractId ≤ 0.

---

### GET `/api/chat/rooms/{roomId:int}`
Single-room detail view including `canSendMessages` flag and `unreadCount`.

**Auth:** Authenticated (participant or Admin).

**Response:** `200 OK`
```json
{
  "data": {
    "id": 1,
    "contractId": 12,
    "client": { "id": "guid", "name": "Alice", "role": "Client" },
    "freelancer": { "id": "guid", "name": "Ben", "role": "Freelancer" },
    "canSendMessages": true,
    "contractStatus": "Active",
    "lastMessageAt": "…",
    "createdAt": "…",
    "unreadCount": 3
  }
}
```

---

### GET `/api/chat/rooms/{roomId:int}/messages`
Paginated messages (descending+take+reverse. Pagination is oldest-to-newest within each page.)

**Auth:** Authenticated participant.

**Query params:**
- `pageSize` int, default 50, max 200
- `beforeMessageId` nullable int — only return messages with Id < beforeMessageId)

**Response:** `200 OK`
```json
{
  "data": {
    "roomId": 1,
    "totalCount": 31,
    "items": [
      { "id": 10, "roomId": 1, "senderId": "guid", "senderRole": "Client", "senderName": "Alice",
        "text": "hi", "isRead": true, "isDeleted": false, "createdAt": "…" }
    ],
    "hasMore": true
  }
}
```

Note: Deleted messages still appear with `"text": "[deleted]", "isDeleted": true — tombstone — we do not use ascending + take Lancy bug pattern; pagination order was deliberately not ported.

---

### POST `/api/chat/rooms/{roomId:int}/messages`
Send a text message. Enforces status gate + contact-information filter. Also pushes a `NewMessage` notification + SMS dispatch via `NotificationService.PublishAsync(Recipient, NotificationType.NewMessage, …)` and a SignalR `MessageReceived` group broadcast.

**Auth:** Authenticated participant.

**Request:**
```json
{ "content": "delivered the first revision" }
```

**Response:** `200 OK` → `ChatMessageResponse`.

**Errors:**
- `403 Forbidden — not a participant.
- `400 Bad Request` — empty or > 4000 chars **or** blocked contact info detected → `{ errors: […], "blockedContactInfo": true }`.
- `400 Bad Request — contract not in {Active, Submitted, Disputed}.

---

### DELETE `/api/chat/rooms/{roomId:int}/messages/{messageId:int}`
Sender-only soft-delete. Non-sender attempt → 403.

**Auth:** Authenticated (original message sender.

**Response:** `204 No Content`

After deletion the message still returned by `GET …/messages as text: "[deleted]", "isDeleted": true. DeletedByUserId recorded.

---

### PATCH `/api/chat/rooms/{roomId:int}/archive`
Participant-only archive. Per-side independent flag.

**Auth:** Authenticated Client or Freelancer participant (Admin → 400).

**Response:** `204 No Content`

Errors:**
- `403 Forbidden` — not a participant.
- `400 Bad Request` — Admin role has no archive state.

---

### PATCH `/api/chat/rooms/{roomId:int}/read`
REST fallback read-receipt. Marks other-party unread messages as read, optionally bounded to `up to a specific `messageId`. Mirrors the SignalR `Read` hub method.

**Auth:** Authenticated participant.

**Request (optional body):**
```json
{ "messageId": 42 }
```

Omit body for "mark latest unread.

**Response:** `200 OK`
```json
{ "marked": 2 }
```

---

## SignalR Hub — `/hubs/chat`

WebSocket/SSE/long-polling clients. JWT authentication via the `?access_token=` query string — handled by `JwtQueryStringMiddleware` placed before `UseAuthentication` reads `Authorization: Bearer …` header.

Connection joins user group on connect: `Groups.AddToGroup(Context.ConnectionId, $"user_{userId:N}")`.

### Hub methods:
| Method | Args | Behavior |
|--------|------|----------|
| `JoinRoom(roomId)` | int roomId | Participant-only, adds connection to `room_{roomId}` group. |
| `LeaveRoom(roomId)` | int roomId | Removes connection from group. |
| `Send(roomId, content)` | int, string | Calls `ChatService.SendMessageAsync(...)` (runs status gate + contact block). Broadcasts `MessageReceived(message)` to the room group. |
| `Typing(roomId, isTyping)` | int, bool | Participant-only. Sends `UserTyping({roomId, userId, isTyping)` to all others in room `OthersInGroup`. |
| `Read(roomId, messageId?)` | int, int? | Participant-only. Runs `MarkRoomAsReadAsync(…)` then broadcasts `MessagesRead({ RoomId, ReaderId, Marked, UpToMessageId})` to whole room. |
| `DeleteMessage(roomId, messageId)` | int, int | Sender-only via service. Broadcasts `MessageDeleted({ RoomId, MessageId })` to room. |

### Client-side connection snippet (browser):
```js
h = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/chat', { accessTokenFactory: () => JWT })
      .configureLogging(signalR.LogLevel.Information).build();
h.on('MessageReceived', m => console.log(m));
h.on('UserTyping', t => console.log(t));
h.on('MessagesRead', r => console.log(r));
h.on('MessageDeleted', d => console.log(d));
await h.start();
await h.invoke('JoinRoom', ROOM_ID);
await h.invoke('Send', ROOM_ID, 'hello via SignalR');
```

---

## Contract-chat status allowed statuses

Only these ContractStatus are send-allowed:
- `Active` (work underway)
- `Submitted` (deliverable awaiting review)
- `Disputed` (dispute under review)

All other statuses → sender gets HTTP 400 / HubException "Chat is not available for contracts in status X".

---

## Database objects

- `ChatRoom` — `ContractId (FK → Contract.Restrict)`, `ClientId (FK → User.Restrict)`, `FreelancerId (FK → User.Restrict)`, unique composite unique `(ContractId, ClientId, FreelancerId)` filtered `is_deleted = false`.
- `Message` — `RoomId (FK → ChatRoom.Cascade)`, `SenderId (FK → User.Restrict)`, `SenderRole` enum, `Text (4000), `IsRead`, soft-delete + `DeletedByUserId`. Global soft-delete query filter is on; reads bypass `IgnoreQueryFilters()` to preserve tombstones in message queries.
