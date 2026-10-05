# Design System Decision

## Decision

Keep the existing WebOfWork custom CSS design system as canonical. Do not introduce Tailwind or replace the current token system as a prerequisite for parity work.

## Canonical language

- Brand: WebOfWork.
- Currency: USD everywhere, formatted through `formatMoney`.
- Canvas: warm neutral `#f4f1eb`.
- Primary text/navigation: deep navy `#17202a`.
- Accent/action: muted gold `#a56f24` / `#d6a84f`.
- Supporting surfaces: `#ebe6dd` and `#fbfaf7`.
- Borders: neutral stone borders such as `#ded8ce`.
- Body copy: slate-gray values currently used in `index.css`.
- Typography: current imported font stack and `fonts.css`; do not introduce the reference brand fonts without a design review.
- Buttons/forms/cards: reuse `.button`, `.form-card`, `.input`, `.card`, and existing shell classes.
- Responsive behavior: preserve current clamp-based layout and mobile breakpoints.

## Why not Tailwind now

The current app already has a coherent, custom WebOfWork visual language across public, client, freelancer, and admin screens. Adding Tailwind during parity work would create a parallel styling system, increase migration risk, and make visual consistency less predictable. the reference class patterns may be translated into the existing CSS primitives instead.

## Reusable the reference patterns

Port these patterns, not the legacy API or branding:

- Page heading/eyebrow hierarchy.
- Card/table/section spacing.
- Modal focus and action grouping.
- Empty/loading/error state structure.
- Form labels, helper text, and validation placement.
- Responsive navigation and sidebar behavior.
- Toast placement and concise success/error messaging.

## Allowed additions

Add a dependency only when a feature needs a capability not present in WebOfWork and the decision is documented. Current WebOfWork dependencies already cover the base system:

- React/Vite
- React Query
- Redux Toolkit
- Axios
- React Hook Form/Zod
- Lucide React
- SignalR
- react-hot-toast
- Vitest

PayPal SDK, signature canvas, chart library, animation library, or Tailwind are not required for Batch 0 and must not be added speculatively. Existing payment/signature/chart implementations remain authoritative until a separate feature decision approves a replacement.

## Product rules

- Never use the reference strings, ₹/INR/LKR/EUR, Razorpay, Stripe, Twilio, MailHog, or removed infrastructure.
- Keep `Currency = "USD"` and `decimal(18,2)` at all financial boundaries.
- Keep SignalR, Hangfire, AI workflow, internal reads, and admin approval.
- Removed concepts remain absent: milestones, portfolio tags, skill endorsements.
- Every new screen uses WebOfWork layout/theme primitives and current API DTOs.
