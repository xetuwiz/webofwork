# Revisions API

## Endpoints

| Method | Route | Auth | Description | Payload |
| --- | --- | --- | --- | --- |
| `POST` | `/api/revisions/contract/{contractId}` | Client | Request a revision | `RequestRevisionRequest` |
| `POST` | `/api/revisions/{id}/action` | Freelancer | Accept or reject a revision | `RevisionActionRequest` |
| `GET` | `/api/revisions/contract/{contractId}` | Client/Freelancer | Get revisions for a contract | None |

### `RequestRevisionRequest`
- `Reason` (string, required, max 1000)

### `RevisionActionRequest`
- `Accept` (boolean, required)
- `RejectionMessage` (string, optional)
