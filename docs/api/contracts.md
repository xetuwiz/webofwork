# Contracts API

This API handles contract generation, status lifecycle, and PDF legal document generation.

## Endpoints

### `GET /api/contracts`
Gets all contracts for the current user (as client or freelancer).

**Returns**: `200 OK` with `List<ContractResponse>`

### `GET /api/contracts/{id}`
Gets a specific contract by ID.

**Returns**: `200 OK` with `ContractResponse` or `404 Not Found` or `403 Forbidden`

### `GET /api/contracts/offers`
Gets all contract offers (status = `Offered`) for the current freelancer.

**Returns**: `200 OK` with `List<ContractResponse>`

### `POST /api/contracts/{id}/accept`
Accepts a contract offer (Freelancer only). This signs the contract and triggers PDF generation via QuestPDF and upload to Cloudinary.

**Body**:
```json
{
  "signature": "John Doe",
  "signatureType": "type"
}
```

**Returns**: `204 No Content`, `400 Bad Request`, or `403 Forbidden`

### `PATCH /api/contracts/{id}/status`
Updates the status of a contract following valid state machine transitions (e.g., Active -> Submitted -> Completed).
If marked as `Completed`, `PayoutEligibleAfter` is automatically set to 7 days in the future.

**Body**:
```json
{
  "status": "Completed"
}
```

**Returns**: `204 No Content`, `400 Bad Request`, or `403 Forbidden`
