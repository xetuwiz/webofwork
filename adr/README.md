# Architecture Decision Records

The primary written evidence for **LO4** — *"Select appropriate frameworks, tools,
and agentic AI-assisted approaches to meet specific project and industry
requirements."* — and the document most likely to be opened during the viva.

Five decisions, one page each. Each records the context, the options actually
considered, what was chosen, and what it cost. The consequences section is not
decoration: a decision with no recorded downside is a decision nobody thought
about.

| # | Decision | Status |
|---|---|---|
| [0001](0001-react-state-management.md) | React: Redux Toolkit for session state, TanStack Query for server state | Accepted |
| [0002](0002-flutter-state-management.md) | Flutter: `provider` with `ChangeNotifier` | Accepted |
| [0003](0003-agentic-orchestration.md) | Agentic AI: a custom dispatcher, with `WorkflowRun.Status` as the single owner of pause state — **no LangGraph** | Accepted |
| [0004](0004-workflow-state-schema.md) | Agent workflow state: normalised tables with `jsonb` payloads, inside the existing database | Accepted |
| [0005](0005-deployment-platform.md) | Deployment: one VPS for API, database and AI service; Vercel for the React build | Accepted |

## Why these five

The specification names four as the minimum: React state management, Flutter
state management, the agentic framework and orchestration method, and the schema
strategy for workflow state. Those are 0001–0004. The fifth (0005) is the
deployment platform, also named in the specification, and it is the one with the
largest effect on what the group could afford — which matters because the brief
requires the whole system to be completable on institution-provided or no-cost
services.

Two decisions are deliberately *not* recorded here, because a record of a choice
nobody had a real alternative to teaches nothing:

- **PostgreSQL over another relational database**, and **EF Core as the ORM.**
  There was no deliberation; the module brief mandates them.
- **JWT for authentication.** Mandated by Part 1.

## The shape of these records

Every record answers the same four questions, so they can be read side by side:

- **Context** — what forced a choice, stated as a constraint rather than a
  preference.
- **Options** — what was genuinely considered, including the option we chose to
  *not* take. An ADR that lists only the chosen option is a justification, not a
  record.
- **Decision** — what was chosen, and the reasoning that actually decided it.
- **Consequences** — what this makes easy, what it makes hard, and what it cost.

Where a decision was later revised in the light of something discovered during
implementation, the record says so rather than presenting the final state as
though it had been the plan all along. Where a decision was wrong and had to be
undone, that is recorded too — see 0003, which began as an implementation of the
brief's suggested approach and reversed.

## A note on honesty

These records describe the system as built. Where an option was rejected for a
reason that turned out to be partly wrong, the record says that too. The
specification asks for decisions the group can defend, and a defensible decision
is one whose reasoning survives being questioned — which an ADR written to look
tidier than the actual deliberation will not.