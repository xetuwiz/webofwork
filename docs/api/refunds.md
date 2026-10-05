# Refunds API

## Overview
Allows admins to refund payments from escrow back to the client. This reverses platform analytics and changes contract status to `Refunded`.
Refunds cannot be triggered by freelancers directly.

All stored payment, contract, and analytics values remain USD `decimal(18,2)`. The PayPal refund adapter sends USD via `PayPal:Currency`.

## Endpoints

### POST `/api/refunds`
Refunds a payment that is currently in `Escrow`.

**Auth:** `[Authorize(Policy = "RequireAdmin")]`

**Request Body:**
```json
{
  "paymentId": 1,
  "reason": "Client requested refund"
}
```

**Response 200:**
```json
{
  "success": true
}
```

**Errors:**
- `400` Payment not found / already refunded / not in Escrow
