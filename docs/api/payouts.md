# Payouts API

## Overview
Admin release of escrow to freelancers via PayPal Payouts after contract completion and the eligibility window (`PayoutEligibleAfter`). Net payout amount is **payment amount minus platform fee**, in **USD**.

Mirrors `server/adminpanel/views/payout_request_views.py`.

---

## Endpoints

### GET `/api/payouts/pending`
List escrow payments ready for release: contract **Completed**, `PayoutEligibleAfter` in the past, status **Escrow**.

**Auth:** Admin (`RequireAdmin` policy)

**Response:** `200 OK` → `PaymentResponse[]` (amount field is net USD to freelancer)

---

### POST `/api/payouts/release`
Send PayPal payout to the freelancer’s account email and mark payment **Released**.

**Auth:** Admin (`RequireAdmin` policy)

**Request body:**
```json
{
  "paymentId": 42
}
```

**Response:** `200 OK`
```json
{
  "success": true
}
```

**Errors:**
- `400 Bad Request` — payment not found, not in Escrow, already released, contract not completed, or before eligibility window
- `500` / ProblemDetails — PayPal payout API failure

On success, the freelancer may receive an SMS (`payout_released` strategy) when phone is verified.

---

## PaymentResponse Schema
Same as [Payments API](./payments.md#paymentresponse-schema). Pending list uses net USD in `amount`.
