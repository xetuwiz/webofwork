# Cross-application parity final report

Date: 2026-09-25

## Scope completed

Parity work was completed for the requested accounts, categories, profiles, projects, proposals and invitations, contracts/deliverables/revisions, payments/payouts/refunds, communication/WebSockets, common/public content, and adminpanel areas.

## Route families

- Accounts: OTP registration/verification, login, refresh, me, role switching, Google, password reset, password change, deletion, and phone/profile operations.
- Categories: category and skill CRUD, pagination, filtering, skill resolution, and deletion protection.
- Profiles: client and freelancer profile views, onboarding, role switching, phone verification, freelancer listing, payment settings, reviews, and todo routes.
- Projects: project creation/list/detail/update/delete, freelancer project filtering, recommendations, proposals, and invitations.
- Contracts: contract list/detail/offers, acceptance, status changes, deliverables, revisions, and dispute-related compatibility.
- Payments: payment verification, payout release, refunds, summaries, transaction history, and platform transactions.
- Communication: rooms, messages, notifications, contact-information validation, raw WebSocket bridge, and existing SignalR hubs.
- Common: platform percentage, support ticket creation, terms, privacy policy, and FAQ.
- Admin: users, dashboard statistics, revenue/proposals charts, pending payouts, global settings, support tickets, CMS, and FAQ aliases.

## Compatibility decisions

- Existing WebOfWork routes were preserved and the reference paths were added as aliases where needed.
- Responses use the global snake_case JSON configuration.
- Decimal values are serialized as strings, dates use UTC ISO-8601 with three fractional digits, and enums serialize lowercase.
- The reference crashes and invalid assumptions were not ported where they would prevent safe execution.
- Existing SignalR hubs continue to work; the raw WebSocket bridge is separate.

## Verification

Command:

```text
dotnet build server/WebOfWork.Api
```

Final result: build succeeded with 0 errors and 9 existing warnings. The remaining warnings are pre-existing package/security and nullable/unused-variable warnings outside the new compatibility code.
