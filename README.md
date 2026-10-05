# WebOfWork

An integrated freelance marketplace: a React web application for clients and
administrators, a Flutter mobile application for freelancers, an ASP.NET Core
Web API over PostgreSQL, and an Agentic AI subsystem that assembles and validates
a shortlist of freelancers for a posted project — pausing for a human decision
before it contacts anybody.

**Course:** SE3090 Software Engineering Frameworks — Assignment 1
**Stack:** ASP.NET Core (net8.0) · PostgreSQL · React · Flutter · Python (Agentic AI)

---

## 1. The business problem

Shortlisting freelancers is slow and inconsistent. A client posting a project has
to read every profile, compare ratings, portfolios and prices by eye, and decide
who to invite. Two clients evaluating the same project reach different lists, and
neither can say why they chose the people they chose.

The cost is not only the client's time. Freelancers apply to projects they are
poorly suited to, because they cannot tell what a client actually values. And the
platform has no record of *why* a match was made, so when a freelancer asks "was
I considered?", nobody can answer.

WebOfWork addresses the first two. A client posts a project; the Agentic AI
subsystem reads the project, plans how to evaluate it, scores the freelancer
pool against explicit weights, validates the result against fixed business rules,
and presents a ranked shortlist **with its reasoning attached** — then stops and
waits. The client approves, rejects or adjusts the weights and re-runs. Only
after approval are invitations sent.

That last boundary is the design. Everything the system decides is a
recommendation; the only thing it does on its own is nothing irreversible.

## 2. User roles

| Role | Application | What they do |
|---|---|---|
| **Client** | React | Posts projects, reviews AI shortlists, approves or rejects them, invites, funds contracts, resolves disputes |
| **Freelancer** | Flutter | Completes a profile and portfolio, browses and applies to projects, responds to invitations, manages contracts and deliverables, tracks payouts |
| **Administrator** | React | Manages users, roles, categories, skills, platform settings, commission, content; monitors every AI workflow |

Authentication is JWT with role-based authorisation on the server; every
authorisation decision is made in the API, never in a client.

## 3. Features by component

Each component below is owned by one student, who is responsible for its API
endpoints, database entities, React and Flutter interfaces, tests, documentation
and a distinct Agentic AI contribution.

1. **Projects, proposals and invitations** — posting, search, filtering, sorting
   and pagination; proposals with bids and delivery estimates; invitations with
   accept/decline and expiry.
2. **Contracts, payments and payouts** — contract lifecycle, PayPal capture and
   release, commission calculation, freelancer payout settings, escrow states.
3. **Disputes, messaging and notifications** — dispute raising and resolution,
   SignalR chat, in-app and push notifications, support tickets.
4. **Profiles, portfolios and talent discovery** — freelancer profiles, skills,
   packages, portfolios with media, reviews, ratings, saved searches — and the
   Agentic AI shortlisting that sits on top of them.

A fifth, **shared**, foundation spans all four: authentication, authorisation,
the shared API contract, third-party integrations, and the Agentic AI subsystem
itself.

## 4. Technology choices, and why

The reasoning is recorded in full in [`adr/`](adr/README.md) — five decisions,
each with the options considered and what the choice cost.

| Area | Choice | One-line reason |
|---|---|---|
| React state | Redux Toolkit (session) + TanStack Query (server) | Two kinds of state with opposite rules; the store owns only what the browser owns |
| Flutter state | `provider` + `ChangeNotifier` | Adequate for a hundred screens without a mid-project rewrite of every one of them |
| Agent orchestration | Custom dispatcher; `WorkflowRun.Status` owns pause state | A graph checkpoint and the database row would both answer "is this waiting?" — and they can disagree |
| Workflow state | Normalised tables, `jsonb` payloads, same database | Approval and its consequence must commit in one transaction |
| Deployment | One VPS (API, database, AI) + Vercel (React) | Four stateful components on no-cost services; managed Postgres is not free |

## 5. System architecture

```
┌──────────────┐        ┌─────────────────────────┐       ┌──────────────┐
│   React      │        │    ASP.NET Core Web API  │       │   Flutter    │
│  (clients,   │◄──────►│   JWT · RBAC · business  │◄──────►│ (freelancers)│
│    admins)   │  HTTPS │   rules · EF Core        │  HTTPS │              │
└──────────────┘        └───────────┬─────────────┘       └──────────────┘
      │                             │                             │
      │  SignalR /ws/chat            │  EF Core                   │
      │  /ws/notifications           ▼                             │
      └──────────────────────►┌──────────────┐                     │
                               │  PostgreSQL  │◄────────────────────┘
                               │  28 migrat.  │   (via the same API only)
                               └──────────────┘
                                     ▲
                                     │ internal reads / writes
                                     │ X-Internal-Api-Key, never public
                                     │
                        ┌────────────┴─────────────┐
                        │   Python Agentic AI      │
                        │   6 agents · allow-listed │
                        │   tools · 10 rules       │
                        └──────────────────────────┘
```

**The mandatory rule holds in both directions.** React and Flutter call only the
ASP.NET Core API. The Python service is called by ASP.NET Core and by nothing
else; it reaches PostgreSQL only through the `/internal/read/*` and
`/internal/workflows/*` endpoints, which is enforced by a test that parses the
Python package for database drivers and raw SQL.

## 6. Agentic AI architecture

Six agents, each with a distinct responsibility, a typed input and output
contract, and its own tool permissions.

| Stage | Agent(s) | Responsibility |
|---|---|---|
| `draft` | **Draft** | Turns free text into a project draft whose category and skill IDs are real database rows |
| `talent_match` | **Planning** → **Matching** → **Validation** | Chooses weights and hard filters; scores the pool; applies the deterministic gate |
| `screen` | **Screening** | Ranks incoming proposals against the project, with fit bands |
| `validate` | **Validation** | Runs the ten business rules before money moves |
| `outreach` | **Outreach** | Sends invitations — only after approval |

**The two human gates.** A client approves the shortlist
(`PendingClientApproval`); a manager approves a contract
(`PendingManagerApproval`). Both are enforced on the server, both are separate
calls, and the second is not reachable without the first.

**What the model decides, and what it does not.** The language model writes prose
and classifies into closed sets. It never writes a score, a price, a verdict or
an ID. Scoring is a formula in code; validation is ten deterministic rules; every
ID that reaches the database is checked against rows that were actually read.
Tests pin this: the same shortlist scored with the model answering and with the
model switched off produces identical numbers.

**Safety properties enforced by tests, not by convention:**

- Only the Outreach agent holds the tool that contacts a person, and it refuses
  without an approval.
- No path reaches `send_invitations` before validation has run.
- No raw prompt or completion is ever persisted — a test parses the package and
  fails if a persistence call is handed a prompt-shaped argument.
- Unknown fields in a model response are rejected rather than silently dropped.
- A missing fixture fails loudly instead of inventing an answer.

Full detail: [`ai/docs/architecture.md`](ai/docs/architecture.md),
[`ai/docs/agent-contracts.md`](ai/docs/agent-contracts.md),
[`ai/docs/evaluation.md`](ai/docs/evaluation.md).

## 7. Database design

PostgreSQL, 44 entities, 28 EF Core migrations, audit fields on every table,
soft deletes where history matters. Table and column names are `snake_case`,
applied globally by `UseSnakeCaseNamingConvention()` rather than per entity — so
a new entity cannot introduce a differently-named table.

The ER diagram is **generated from the live EF model** rather than drawn by hand:
[`ER-DIAGRAM.md`](ER-DIAGRAM.md). A test regenerates it and fails if it has
drifted, so the diagram cannot quietly describe a database from two months ago.

Agent workflow state occupies six tables — `workflow_runs`,
`workflow_step_logs`, `tool_call_logs`, `agent_recommendations`,
`proposal_screenings`, `validation_results`. Rationale in
[ADR 0004](adr/0004-workflow-state-schema.md).

## 8. Repository structure

```
adr/                     Architecture Decision Records (LO4 evidence)
ai/                      Python Agentic AI service (FastAPI)
  agents/                the six agents
  graph/dispatcher.py    stage -> agent orchestration
  policies/              the ten validation rules
  tools/registry.py      allow-list and per-agent permissions
  llm/                   provider chain, cache, budget, fixtures
  tests/                 544 tests
  docs/                  architecture, contracts, evaluation, deploy
client/                  React application (Vite)
  src/features/          feature-sliced
  src/app/store.js       the single Redux slice (auth)
mobile/                  Flutter application
server/WebOfWork.Api/    ASP.NET Core Web API
  Controllers/           33 controllers
  Services/              48 services
  Models/                44 entities
Tests/WebOfWork.Api.Tests/   288 xUnit tests (268 [Fact] + 20 [Theory] over 97 [InlineData] rows)
docs/                    API reference, audits, parity checks
```

## 9. Prerequisites

- .NET SDK 8.0
- Node.js 18+ and npm
- Flutter 3.x (Dart SDK ≥ 3.0.3)
- Python 3.11+
- PostgreSQL 14+ — or Docker, for the container in `docker-compose.yml`
- Docker — **required for the backend test suite**, which starts real PostgreSQL
  and Redis containers through Testcontainers

## 10. Installation and startup

Start the components in this order. Each step assumes the previous one is
running.

### 10.1 Database

```bash
createdb webofwork
```

Or, with Docker:

```bash
docker compose up -d postgres
```

### 10.2 Backend

```bash
cd server/WebOfWork.Api
dotnet restore
dotnet ef database update      # applies all 30 migrations
dotnet run
```

Serves on `https://localhost:5001`. Swagger is at `/swagger`, health at `/health`.

### 10.3 Agentic AI service

```bash
cd ai
python -m venv .venv && source .venv/bin/activate    # Windows: .venv\Scripts\activate
pip install -r requirements.txt
cp .env.example .env          # then fill it in — see §11
python -m ai.main
```

Serves on `http://localhost:8001`. **It starts with no provider keys at all** and
runs on recorded fixtures, reporting that in `/health` rather than failing on a
missing setting — CI and a demonstration both run this way.

### 10.4 React

```bash
cd client
npm install
cp .env.example .env          # set VITE_BASE_URL to https://localhost:5001/api
npm run dev
```

### 10.5 Flutter

```bash
cd mobile
flutter pub get
flutter run
```

The dev API uses a self-signed certificate; accept the browser warning once.

## 11. Environment variables

### 11.1 Agentic AI service — `ai/.env`

`ai/.env` is gitignored. Required: `INTERNAL_API_KEY`, `DOTNET_BASE_URL`.

| Variable | Purpose |
|---|---|
| `INTERNAL_API_KEY` | Shared secret; **must match** the API's `AgenticAi:InternalApiKey` |
| `DOTNET_BASE_URL` | The API's base URL, including `/internal` routing as expected |
| `DOTNET_VERIFY_TLS` | `false` for the self-signed dev certificate. Refused when `APP_ENV=production` |
| `MAX_INTERNAL_READS_PER_WORKFLOW` | Read budget per run (default 20) |
| `LLM_PROVIDER_CHAIN` | Ordered, comma-separated. Unset or `fixture` means recorded responses |
| `LLM_<PROVIDER>_API_KEYS` | Provider keys, comma-separated for rotation |
| `LLM_<PROVIDER>_MODEL` | Per-provider model name — every hardcoded default was wrong on first contact with a real API |
| `LLM_ALLOW_FIXTURE_FALLBACK` | Whether recorded responses are the last resort |
| `APP_ENV` | `production` enables the refusal guards |

Provider order is measured, not alphabetical: Groq (317 ms) → Cerebras (560 ms) →
Cohere (1.7 s) → Gemini → NVIDIA → Mistral → OpenRouter. Gemini was first in an
earlier draft and spent 74 seconds returning 503 across ten keys before falling
through to a provider that answered in 300 ms.

### 11.2 React — `client/.env`

| Variable | Purpose |
|---|---|
| `VITE_BASE_URL` | API base; **must end in `/api`** — every client path is relative to it |
| `VITE_WS_URL` | WebSocket base, e.g. `wss://localhost:5001/ws` |
| `VITE_CLOUDINARY_CLOUD_NAME`, `VITE_CLOUDINARY_UPLOAD_PRESET` | Cloud name and *unsigned* preset only |
| `VITE_GOOGLE_CLIENT_ID` | Must match the server's `Google:ClientId` |
| `VITE_PAYPAL_CLIENT_ID` | Sandbox client id. There is deliberately no `VITE_` variable for the secret |

Anything in a `VITE_` variable ships to the browser. No secret may appear in one.

### 11.3 Backend

Configured through `appsettings.Development.json`, environment variables
(`Jwt__Secret`, `ConnectionStrings__Postgres`, `AgenticAi__InternalApiKey`), or
user secrets. **`appsettings.Development.json` must not carry production
credentials** — §14 below.

## 12. API documentation

Interactive OpenAPI documentation is served by the running API at
`/swagger`. Every response is snake_case, money is serialised as a string to
avoid floating-point loss, and enums are PascalCase (`"Fixed"`).

Authentication is `Authorization: Bearer <jwt>`. Role checks are enforced by
policy on the server.

Internal endpoints (`/internal/read/*`, `/internal/workflows/*`,
`/internal/actions/*`) are not part of the public contract: they are guarded by
`X-Internal-Api-Key` with constant-time comparison, rate-limited, and must not be
reachable from the public internet.

## 13. Test instructions

| Suite | Command | Count |
|---|---|---|
| Backend | `cd Tests/WebOfWork.Api.Tests && dotnet test` | **288** test methods (see note) |
| Agentic AI | `cd ai && python -m pytest -q` | **544** (84% coverage on agents, tools, llm, policies) |
| React | `cd client && npm test` | **67** |
| Flutter | `cd mobile && flutter test` | **290** |

The Python suite runs fully offline against recorded fixtures and needs no
network, no database and no API keys:

```bash
cd ai
LLM_PROVIDER_CHAIN=fixture LLM_ALLOW_FIXTURE_FALLBACK=false python -m pytest -q
```

The backend suite needs Docker. Without it, 92 tests fail with
`Docker is either not running or misconfigured` — that is the container runtime,
not the code.

CI: [`backend.yml`](.github/workflows/backend.yml) builds and runs the backend
tests on every push and pull request; [`ai.yml`](.github/workflows/ai.yml) runs
the Python suite twice — on fixtures, and with no provider configured at all —
plus the architecture guards.

### Performance measurements

§12 asks for evidence covering concurrent requests, response time,
success/failure rate, database response and Agentic AI latency. The first four
come from the backend suite:

```bash
cd Tests/WebOfWork.Api.Tests
dotnet test --filter "FullyQualifiedName~PerformanceTests" --logger "console;verbosity=detailed"
```

Each test prints a `PERF|…` line carrying the numbers, so figures can be lifted
from CI output rather than retyped. They are **in-process measurements against a
real PostgreSQL container** — no socket, no TLS, no proxy — so they are a floor,
not production latency, and the suite asserts *shape* (no errors, p95 within an
order of magnitude of serial) rather than millisecond budgets, which is what
keeps it from flaking on a shared runner.

Agentic AI latency:

```bash
# from the repository root — the package is `ai.*`
python -m ai.scripts.measure_latency
python -m ai.scripts.measure_latency --json          # machine-readable
python -m ai.scripts.measure_latency --provider groq # includes a real model call
```

By default this measures the system's own cost — internal reads, validation
rules, persistence — with the model call excluded, because that is the part this
project controls. A figure mixing our cost with a provider's cannot be compared
against anything.

### End-to-end

```bash
cd Tests/WebOfWork.Api.Tests
dotnet test --filter "FullyQualifiedName~CrossPlatformWorkflowTests"
```

This runs all five layers in one process graph: a real API process on a loopback
port, a real PostgreSQL container, and the real Python AI service — which calls
back into the API's internal endpoints exactly as it does in production. It then
drives the client journey and asserts, before anything else, that **no invitation
exists until the client approves**. It fails rather than skipping when the
environment is not ready, because a skipped end-to-end test is a green run that
proved nothing.

**One honest limitation.** The AI service is started on recorded fixtures, which
are keyed by a hash of the prompt, and the prompt built from the test's seeded
project has no recording — so the planning stage cannot complete and the run takes
the safe-failure path. The test asserts the invariant that holds in *both* cases
and logs which branch it took:

- **Pipeline completed** — recommendations persisted, approval creates exactly the
  approved invitations and no others.
- **Pipeline could not reach a model** — the run is persisted, does not report
  success, and nobody is contacted.

To exercise the approval leg, either record a fixture for that prompt
(`python -m ai.scripts.record_fixture planning --file answer.json`) or give the
service a provider key.

## 14. Deployment

**Status: not yet deployed.** There is no live API URL, no live React URL and no
APK. The intended arrangement, its rationale and its costs are recorded in
[ADR 0005](adr/0005-deployment-platform.md), which also states plainly that the
decision has not been executed.

Local startup is documented in §10 and is reproducible from a clean clone.

## 15. Third-party integrations

All are reached through the backend, which holds the credentials.

| Service | Purpose | Failure handling |
|---|---|---|
| **PayPal** (sandbox) | Contract funding, capture, release, refunds | Timeout, invalid response and declined-capture paths are handled and tested |
| **Cloudinary** | Portfolio and deliverable media | Upload size and type validated before transfer |
| **MailKit (SMTP)** | Verification, OTP, notification email | Send failures are logged, never allowed to fail the request |
| **Google OAuth** | Sign-in | Client id validated against configuration |
| **Android SMS gateway** | OTP delivery | Strategy-based, with a fallback and a no-op path |
| **Firebase Cloud Messaging** | Push notifications | Self-disables with a warning when unconfigured, so the app runs without it |

## 16. Security considerations

- **Passwords** are hashed; plaintext is never stored or logged.
- **Secrets** are environment variables. No `VITE_` variable contains a secret,
  and no key material is committed.
- **Authorisation** is enforced on the server for every endpoint. Hiding a button
  in React is a user-experience decision, not a control.
- **Internal AI endpoints** require a constant-time-compared API key, are
  rate-limited, and are bound to loopback in the intended deployment.
- **The AI service cannot write to the database**, cannot contact anyone without
  an approval, and cannot persist raw model input or output. Each of these is
  enforced by a test, because a convention is not a control.
- **The mobile app keeps its tokens in the platform keystore** (Android Keystore,
  iOS Keychain), not in `SharedPreferences`. Tokens written by an earlier build
  are migrated once and the plaintext copies deleted.
- **Production refuses** `DOTNET_VERIFY_TLS=false` and a fixture-only provider
  chain. Both refusals are tested.
- **Outstanding:** all provider keys and the Firebase service-account key used
  during development must be rotated before any shared deployment. See the
  warning block in `ai/.env`.

## 17. AI usage declaration

This project was developed with AI assistance at **Level 4 (Full AI)**, as
permitted by the assignment brief, with full disclosure per student in the
individual AI usage logs submitted with the consolidated report.

AI tooling was used for scaffolding and refactoring, for schema and migration
drafts, for test generation, and for documentation drafting. All submitted work
was reviewed, executed and verified by the group: every test result quoted in the
report was produced by running the suite, and every decision recorded in
[`adr/`](adr/README.md) was made and justified by the group.

No AI tools are used during the demonstration or viva, and the Agentic AI
subsystem implemented in this application is executed as part of the
demonstration.

## 18. Current status

Complete and verified: backend, database, React and Flutter applications, the
Agentic AI subsystem, the cross-platform end-to-end test, and 1,051 automated
tests across four suites.

**A bug the end-to-end test found.** `AiTalentMatchController.Start` compared
`Project.ClientId` — a foreign key to `client_profiles` — against the user id in
the token. The two are generated independently, so they are never equal and the
endpoint answered 401 for every client, always: "Find Best Talent" could not be
used by anyone. It went unnoticed because the only test of that ownership check
mocked the service to throw, so the comparison never ran. A test that mocks the
thing it claims to verify is not a test of it; the end-to-end run exercises the
real comparison and found it.

Outstanding: deployment and live URLs; the APK; the consolidated report, demo
video, individual contribution statements, AI usage logs and reflections.

Resolved: `.gitignore` used to contain an unanchored `Tests/` rule, which excluded
both `Tests/` and `ai/tests/` from version control — a rule matching at any depth
silently dropped 764 test files, and the CI workflows could only fail with an
explanation rather than reporting a green run that tested nothing. The rule has
been removed rather than anchored: writing `/Tests/` would still match the real
top-level `Tests/` directory and still exclude it. Both directories are tracked now,
along with `docs/` and `scripts/`.