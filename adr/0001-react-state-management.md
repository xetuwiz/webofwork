# 0001 — React: Redux Toolkit for session state, TanStack Query for server state

**Status:** Accepted
**Affects:** `client/`

## Context

The React application is the staff-facing side of the platform: project and
proposal management, contract and payment administration, dispute handling,
dashboards, and the Agentic AI monitoring and approval screens.

Two kinds of state live in that application, and they have opposite
requirements:

- **Session state** — who is signed in, what role they hold, the access token.
  It must survive a page refresh, it must be readable synchronously by route
  guards before any request is made, and logging out must clear it
  unconditionally.
- **Server state** — the project list, a proposal's screening result, a workflow
  run's current status. It is owned by the database, changes without the user's
  involvement, and is worthless if it is stale.

The failure mode to avoid is a single store holding both, because then every
component needs to know which rules apply to which kind of data.

## Options considered

**A. Context API only.** React's built-in mechanism. Nothing to install, and
adequate for session state.

Rejected for server state: there is no cache, no request deduplication, no
invalidation, and no background refetch. Every screen would hand-roll a
`useEffect` with a loading flag and an error branch. With 33 backend endpoints
and screens that each read several of them, this is a large amount of duplicated
asynchronous code — and duplicated code is where the loading and error states go
missing.

**B. Redux Toolkit for everything.** One store, one set of rules.

Tempting, and it is what a single-store design implies. Rejected because it puts
server data into a store that must then be invalidated by hand. An
`invitations` slice has no automatic relationship to the invitation that was
just created on the server; somebody has to remember to dispatch a refetch. That
bookkeeping is invisible, so it is skipped, so the screen shows stale data after
an action that visibly succeeded — the class of bug that is reported as "the app
is broken" and is actually "the app is correct about information it no longer
has".

**C. Zustand.** Smaller API surface than Redux Toolkit, no provider nesting.

Rejected only on maturity-of-integration grounds: the existing codebase is
Redux-based, and adding a second state library to share session state would mean
two mechanisms for one job.

**D. Redux Toolkit for session state only, TanStack Query for server state.**
Chosen.

## Decision

Use **Redux Toolkit** with exactly one slice — `auth`, holding the user and
access token — persisted through `redux-persist` to `localStorage`. Use
**TanStack Query** (`useQuery` / `useMutation`) for every server-backed read and
write, with axios as the transport and a single interceptor pair attaching the
bearer token and handling 401.

The dividing line is **who owns the data**. If the database owns it, it goes
through a query hook; if the browser owns it, it goes in the store.

The result is a deliberately small Redux surface: one slice, 75 `useSelector`
call sites, and `createApi` from RTK Query is used zero times — TanStack Query
is used directly through its hooks rather than through its Redux integration, so
the two libraries do not overlap.

The store also carries a logout reset. `auth/logout` resets the root reducer to
`undefined` and removes the persisted key, rather than dispatching a field-by-field
clear. A field-by-field clear is the version where one forgotten field survives
logout and reappears on the next sign-in as the previous user's data.

## Consequences

**What this makes easy.** Cache invalidation is declarative: a mutation
invalidates the queries whose data it changed, and the screen updates. Loading
and error states come from the query's own status rather than from local flags,
so they cannot disagree with the data. Route guards read the token synchronously
from the store, so a protected route never renders and then corrects itself.

**What this makes hard.** Two state mechanisms means two things to learn, and a
developer who puts server data in Redux by mistake will get no error — it will
simply never refresh. The one-slice discipline is therefore a convention, not
something the types enforce, and it is worth stating in review.

**What it cost.** Two dependencies rather than one, and `redux-persist` writes the
access token to `localStorage`, where any script on the origin can read it. That
is a real exposure and it is deliberate for the web client: the mitigation is a
short-lived access token plus a refresh token, and the alternative — keeping the
token only in memory — means a browser refresh signs the user out, which is a
worse outcome for a staff tool used all day. The mobile client does not make this
trade: its tokens are in the platform keystore, because on a device the storage
is not sandboxed per-origin and "any script" is any app that gets the backup.

**Revisit if.** The web client gains a second genuinely client-owned concern —
an offline draft queue, or a multi-tab edit-conflict rule. At that point a
second slice is justified and the store stops being unusual.