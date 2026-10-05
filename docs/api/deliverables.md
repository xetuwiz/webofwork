# Deliverables API

## Endpoints

| Method | Route | Auth | Description | Payload |
| --- | --- | --- | --- | --- |
| `POST` | `/api/deliverables/contract/{contractId}` | Freelancer | Submit a deliverable | `SubmitDeliverableRequest` |
| `GET` | `/api/deliverables/contract/{contractId}` | Client/Freelancer | Get deliverables for contract | None |

### `SubmitDeliverableRequest`
- `Title` (string, required, max 255)
- `FileUrl` (string, optional)
- `LinkUrl` (string, optional)
- `Notes` (string, optional, max 2000)
*Note: Either FileUrl or LinkUrl must be provided.*
