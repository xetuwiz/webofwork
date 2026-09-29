# WebOfWork API Contract Map

Frontend features must use these WebOfWork contracts, not the reference paths. Auth roles come from ASP.NET policies; money is USD; DTOs are used instead of EF entities.

| Feature | Endpoint(s) | Request/response | Auth/role | Notes |
|---|---|---|---|---|
| Auth | `POST /api/auth/send-otp`, `resend-otp`, `verify-otp`, `login`, `google`, `refresh`, `logout` | Auth DTOs; `LoginResponse.accessToken` | Public/auth | Client must not select admin. |
| Profile | `/api/profile/me`, `/api/freelancers/onboard`, phone OTP, skills, packages, portfolio media | Profile DTOs | Client/freelancer | Keep private payment settings hidden publicly. |
| Categories | `/api/categories`, `/api/categories/skills` | Category/Skill DTOs | Public reads/admin writes | `no_pagination` compatibility. |
| Projects | `POST /api/projects`, `GET /api/projects`, `GET /api/projects/{id}`, `/my`, `/recommended` | `CreateProjectRequest`, `ProjectResponse`, `ProjectListResponse` | Client writes; public/freelancer reads | Project detail includes client/applied/count/contract/workflow metadata. |
| Proposals | `/api/proposals`, `/project/{id}`, `/my`, `/{id}/reject` | Proposal DTOs | Client/freelancer by ownership | Use paginated envelope. |
| Invitations | `/api/invitations`, `/received`, `/sent`, `/{id}/status`, `/{id}/cancel` | Invitation DTOs | Client/freelancer | Support status filters/pagination. |
| Contracts | `/api/contracts`, `/{id}`, `/offers`, `/{id}/accept`, `/{id}/status` | Contract DTO + nested data | Participant/admin | Single deliverable; status/role/search/ORD filters. |
| Deliverables | `/api/deliverables/contract/{id}` | `SubmitDeliverableRequest` | Freelancer | File/link; no milestones. |
| Revisions | `/api/revisions/contract/{id}`, `/{id}/action` | Revision DTOs | Participant | One pending revision; accept returns contract active. |
| Disputes | `/api/disputes`, `/contract/{id}`, `/{id}`, `/{id}/resolve` | Dispute/resolve DTOs | Participant/admin | Refund/Release/Split; `PaymentId` returned. |
| Payments | `/api/payments/verify`, client spending, freelancer history, admin transactions, webhook | PayPal/payment DTOs | Client/admin; webhook signature | USD, partial refund ledger, capture ID. |
| Payouts/refunds | `/api/payouts/pending`, `/release`, `/api/refunds` | Payout/refund DTOs | Admin | Payout eligibility and proportional fee. |
| Chat | `/api/chat/rooms`, room messages, delete/archive/read | Chat DTOs | Auth | Realtime via `/hubs/chat`. |
| Notifications | `/api/notifications`, read/read-all/delete | Notification DTOs | Auth | Realtime via `/hubs/notifications`. |
| Support | `/api/support/tickets`, admin tickets/detail/reply | Support DTOs | User/admin | Pagination/filtering. |
| Content | `/api/content/faq`, terms, privacy, CMS | Content DTOs | Public/admin | CMS admin routes. |
| Admin | users/dashboard/charts/settings/reviews | Admin DTOs | Admin policy | Exclude admins from user list; paginate. |
| AI workflow | `POST /api/ai/workflows/start`, `GET /api/ai/workflows`, `GET /api/ai/workflows/{id}`, client `GET /api/ai/workflows/by-project/{id}`, approve/cancel | Workflow DTOs | Client start/status; admin list/detail/approve | Approval rationale persisted; steps included in admin detail. |
| Internal | `/internal/read/*`, `/internal/workflows/*` | Internal DTOs | Internal API key | Never call from browser. |

## Contract adaptation rules

- Never expose EF entities directly to React.
- Validate request DTOs with FluentValidation.
- Use policy-based authorization plus ownership checks.
- Treat `400/403/404/409` as actionable UI states.
- Normalize paginated `{results,count,page,pageSize,totalPages}` responses.
- Use `formatMoney` for USD display.
