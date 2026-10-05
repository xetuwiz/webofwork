# 0005 — Deployment: one VPS for the API, database and AI service; Vercel for the React build

**Status:** Accepted — **not yet executed.** See *Status* below.
**Affects:** `client/vercel.json`, `server/WebOfWork.Api/appsettings.Production.json`, `ai/Dockerfile`, `docker-compose.yml`

## Context

Part 14 requires four things to be deployed and reachable: the ASP.NET Core API
with a working health URL and Swagger URL, PostgreSQL deployed securely with
migrations and restricted credentials, the React application live and configured
against the deployed API, and a Flutter APK. The Agentic AI service may be
deployed or run locally.

The brief also constrains the budget explicitly: *"You must be able to complete
this assignment using institution-provided or no-cost services. Paid
subscriptions are not required."*

That constraint decides the architecture before any preference is expressed. A
platform that is free until it is asked to hold a stateful database — and then
charges — is not free for this project, because this project has four
stateful components.

There is a second constraint that is easy to overlook: **the React client and the
Flutter app must call the same ASP.NET Core API**, and the Python service must be
called by ASP.NET Core rather than by either client. Any deployment that makes
the AI service publicly reachable violates the mandatory backend rule, and the
one place the AI service must *not* be is the public internet.

## Options considered

**A. Everything serverless** — the API on a container or function platform, the
database on a serverless Postgres provider, the React build on a CDN, the AI
service as a function.

The natural modern default, and the wrong shape here for three reasons. The
database's provider free tier expires; the API needs a long-lived WebSocket for
chat and notifications, which most function platforms handle poorly or not at
all; and the AI service holds an in-process LLM cache and a per-workflow budget,
which does not survive a platform that recycles processes between requests.

**B. Three managed services** — a cloud database, a container host for the API,
Vercel for the frontend.

Rejected on cost. Managed Postgres and a container host are both paid after the
trial, and the brief requires a no-cost path.

**C. A single virtual machine running everything, with the frontend on a free
static host.** Chosen.

The database, the API and the AI service sit on one host, behind one reverse
proxy. The React build is a static bundle served from a CDN that is free
permanently rather than free until it is popular.

## Decision

**One VPS hosts all three stateful or long-running components.**

- **PostgreSQL** — bound to `localhost`, not `0.0.0.0`. `appsettings.Production.json`
  points at the host address, but the database itself is not reachable from
  outside; only the API connects to it.
- **ASP.NET Core API** — published as a self-contained build, run under a process
  supervisor, behind a reverse proxy that terminates TLS and serves `/swagger`
  and `/health`.
- **Agentic AI service** — built from `ai/Dockerfile`, listening on the loopback
  interface or an internal network address only. It authenticates every call with
  the shared `X-Internal-Api-Key`, and it is never exposed publicly. The AI
  service itself refuses to run in production with `DOTNET_VERIFY_TLS=false` or
  a fixture provider chain, and both refusals are tested.

**React is deployed separately**, because a static bundle has no reason to occupy
the same host as a database. `client/vercel.json` holds a single rewrite sending
every path to `index.html`, which is what makes client-side routing survive a
hard refresh on a deep link — the failure mode otherwise being a 404 on
`/my-projects/42/ai-shortlist`.

**Secrets are environment variables**, supplied at deploy time and never committed.
The service is built and run from no-cost or institution-provided resources only.

## Status

**This decision has not yet been executed.** No deployment exists: there is no
live API URL, no live React URL, and no APK. There is also no Dockerfile or
deployment script for the API — the AI service has one, the .NET host does not
yet — so this record describes the intended arrangement and not a verified
working one.

That is stated here rather than left implicit because an ADR that claims a
deployment exists when it does not is worse than no ADR: it is a document the
viva will treat as evidence, and it would be evidence of nothing.

## Consequences

**What this makes easy.** One host to secure and one to monitor. The API reaches
the database and the AI service over the loopback interface, so neither
dependency crosses the public network. Costs are one predictable monthly figure.
Restarting the AI service cannot affect the API or the database.

**What this makes hard.** Everything is on one machine, so that machine is a
single point of failure, and the database has no managed failover. There is no
elastic scaling: the API, the database and the AI service compete for the same
CPU, and a long AI workflow can make the API slow. Backups are a manual
responsibility rather than a provider feature. The AI service is deliberately
single-worker — two workers would mean two LLM caches and two budget counters, so
a request could be served from a different cache than the one that answered its
neighbour — which caps throughput at one container's worth by design.

**What it cost.** Managed high availability, and the option of running the
database somewhere other than next to the API. For an assessed project whose
requirement is a working, reproducible, no-cost deployment, that is the correct
trade. It would not be the correct trade for a system with real users, and the
record should say so.

**Revisit if.** The AI service's workload grows enough that a slow workflow can
starve the API. The first move would be to move PostgreSQL to a second host and
keep the rest — which keeps the internal-only rule intact while removing the
contention that matters.