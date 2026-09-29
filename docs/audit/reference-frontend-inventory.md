# The reference Frontend Inventory

Batch 0 parity reference. the reference is a private visual/UX/behavior reference only; its API calls are not authoritative for WebOfWork.

## Runtime and infrastructure

| Path | Route/area | Purpose | UI/behavior | API/realtime/state | Notes |
|---|---|---|---|---|---|
| `src/App.jsx` | app shell | Providers, global routes, modal/toast boundary | Global composition | Redux/query providers | Adapt transport/auth behavior. |
| `src/main.jsx` | entry | React mount | Starts client | Redux | Reference only. |
| `src/api/axiosInstance.js` | all API | HTTP client | Interceptor attachment | Axios | Remap endpoints. |
| `src/api/axiosInterceptors.js` | all API | Token/refresh | Silent auth behavior | Axios/auth state | WebOfWork refresh contract wins. |
| `src/app/store.js` | global | Redux state | Auth/data state | Redux | Adapt to WebOfWork store. |
| `src/hooks/modal/useModalStore.jsx` | global | Modal switchboard | Open/close/render | Store | Adapt into WebOfWork host. |
| `src/hooks/chat/UseChatRooms.jsx` | chat | Socket/room state | Join/leave/message | Realtime | Replace transport with SignalR ChatHub. |
| `src/hooks/notification/useNotificationSocket.jsx` | notifications | Socket/invalidation | Callbacks | Realtime | Replace with NotificationHub. |
| `src/utils/cloudinaryHelper.js` | uploads | Media validation/upload | File/image behavior | Cloudinary | Keep WebOfWork upload contract. |
| `src/utils/date.js` | common | Date formatting | Display | Utility | Adapt to WebOfWork dates. |

## Public/shared pages

| Path | Route | Purpose | Reference behavior | WebOfWork action |
|---|---|---|---|---|
| `components/common/AboutUs.jsx` | `/about-us` | About content | Long-form page | Adapt to content endpoints. |
| `components/common/ContactUs.jsx` | `/contact-us` | Contact page | Form/layout | Adapt to support/content APIs. |
| `components/common/WhyUs.jsx` | `/why-us` | Value page | Sections/CTA | Keep WebOfWork copy. |
| `components/common/faq/FAQ.jsx` | `/faq` | Public FAQ | Accordion/list | Adapt to FAQ query. |
| `components/common/privacypolicy/PrivacyPolicies.jsx` | `/privacy-policies` | Privacy page | Long-form content | Adapt. |
| `components/common/termsandconditions/TermsAndConditions.jsx` | `/terms-and-conditions` | Terms page | Long-form content | Adapt. |
| `components/common/SupportPage.jsx` | `/support` | Support form | Submit/success/error | Use WebOfWork support DTO. |
| `components/NotFound.jsx` | `*` | Not found | Error state | Match WebOfWork shell. |
| `components/Spinner.jsx` | global | Loading | Spinner | Reuse/adapt. |

## Client, marketplace, and work areas

| Reference path | WebOfWork target | Behavior | Action |
|---|---|---|---|
| `features/client/account/*` | `client/src/features/client/account/*` | Signup, signin, OTP, password, Google, delete | Adapt auth DTOs/roles; never client-select admin. |
| `features/client/onboarding/step1` | `features/client/onboarding/step1` | Identity/phone | Use WebOfWork SMS/phone contract. |
| `step2` | `step2` | Skills | Adapt skills API; remove tags/endorsements. |
| `step3` | `step3` | Packages | USD only. |
| `step4` | `step4` | Portfolio | Keep PortfolioMedia; remove tags. |
| `step5` | `step5` | Payment settings | Real PayPal email/USD. |
| `step6` | `step6` | Finish | Adapt current onboarding APIs. |
| `features/client/profile/*` | `features/client/profile/*` | Profile, settings, interests, spending | Adapt profile/category/payment APIs. |
| `features/client/findtalent/*` | `features/client/findtalent/*` | Browse/invite | Adapt freelancers/invitations. |
| `projectManagement/AddProject.jsx` | `projectManagement/AddProject.jsx` | Create project | Keep AI trigger; WebOfWork DTO/USD. |
| `EditProject.jsx` | `EditProject.jsx` | Edit | Ownership/status adaptation. |
| `MyProjects.jsx` | `MyProjects.jsx` | List | Paginated response. |
| `ProjectDetails.jsx` | `ProjectDetails.jsx` | Detail | Add AI status and current metadata. |
| `projectManagement/proposals/*` | `projectManagement/proposals/*` | Review/hire/reject | Adapt proposal/contract creation. |
| `features/client/contract/*` | `features/client/contract/*` | List/detail/escrow/delivery/revision/dispute/review | Single deliverable; WebOfWork nested DTO. |
| `contract/components/*` | `contract/components/*` | Fund/sign/approve/dispute | Adapt PayPal/contract/dispute APIs. |
| `features/freelancer/findJobs/*` | `features/freelancer/findJobs/*` | Browse jobs | Adapt projects pagination. |
| `features/freelancer/jobdetails/*` | `features/freelancer/jobdetails/*` | Job details/proposal CTA | Adapt project detail. |
| `features/freelancer/proposals/*` | `features/freelancer/proposals/*` | My proposals/modal | Adapt package/USD. |
| `features/freelancer/invitations/*` | `features/freelancer/invitations/*` | Invitations | Adapt statuses. |
| `features/freelancer/offers/*` | `features/freelancer/offers/*` | Offers/sign/accept | Adapt contracts/offers. |
| `features/freelancer/contracts/*` | `features/freelancer/contracts/*` | Contracts/work/revision/dispute | Single deliverable. |
| `features/freelancer/profile/*` | `features/freelancer/profile/*` | Profile/portfolio/payment/earnings | USD/PortfolioMedia. |

## Admin and communication

| Reference path | WebOfWork target | Behavior | Action |
|---|---|---|---|
| `admin/dashboard/*` | `features/admin/dashboard/*` | Stats/revenue/proposal charts | Adapt DTOs/USD. |
| `admin/categorymanagement/*` | `features/admin/categorymanagement/*` | Categories/skills CRUD | Preserve constraints. |
| `admin/usermanagement/*` | `features/admin/usermanagement/*` | Users/modals | Adapt pagination/admin exclusion. |
| `admin/disputemanagement/*` | `features/admin/disputemanagement/*` | List/detail/resolution | Refund/Release/Split. |
| `admin/payoutmanagement/*` | `features/admin/payoutmanagement/*` | Payout release/refund | USD/eligibility. |
| `admin/transactions/*` | `features/admin/transactions/*` | Transactions | WebOfWork response. |
| `admin/support/*` | `features/admin/support/*` | Ticket list/detail/reply | WebOfWork pagination. |
| `admin/faq/*`, terms, privacy, settings, reviews | `features/admin/*` | CMS/admin CRUD | Adapt current APIs. |
| `components/chat/*` | `components/chat/*` | Rooms/messages | Visual parity + SignalR ChatHub. |
| `components/notifications/*` | `components/notifications/*` | List/read/delete | Visual parity + SignalR NotificationHub. |
| No the reference AI workflow UI | `features/aiworkflow/*` | Monitor/detail/approval/SignalR | WebOfWork-only feature. |

## Explicit removals and known issues

- Milestones, `MilestoneDeliverable`, `PortfolioTag`, and `SkillEndorsement` are intentionally removed.
- Do not port the reference payment providers, auth response names, or currency copy.
- Do not trust the reference API paths; use the WebOfWork contract map.
- Keep WebOfWork AI workflow, Hangfire, SignalR, internal reads, admin approval, and USD.
