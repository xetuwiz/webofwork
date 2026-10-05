# WebOfWork Frontend Inventory

Batch 0 inventory of the actual WebOfWork React client. This is the current implementation baseline, not a the reference copy.

## Stack and shared infrastructure

| Path | Actual state | API/state/realtime | Notes |
|---|---|---|---|
| `client/src/main.jsx`, `App.jsx` | Vite React entry and providers | Redux, React Query, modal/toast boundary | New AI/SignalR-capable app. |
| `client/src/index.css` | Canonical custom WebOfWork design tokens/classes | CSS | Warm neutral canvas, navy text, gold accent. |
| `api/axiosInstance.js`, `axiosInterceptors.js` | .NET HTTP/refresh integration | Axios/Redux | WebOfWork contract is authoritative. |
| `app/store.js` | Redux auth/global state | Redux persist | Keep. |
| `hooks/chat/UseChatRooms.jsx` | SignalR ChatHub | ChatHub | Better than the reference transport. |
| `hooks/notification/useNotificationSocket.jsx` | SignalR NotificationHub + AI event invalidation | NotificationHub | Keep and extend. |
| `utils/formatMoney.js` | USD formatter | Utility | Product currency is USD. |

## Feature inventory

| Area | Actual files | State | Backend dependency |
|---|---|---|---|
| Auth/account | `features/client/account/*` | Connected auth/OTP/Google/profile | `/api/auth/*` |
| Onboarding | `features/client/onboarding/*` | Connected six-step flow | profiles/phone/skills/packages/media/payment |
| Landing/public | `features/client/landingPage/*`, `components/common/*` | Connected public pages/CMS/support | content/FAQ/support |
| Client profile | `features/client/profile/*` | Connected profile/interests/spending | `/api/profile/*`, payments |
| Find talent | `features/client/findtalent/*` | Connected list/invitations | freelancers/invitations |
| Projects | `features/client/projectManagement/*` | Connected create/edit/list/detail/proposals | projects/proposals/AI workflow |
| Client contracts | `features/client/contract/*` | Connected list/detail/modals/revisions/dispute | contracts/deliverables/revisions/disputes/reviews |
| Freelancer shell | `layouts/FreelancerLayout.jsx`, `features/freelancer/dashboard/*` | Connected shell/dashboard | profile/payments/notifications |
| Freelancer jobs | `features/freelancer/findJobs/*`, `jobdetails/*` | Connected list/detail | projects/proposals |
| Freelancer proposals | `features/freelancer/proposals/*` | Connected list/modal/detail | proposals/packages |
| Freelancer invitations | `features/freelancer/invitations/*` | Connected list/actions | invitations |
| Freelancer offers | `features/freelancer/offers/*` | Connected list/signature/accept | offers/contracts |
| Freelancer contracts | `features/freelancer/contracts/*` | Connected list/detail/deliverable/revision/dispute | contracts/deliverables/revisions/disputes |
| Freelancer profile | `features/freelancer/profile/*` | Connected profile/media/payment/earnings | profiles/media/payments |
| Admin | `features/admin/*` | Connected dashboard/users/categories/support/CMS/reviews/disputes/payouts/transactions/settings | admin/content/support/payment endpoints |
| AI workflow | `features/aiworkflow/*` | WebOfWork-only monitor/detail/approval/status | `/api/ai/workflows/*` |
| Chat | `components/chat/*` | SignalR chat | `/api/chat/*`, ChatHub |
| Notifications | `components/notifications/*` | SignalR notifications | `/api/notifications/*`, NotificationHub |

## Current frontend tests

- Chat display helper tests.
- Notification display helper tests.
- Contract state display tests.
- Dispute form tests.
- Payment form tests.
- Project schema tests.
- Auth schema tests.
- Onboarding flow tests.
- Admin error-state tests.
- Route protection tests.
- USD formatter tests.

## Known implementation characteristics

- React 19, Vite, React Query, Redux Toolkit, Axios, React Hook Form, Zod, Lucide, SignalR, Vitest.
- The reference-specific Tailwind/Recharts/PayPal SDK/signature canvas are not automatically copied; introduce only when the design decision requires them.
- AI workflow, internal read surfaces, Hangfire, USD money, PortfolioMedia, and explicit WebOfWork approval are retained.
- Milestones, portfolio tags, and skill endorsements are absent by design.
