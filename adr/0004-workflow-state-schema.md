# 0004 — Agent workflow state: normalised tables with `jsonb` payloads, inside the existing database

**Status:** Accepted
**Affects:** `server/WebOfWork.Api/Models/Workflow*.cs`, `ToolCallLog.cs`, `AgentRecommendation.cs`, `ProposalScreening.cs`, `ValidationResult.cs`

## Context

Part 5 requires the workflow to persist its ID, objective, plan, completed steps,
tool results, validation results, errors, approval status and final outcome in
"structured, durable storage".

Part 6 requires a normalised relational database, and separately states: *"Persist
only the Agentic AI workflow state and execution summaries required by the
design; do not store hidden reasoning, passwords, tokens or unnecessary
sensitive data."*

That last clause is a design constraint, not a warning. The AI subsystem produces
two very different kinds of thing — decisions and prose — and only one of them
should survive in the database. Storing raw model prompts and completions would
put client project briefs and freelancer cover letters into `workflow_step_logs`
permanently, readable by anyone with database access and removable by nobody.
§9.4 of the brief states the same rule and requires it to be *enforced*, not
merely intended.

The schema therefore has to satisfy three things at once: queryable state,
auditable execution, and a hard limit on what may be written.

## Options considered

**A. One table, one `jsonb` document per run.** The whole run — plan, steps, tool
results, validation, approval — as a single JSON column.

Rejected on the first query the product needs to ask. "Show the admin every run
currently waiting on a manager", and "show this project its most recent run", are
both filters on status and project. With a JSON document they require a JSON
expression and a scan, and they cannot be served by an index. Worse, the
approval decision — the single most important fact in the system, and the one
whose absence is unrecoverable — would live inside a blob that the application
also rewrites wholesale. Two concurrent updates to different parts of one run
become a lost update.

**B. Fully normalised, no JSON anywhere.** Every tool argument, every validation
issue, every skill-match detail becomes a table.

Rejected as disproportionate. The payloads are genuinely variable in shape per
agent, and normalising them produces a schema that changes every time an agent's
output contract does — a migration per prompt edit, for data that is only ever
read as a whole. It would also store more than is needed: a model-authored reason
string does not need to be a row to be searchable, and it never is queried by
value.

**C. A separate store for agent state — a second database, or a document store
such as MongoDB.**

Rejected on the transactional requirement. The approval that releases invitations
must be committed together with the state change that records the approval, and a
"validate → contract" rule must see the freelancer's current state at the moment
it decides. Across two stores that is a distributed transaction, or an eventual
consistency window in which the validation agent reads a stale rating and approves
a contract it should have blocked. The brief also asks for migrations, seed data
and constraints as one coherent schema; two databases make the ER diagram a lie.

**D. Normalised tables for everything queryable, `jsonb` for payloads that are
only ever read whole.** Chosen.

## Decision

Six tables, all in the existing PostgreSQL database, all reachable through the
existing `DbContext` and migration history:

| Table | Holds | Queryed by |
|---|---|---|
| `workflow_runs` | objective, stage, status, plan, approval decision and approver, final output, resume value | status, project, user, date |
| `workflow_step_logs` | step name, agent, status, duration, input/output summaries | run, step name, date |
| `tool_call_logs` | tool, success, duration, summarised arguments and result | run, tool |
| `agent_recommendations` | one row per candidate: score, the five factor scores, reason, strengths, gaps | run, project, score |
| `proposal_screenings` | one row per proposal: fit score, band, strengths, concerns, rationale | run, proposal |
| `validation_results` | one row per validation: valid, issues, cost breakdown, requires-approval | run, project |

**Queryable facts are columns.** Status is an enum, not a JSON key. A score is a
`numeric(18,4)`, not a number inside a document. The indexes follow the queries:
`(project_id, stage)`, `status`, `created_at`, and foreign keys on every child
table with cascade delete from the run.

**Variable-shape payloads are `jsonb`.** `plan`, `resume_value`, and the step
input/output summaries. `resume_value` is per-stage by nature — a shortlist
approval and a contract approval are different shapes — and the alternative is a
column per stage on a table that would then need migrating every time a stage is
added.

**`final_output` is `text`, deliberately, not `jsonb`.** It is written once and
read once, as an opaque payload for display. Making it queryable would be an
index on data whose query pattern does not exist.

**No raw model I/O is persisted, and this is enforced rather than intended.**
`ai/tools/registry.py` builds a structured summary of every tool call before it is
written, and a test parses the whole Python package — with `ast`, not `grep`, so
that a function signature containing the word `prompt` is not a false positive —
and fails if a persistence call is handed a prompt-shaped argument. A companion
test asserts the summariser actually exists, so the rule cannot be satisfied by
never writing anything.

## Consequences

**What this makes easy.** The admin dashboard is ordinary SQL. "Runs waiting on a
manager" is an indexed filter. The approval path is a single-row update inside an
existing transaction with the invitation creation, so an approval cannot be
recorded without its consequence. Deleting a run takes its children with it, and
the audit trail cannot be left pointing at nothing.

**What this makes hard.** `jsonb` columns are not schema-checked by the database.
A malformed plan would be stored without complaint. That is contained by the
other decision — the Python side validates against a Pydantic model before
writing — but it does mean the database is trusting its only writer, which is
precisely why the AI service is forbidden from writing directly and a test
enforces that ban.

**What it cost.** Six tables and six configurations to maintain, and a schema
that grows a table per new output type. For an audit trail that is the correct
shape: the data outlives the code that wrote it, and someone with SQL access must
be able to answer questions about a run from six months ago without the
application running. A single JSON document would have been less code and a worse
archive.