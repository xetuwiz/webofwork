# Feature Gap Matrix

Status vocabulary: `missing`, `partial`, `divergent`, `equal`, `better`, `intentionally removed`, `backend-dependent`.

| The reference reference | WebOfWork target | Status | Visual gap | Functional gap | API adaptation | Priority |
|---|---|---|---|---|---|---|
| `client/account/*` | `features/client/account/*` | partial/divergent | Compare form/modal layout | Verify token/role/OTP flows | `/api/auth/*` DTOs | P0 |
| `client/onboarding/step1-6` | `features/client/onboarding/*` | partial | Compare step cards/progress | Current onboarding contract | Profiles/phone/skills/packages/media/payment | P0 |
| `components/common/*` | `components/common/*` | partial | Compare public shell and typography | CMS/support/FAQ data | `/api/content/*`, `/api/support/*` | P1 |
| `landingPage/*` | `features/client/landingPage/*` | partial | Compare hero/sections/CTA | Public data/copy | Public content | P1 |
| `client/homepage/*` | `features/client/homepage/*` | partial | Compare header/hero/cards | Public freelancer data | Freelancers API | P1 |
| `client/profile/*` | `features/client/profile/*` | partial | Compare profile layout | Interests/photo/spending | Profile/payments | P1 |
| `client/findtalent/*` | `features/client/findtalent/*` | partial | Compare cards/filter | Invitation action | Freelancers/invitations | P1 |
| `client/projectManagement/*` | `features/client/projectManagement/*` | partial | Compare list/detail/proposal cards | AI trigger/project status | Projects/AI | P0 |
| `client/contract/*` | `features/client/contract/*` | better/partial | Single-deliverable sections | Nested contract response | Contracts/deliverables/revisions/disputes/reviews | P0 |
| `freelancer/dashboard/*` | `features/freelancer/dashboard/*` | partial | Compare sidebar/header/cards | Invitations/todos/data | Freelancer APIs | P1 |
| `freelancer/findJobs/*`, `jobdetails/*` | same targets | partial | Compare cards/detail | Proposal CTA | Projects/proposals | P1 |
| `freelancer/proposals/*` | `freelancer/proposals/*` | partial | Compare list/modal | Package/fee/withdraw | Proposals/packages | P1 |
| `freelancer/invitations/*` | same target | partial | Compare tabs/cards | Status actions | Invitations | P1 |
| `freelancer/offers/*` | same target | better/partial | Compare offer cards/signature | Accept/offer status | Offers/contracts | P0 |
| `freelancer/contracts/*` | same target | better/partial | Compare single-deliverable view | Deliverable/revision/dispute | Contracts/deliverables/revisions/disputes | P0 |
| `freelancer/profile/*` | same target | partial | Compare profile/earnings | Media/payment/earnings | Profiles/payments | P1 |
| `admin/dashboard/*` | `features/admin/dashboard/*` | partial | Compare stat/chart styling | Real DTO data/month changes | Admin dashboard | P1 |
| `admin/categorymanagement/*` | same target | partial | Compare tables/modals | CRUD/constraints | Categories/skills | P1 |
| `admin/usermanagement/*` | same target | partial | Compare table/modals | Admin exclusion/pagination | Admin users | P1 |
| `admin/disputemanagement/*` | same target | better/partial | Compare list/detail | Refund/Release/Split/payment metadata | Disputes/refunds | P0 |
| `admin/payoutmanagement/*` | same target | better/partial | Compare payout table | Eligibility/release/refund | Payouts/refunds | P0 |
| `admin/transactions/*` | same target | partial | Compare table/pagination | WebOfWork payment fields | Transactions | P1 |
| `admin/support/*` | same target | partial | Compare list/detail | Reply/pagination | Support | P1 |
| `admin/settings/CMS/FAQ/reviews` | `features/admin/*` | partial | Compare forms/tables | Current DTOs | Content/settings/reviews | P1 |
| `components/chat/*` | `components/chat/*` | better/divergent | Compare room/message layout | SignalR ChatHub required | Chat REST + `/hubs/chat` | P0 |
| `components/notifications/*` | `components/notifications/*` | better/divergent | Compare notification list | SignalR NotificationHub | Notifications REST + hub | P0 |
| Milestone UI/contracts | None | intentionally removed | Do not add | Single-deliverable contract | No milestone endpoint | intentional |
| Portfolio tags | None | intentionally removed | Do not add | No tag model | No tag endpoint | intentional |
| Skill endorsements | None | intentionally removed | Do not add | No endorsement model | No endorsement endpoint | intentional |
| No the reference AI workflow | `features/aiworkflow/*` | better | Design natively in WebOfWork admin system | Monitor/detail/approval/status/SignalR | AI workflow endpoints | P0 |
| No the reference internal reads/Hangfire | Server-only | better | Not browser-facing | AI infra | Internal API key | intentional |

## Global constraints

- The reference is visual/UX/behavior reference only.
- WebOfWork DTOs/controllers/final plan are API source of truth.
- WebOfWork branding and USD are canonical.
- No the reference strings, currencies, or removed infrastructure.
- Every changed screen needs loading/error/empty states and route/API verification.
