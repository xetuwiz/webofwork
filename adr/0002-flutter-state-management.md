# 0002 — Flutter: `provider` with `ChangeNotifier`

**Status:** Accepted
**Affects:** `mobile/`

## Context

The Flutter application is the freelancer-facing side: browse projects, submit
proposals, respond to invitations, manage contracts and deliverables, track
payments, and see the AI reasoning attached to a match.

It is a single-developer-built app with roughly one hundred screens, a live
WebSocket for chat and notifications, and a handful of long-lived services that
outlive any one screen: the socket, the connectivity monitor, the translation
service, and the onboarding state.

The requirement is a justified state-management approach, with reusable widgets,
routing, and secure API integration.

## Options considered

**A. `setState` and inherited state only.** No dependency.

Rejected at the scale of a hundred screens. The failure is not `setState` itself
but that a service which needs to push an event to whichever screens are
currently mounted — the WebSocket delivering a message — has no way to reach them.
The pattern that emerges is a chain of callbacks through constructors, which
breaks the moment a screen is rebuilt for an unrelated reason.

**B. Riverpod.** The strongest option on paper. Compile-safe providers, no
`BuildContext` in logic, good test story, and provider overrides make tests
clean.

Genuinely tempting. Rejected for one reason: it requires the dependency-scoped
refactor that 0001 deliberately avoided. Introducing Riverpod means every screen
that reads state has to be rewritten to `ref.watch`, and the two approaches then
coexist across the app with nothing to say which is correct where. The team had
four people, one of whom owns this component, and a mid-project rewrite of a
hundred screens is how the other components get late.

**C. Bloc / Cubit.** Event-and-state, strong separation, good tooling.

Rejected as more ceremony than the problem needs. Cubit removes most of that
objection, but the event stream is still structure imposed on what is mostly
"load some data, show it, react to a socket event".

**D. GetX.** Very small amount of code; combines state, routing and dependency
injection.

Rejected on maintainability grounds. GetX's routing and DI are convenient until
a route needs a parameter that is not a string, at which point the framework's
own routing table becomes the obstacle. Its reactivity is also global by default,
which makes "why did this rebuild?" a question about the whole app rather than
about one provider.

**E. `provider` with `ChangeNotifier`.** Chosen.

## Decision

Use the `provider` package. Long-lived services are declared once in
`helper/providers.dart` as `ChangeNotifierProvider`s and read with
`context.watch<T>()` / `context.read<T>()`. Form and screen state stays local to
the widget.

`WebSocketService` is the one deliberate exception: it is a plain `Provider`,
not a `ChangeNotifierProvider`, because the socket surfaces events through the
app's navigation key and its own listeners rather than by rebuilding widgets —
and giving it a `ChangeNotifier` would mean every socket frame marks a subtree
dirty for no visible change.

Screen-local state is deliberately *not* hoisted. A form's field values belong to
the form. Promoting them to a provider because hoisting is the habit turns a
twenty-line widget into one that reads from four services and cannot be read on
its own.

## Consequences

**What this makes easy.** Any screen can reach any service with one line and no
constructor plumbing. A service survives navigation, which is what the socket and
the session need. `ChangeNotifier` is part of the Flutter SDK, so there is no
third-party reactivity to learn, and a `ChangeNotifier` can be constructed
directly in a test with no provider harness at all — which is why the suite
reaches 290 tests without a mocking framework.

**What this makes hard.** `context.watch<T>()` inside a `build` subscribes the
widget to that service, so a `notifyListeners()` from a busy service — a socket
frame, a connectivity poll — rebuilds every subscriber. The discipline that keeps
this cheap is that services notify on meaningful change rather than on every
mutation, and the places where that discipline is not followed are the places
where a screen rebuilds more than it needs to. There is also no compile-time
guarantee that a provided type is registered: a missing registration surfaces as
a runtime `ProviderNotFoundException`, not a build error.

**What it cost.** Roughly one dependency, and a `BuildContext` reaching into
service logic — `use_build_context_synchronously` appears across the codebase
wherever a `BuildContext` is used after an `await`. That is the sharpest edge in
this decision and it is a real one: the correct fix is a `mounted` check on the
`State`, and the existing occurrences mostly predate this choice.

**Revisit if.** The app gains derived or composed state — for example, a
shortlist filtered by availability, rating and price simultaneously, recomputed
from three sources. That is the point where `context.watch` chains stop being
readable and a derived-state mechanism earns its keep. Riverpod's `ref.watch`
composition is the natural upgrade, and the migration is incremental because the
services are already the seams.