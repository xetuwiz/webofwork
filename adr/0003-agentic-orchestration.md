# 0003 — Agentic AI: a custom dispatcher, with `WorkflowRun.Status` as the single owner of pause state

**Status:** Accepted (revised twice during implementation - see *Revision* and
*Second revision* below)
**Affects:** `ai/`, `server/WebOfWork.Api/Services/AiWorkflowService.cs`, `server/WebOfWork.Api/Services/AiWorkflowTrigger.cs`

## Context

Part 5 requires a controlled Agentic AI subsystem that is not a chatbot: a
domain objective, a structured multi-step plan, delegation to at least four
distinct agents, allow-listed tools with validated inputs and structured outputs,
persisted workflow state, deterministic validation, and a high-impact action that
pauses for an authorised human to approve before proceeding.

The brief names LangGraph as the framework used in the labs and lists it first
among the options, with "a custom orchestration approach" as the last of five.

The workflow in question is a client posting a project, the system assembling a
shortlist of freelancers to invite, the client approving that shortlist, and only
then invitations being sent. That last boundary is the whole difficulty: **the
pause is a business fact, not a technical detail.** The state "waiting for a
client" has to be visible to the web client, queryable by the database, and
resumable days later — possibly by a different process, possibly after a
restart.

## Options considered

**A. LangGraph with `interrupt_after` and `Command(resume=...)`.** The suggested
approach, and the one the team started with.

It genuinely does pause and resume, and the checkpointing is the mature part of
the library. The difficulty is what a graph checkpoint *is*: a serialised
interpreter state held in a separate store, addressed by a thread id, describing
where in the graph the run currently sits.

That is a second source of truth for the same fact. The `WorkflowRun` row must
also say `PendingClientApproval`, because that is what the React client polls and
what an admin dashboard lists. Two stores now hold "is this run waiting?", they
are written at different moments by different code, and nothing in the type
system stops them disagreeing. When they disagree — a checkpoint that saved and a
row that did not, or the reverse — the failure is a workflow that is stuck, or
worse, one that resumes and contacts people the client never approved.

**B. A workflow engine: Elsa, WorkflowCore, Hangfire.** Mature, with real
persistence, retries and dashboards.

Rejected as the wrong shape of tool. These are for long-running business
processes with human tasks and timers — order fulfilment, document approval. The
workflow here is six stages completing in seconds, with two human gates that are
already modelled by the `WorkflowRun` status column the rest of the system reads.
Adopting an engine would mean the approval state lives in the engine's store and
the application's copy is derived, which is the same two-sources problem as A,
with more components.

**C. Microsoft Agent Framework / LlamaIndex / Google ADK.** Agent frameworks with
planning, tool-calling and memory.

Genuinely capable, and a reasonable answer for a chatbot-with-tools application.
Rejected because their central abstraction is *the model decides which tool to
call next*. This system's tool selection is **not** the model's: each agent holds
a `ToolBox` restricted to an explicit permission set, and the model never selects
a tool at all. Adopting a framework whose model-driven tool selection was then
forbidden everywhere would mean writing the restriction as a layer on top of the
thing it was meant to be — keeping the dependency and paying for it in code we
would not otherwise write.

**D. A custom dispatcher, with the database row as the only pause state.**
Chosen.

## Decision

`ai/graph/dispatcher.py` maps a `stage` to an ordered set of agents. Each stage
is a plain async function of `(stage, context)` — a pure function of its inputs
with no hidden interpreter state between stages.

**The `WorkflowRun.Status` column is the single owner of pause state.** There is
no checkpoint store, no thread id, and no resume token. Resuming is an ordinary
API call: the client approves through `POST /api/ai/workflows/{id}/approve` and
the row moves to `Approved`.

An earlier version of this paragraph added that "the outreach stage is dispatched
as a fresh invocation with the approved shortlist as its input". **That was
false, and it is worth recording why, because the error is instructive.**
`ResumeWorkflowAsync` sets `Status = Running` and re-dispatches `run.Stage`, which
for a shortlist run is still `talent_match` — it never assigns
`WorkflowStages.Outreach`. So the Python outreach stage does not run in
production, and the only caller of `POST /internal/actions/send-invitations`
anywhere in the .NET codebase is *nothing*: invitations are created entirely in
`ApproveShortlistAsync`, which calls `invitations.CreateInvitationAsync` directly.

The correction, once found, is to state what is actually true rather than to
delete the sentence: **the outreach decision is a .NET concern.** The agentic
part of the system is choosing *who to consider*; the part that contacts a person
is deliberately not agentic, and the enforcement of that is now structural —
there is no path from the model to a human at all.

The status values are `PendingClientApproval` and `PendingManagerApproval`, which
is why they are `9` and `10` and not the `2` and `3` a fresh enum would assign:
the column is already in production data, and reusing an existing integer for a
different meaning would silently reinterpret every run already recorded.

The consequence worth stating plainly: **there is no mid-stage resume.** If the
matcher fails after the planner has run, the run is restarted from the stage
boundary, not from the failed tool call. That is a real reduction in capability
against LangGraph, accepted because a partial recomputation of a five-second
stage is not worth a second source of truth for approval state — and because the
deterministic stages are cheap, while the expensive stage (the model call) is the
one that is retried anyway.

The Python stages also never write to the database. Every read goes through
`/internal/read/*` and every write through `/internal/workflows/*`, which is what
keeps the .NET side the only writer and keeps every write behind its audit and
approval logic. A test enforces this by parsing the package for database drivers
and raw SQL, because it is a property of absence that no functional test can
observe.

## Consequences

**What this makes easy.** One query answers "is this run waiting, and on whom?"
— the same query the dashboard, the client screen and the resume path all use.
Adding a stage is a function and an entry in a dictionary. The whole orchestration
is readable in one file, which matters when the viva asks why a stage runs in
that order.

**What this makes hard.** No per-step retry, no partial resume, and no
visualisation of run state beyond what the audit tables already provide. If a run
is long enough that resuming matters, this decision is wrong and should be
revisited. Debugging a paused run means reading rows in three tables rather than
inspecting a checkpoint.

**What it cost.** Roughly a thousand lines that LangGraph would have supplied,
and the loss of its ecosystem. It also means no automatic checkpoint on
crash — a Python process that dies mid-stage leaves the row in `Running`, which
is detected and marked failed rather than resumed.

## Revision — this decision was reversed once

The first implementation followed option A. It was abandoned not because
LangGraph failed, but because the checkpoint and the `WorkflowRun` row both
claimed ownership of approval state, and a test that exercised the two together
found the disagreement rather than preventing it. Recording this because the
alternative — presenting the custom dispatcher as the original plan — would be a
false account of how the decision was made, and would not survive a viva
question about why not to use the framework the brief recommends.

The honest answer to "why not LangGraph" is: **we tried it, and the pause state
could not have exactly one owner.**

## Second revision — the first revision argued from pause state alone, which was a category error

The argument for the custom dispatcher rests entirely on *pause state*, and that
argument still holds. `WorkflowRun.Status` remains the single owner of "is this run
waiting, and on whom", and a LangGraph checkpoint would be a second store for the
same fact. That part has not changed.
What changed is what this ADR implied about **agentic control flow**, and here it
was wrong in a way that cost marks.
The custom dispatcher set the stage sequence in Python:
```python
STAGES = frozenset({"draft", "talent_match", "screen", "validate", "outreach"})
```
and every tool call was a hard-coded line — 20 of them across the agents, against
6 model calls. So the model chose *no* tools, *no* order, and *no* stopping point.
Every agent produced prose that was attached to a decision code had already made.
That satisfies most of the Part 5 acceptance criteria. It does not satisfy the
three that make a system agentic:
- "analyses a user objective, creates a **structured multi-step plan**"
- "**delegates** steps to distinct agent roles"
- "**calls** allow-listed tools using validated inputs and structured outputs"
and it does not satisfy Lecture 05's definition of an agent, which is an LLM
"directing a loop: it observes state, decides on actions, a runtime executes them,
and results feed back."
The gap was not argued away; it was missed. A previous version of this document
used the lecture's own "choose the least autonomy that solves the task" heuristic
as justification. That heuristic governs an *application* design decision. It does
not apply to whether a subsystem meets a stated acceptance criterion, and citing it
here was a category error — it defended a gap by misreading the requirement it
was being measured against.
**Correction.** The planning agent now runs a real model-directed tool loop
(`ai/agents/tool_loop.py`). The model is advertised a set of read-only tool
schemas and decides which to call, with what arguments, in what order, and when to
stop. Measured against the live provider chain, one planning run produced five
tool calls across three turns and terminated on its own:
```
step 0: get_project, list_categories, get_agent_thresholds
step 1: search_freelancers, get_similar_projects
step 2: search_freelancers
step 3: (no tool calls — the model produced the plan)
```

One caveat on that transcript, because it is the evidence for the whole
agentic claim and it was originally quoted more generously than it deserves:
`planning_agent.py:237,243` calls `get_project` and `get_agent_thresholds`
**before** the loop starts, and pastes both results into the objective. So of the
three calls on step 0, only `list_categories` was a model decision, and the other
two were already in hand. The loop is real and the later turns are the model's,
but the first turn flatters it. The fix is either to stop pre-fetching or to drop
those two from `PLANNER_TOOL_SCHEMAS` so the advertised surface matches what the
model is actually choosing between; advertising a tool whose answer is already
pasted into the prompt is the same drift the advertised-vs-permitted guard was
written to catch, one level up.
Matching and screening remain deterministic, and that is a *stated* split rather
than an accident: a score that determines who is hired has to be reproducible and
explainable, and the assignment asks for deterministic validation. The model gains
authority over *which* steps run, never over the numbers a decision rests on.

**The draft agent was made agentic on the same pattern, and doing so found two
production bugs in the code that was supposed to prevent them.** `_ground` is
the last thing between a model's output and a posted project, and it was silently
discarding every real skill id: it validated skills against *category* ids, and
iterated `skill_names` so an ids-only response matched nothing. Both halves failed
quietly — no exception, and a draft that still read as plausible. They were
invisible until the model could call `list_skills_by_category` and return ids it
had genuinely looked up; a live run then came back saying "Skill id 11 is not a
real row and was dropped" for an id that existed. Third fix, same method: the
category-name fallback was unreachable, because the name was cleared even when no
id had been proposed.

That is the argument for the change beyond "it sounds more agentic". The bugs were
unreachable by unit test and by inspection, because both the correct and incorrect
outcomes looked like a working draft. Only a model that could *choose its own
inputs* produced the shape that exposed them.

The hand-rolled loop rather than `create_agent` for four reasons: the transcript
has to be inspectable for the evaluation; the existing provider chain already does
key rotation, rate-limit backoff, caching and budget enforcement, all of which a
framework's `bind_tools` would bypass; the tool executor is `ToolBox`, which
already owns the per-agent allow-list and must not be duplicated; and the three
guardrails from Lab 05 (unknown tool, tool raises, runaway loop) are enforceable
by hand in a way that is directly testable without a network.

The advertised-vs-permitted check exists because it failed once, silently: the
planner was shown five tools and permitted three, and because the loop turns a
refusal into an observation the agent kept working and nobody noticed it had been
working blind. Both agentic agents are now asserted equal to their permission set.

**Unchanged:** `WorkflowRun.Status` still owns pause state. LangGraph is still
rejected, and now for the stated reason alone rather than a mixed one.

## Who chose the agent sequence, and where the line is drawn

This is the question most likely to be asked in the viva, so it is answered here
rather than left to be inferred from the code.

### The short answer

**ASP.NET Core chooses the sequence. The model chooses the steps within one
stage.** Neither half owns both.

### Why .NET drives

`WorkflowRun.Status` is a column in the application's own database, written by
the application, read by the React approval screen and the admin dashboard, and
used to decide what happens when a client clicks Approve. A run can be paused for
days, resumed from a different process, and survive a restart of both services.

That requires one durable owner of the pause decision. Handing sequencing to a
Python-side supervisor would mean a second thing that knows what the run should
do next, held in a different process, under a different lifetime — which is the
same dual-ownership problem that caused LangGraph to be rejected in the first
place. **The custom dispatcher is not a preference; it is the mechanism that
keeps the pause state single-owner.**

The dispatcher also runs exactly **one stage per request**, dispatched by
`req.stage`. There is no loop in it to supervise, and no sequence held in memory.

### Why the planner emits the sequence anyway

`PlanningAgent` produces `delegated_agents` and `plan_steps` — the ordered list of
agents the plan calls for, with a rationale each. .NET records the plan and
dispatches each step to the distinct agent that owns it.

So "delegates steps to distinct agent roles" is satisfied, and the delegation is
recorded, inspectable and replayable. What it is not is *authoritative*: if the
plan names four agents, .NET runs those four, in the order the workflow's
approval gates require, whether or not the model would have chosen differently.

### The line, stated once

> **The model chooses what it needs to know. Code decides what happens to a
> person.**

| Agent | Model chooses | Code owns | Why |
|---|---|---|---|
| planning | which read-only tools, in what order, when to stop | the plan is normalised into bounds | the next step is genuinely unknowable in advance |
| draft | which catalogue lookups it needs | grounding drops invented ids; validation rejects a malformed draft | id validity is a fact, not a judgement |
| matching | nothing — prose only | every factor, the weights, the order, the shortlist | a score deciding who is hired must be reproducible |
| screening | nothing — rationale only | fit score, band, ordering | same |
| validation | nothing — summary only | the pass/fail verdict | it exists to be independent of the model |
| outreach | nothing — message prose | **who is invited, and only after approval** | the only capability that reaches a human |

Five of six agents decide nothing about outcomes. Two of the six are
model-directed — planning and draft — and in both the model's authority stops at
*reads*. That is a **stated split, not an omission**, and it is what makes the 22
golden cases able to assert exact scores and orderings while the tool-calling
agents stay model-directed.

### What would change if this were wrong

If the supervisor pattern were adopted — a model choosing which agent acts next —
then `test_ordering.py::test_talent_match_runs_validation_before_it_can_contact_anyone`
would stop being true by construction and become true only by prompt. That test is
named "the load-bearing assertion" for a reason: one reordering of `outreach`
before `validation` sends a freelancer an invitation the system never checked,
and §9.1's requirement that a high-impact action "pause until an authorised user
approves" stops meaning anything, because "before" is no longer guaranteed.

A supervisor is therefore a defensible design. It is not *this* design, and the
cost of it here is the property that makes the approval gate verifiable.
