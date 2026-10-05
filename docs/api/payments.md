# Payments API

## Overview

Client-side PayPal verification, escrow recording, and payment reporting. All amounts are **USD** (`decimal(18,2)`). PayPal orders must match the single-deliverable contract amount and use USD at capture time.

Mirrors the payment service and Lancy payment patterns (with PayPal signature verification instead of mock webhooks).

---

## Endpoints

### POST `/api/payments/verify`

Verify a completed PayPal order and place funds in escrow. Creates or activates the contract, accepts the winning proposal, rejects other pending proposals, and updates platform analytics.

**Auth:** Authenticated **Client** (must own the contract’s project)

**Request body:**

```json
{
  "contractId": 12,
  "payPalOrderId": "5O190127TN364715T",
  "payPalAmountUsd": 500.0
}
```

| Field             | Description                                                  |
| ----------------- | ------------------------------------------------------------ |
| `contractId`      | Contract being funded                                        |
| `payPalOrderId`   | Unique PayPal order ID (idempotent — duplicates rejected)    |
| `payPalAmountUsd` | USD amount captured in PayPal; must equal the contract total |

**Response:** `200 OK` → `PaymentResponse`

**Errors (RFC 7807 ProblemDetails):**

- `401 Unauthorized` — not authenticated
- `403 Forbidden` — caller is not the contract client
- `400 Bad Request` — validation, duplicate order ID, amount/currency mismatch, or invalid PayPal order

On success, the freelancer may receive an SMS (`payment_verified` strategy) when phone is verified.

---

### GET `/api/payments/client/spending`

Total USD spent by the authenticated client (excludes refunded payments).

**Auth:** Authenticated **Client**

**Response:** `200 OK`

```json
{
  "totalSpendingUsd": 1500.0
}
```

---

### GET `/api/payments/freelancer/history`

Released payouts for the authenticated freelancer (net of platform fee).

**Auth:** Authenticated **Freelancer**

**Response:** `200 OK` → `PaymentResponse[]`

---

### GET `/api/payments/admin/transactions`

Platform-wide payment ledger (all statuses).

**Auth:** Admin (`RequireAdmin` policy)

**Response:** `200 OK` → `PaymentResponse[]`

---

### POST `/api/payments/webhook`

PayPal webhook ingress. Signature verified via PayPal `/v1/notifications/verify-webhook-signature` before acceptance.

**Auth:** Anonymous (PayPal only)

**Headers:** PayPal transmission headers (`PAYPAL-TRANSMISSION-ID`, `PAYPAL-TRANSMISSION-SIG`, etc.)

**Body:** Raw JSON event payload from PayPal

**Response:** `200 OK` → `{ "received": true }`

**Errors:**

- `400 Bad Request` — signature verification failed

---

## PaymentResponse Schema

```json
{
  "id": 1,
  "contractId": 12,
  "payPalOrderId": "5O190127TN364715T",
  "amount": 500.0,
  "status": "Escrow",
  "createdAt": "2026-09-21T00:00:00Z"
}
```

**Status values:** `Pending`, `Escrow`, `Released`, `Refunded`
