
# The reference parity — working method

How the `webofwork` / the reference parity work was actually done, so it can be repeated.
This is a *process* doc, not a change log. Progress lives in `progress.md`.

## Ground rules

1. **`ref/` is read-only reference.** `ref/the reference project` is the Python/Django
   original. It is gitignored (`.gitignore:38`), so nothing in it can leak into
   the build. Consult it for the authoritative answer; never edit or import it.
2. **The frontend is a spec, not a source of truth.** `.jsx` reads tell you
   *which* fields must exist. `ref/` tells you *what the values mean*. Use both;
   when they disagree, `ref/` wins on semantics.
3. **Additive changes only.** Add the reference aliases/fields alongside existing
   ones. Never repurpose a field the current frontend depends on.

## The loop

For each endpoint:

1. **Find the consumer** — grep the frontend for the field names.
2. **Find the source of truth** — read the matching `Meta.fields` / view in
   `ref/the reference project`. Get the *exact* list, and the method behind any
   `SerializerMethodField`.
3. **Compare against the current DTO** — read the C# DTO and the mapper.
4. **Change DTO + mapper + route** as needed.
5. **Build.** `Set-Location server; dotnet build WebOfWork.Api/WebOfWork.Api.csproj -v q --nologo`
6. **Start the server, probe with a real token, read the actual JSON.**
7. **Clean up** — delete fixtures, restore roles, kill processes, remove logs.
8. **Append the finding to `progress.md`.**

Steps 5–7 are not optional. A clean build proves nothing about the payload.

## Running the server

The app runs on **https://localhost:5001** (self-signed) — use `curl.exe -k`.

```powershell
Set-Location 'c:\catacolite\webofwork\server\WebOfWork.Api'
$p = Start-Process dotnet -ArgumentList 'run' -PassThru `
     -RedirectStandardOutput 'c:\catacolite\webofwork\b.out.log' `
     -RedirectStandardError   'c:\catacolite\webofwork\b.err.log'
Start-Sleep -Seconds 50   # cold start; 26s is NOT enough on first run
```

Teardown — `WebOfWork.Api` must die before the next build or MSB3021 locks the exe:

```powershell
Get-Process -Name 'WebOfWork.Api' -EA SilentlyContinue | Stop-Process -Force
Get-Process dotnet -EA SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 3
```

## Getting a token

There is **no register endpoint** — auth is OTP-based. Use the seeded account:

```powershell
$r = curl.exe -k -sS -X POST 'https://localhost:5001/api/auth/login/' `
     -H 'Content-Type: application/json' `
     -d '{"email":"t@t.com","password":"Passw0rd!"}'
$tok = ($r | ConvertFrom-Json).access
```

For an admin token, flip the role and log in again:

```powershell
$env:PGPASSWORD='Webofworkpassword1#'
& psql -h 49.13.208.232 -U webofwork_user -d webofwork -c "update users set role=3 where email='t@t.com';"
```

**Always restore `role=1` when finished.**


### Traps learned building these

- The client's axios paths are relative to `VITE_BASE_URL`, which ends in `/api`.
  Probing without that prefix 404s every route.
- A 404 with a body is the *application* answering (e.g. "User not found"); only
  an empty 404 is an unroutable path. Both problem+json and text/plain count.
- Entity keys are inconsistent: `User.Id` and `FreelancerTodo.Id` are Guid,
  `Category.Id` / `Project.Id` / `Contract.Id` are int. A route only matches the
  right shape, so probe both.
- An empty `results` array exposes only the envelope, so row-level keys are
  unverifiable. That is a data gap, not a shape failure -- it should say "no rows
  to check", not "missing keys".
- Layer 2 is a static read. It cannot see a key that is only accessed through a
  computed path, and it will not catch a wrong *value* (see the enum layer).

### What layer 2 has already found

- `GET /api/contracts/` returned a bare array while ClientContracts.jsx reads
  `data?.results` / `.count` / `.next` / `.previous` -- the list rendered empty and
  pagination was inert. Now wrapped in the DRF envelope.
- `GET /api/profile/freelancers-list/` had the same defect; FindTalent.jsx reads

## Fixtures

`psql -h 49.13.208.232 -U webofwork_user -d webofwork` with that password.
Expect NOT NULL failures — `client_profiles` may need an explicit `id`,
`reviews` needs `updated_at`, and `ix_contracts_project_id` is unique so only one
contract per project. Deleting a `client_profiles` row a contract points at
fails on the FK; re-point the contract first.

Always leave it clean:

```powershell
& psql ... -c "delete from projects where title like 'PROBE %';"
& psql ... -c "update users set role=1 where email='t@t.com';"
```

## Probing

Interpret statuses carefully:

- `401` / `403` — route resolved, role gate hit. **Not a routing bug.**
- `404` **with a body** — the application answered ("User not found"). Routed fine.
- `404` **empty** — genuinely unroutable.
- `405` — route resolved, wrong verb.
- `500` + `"matched multiple endpoints"` — duplicate route attributes.

## Traps I actually hit

- **Duplicate route attributes → 500.** Never stack
  `[HttpGet("x"), HttpGet("x/")]` on one action; the trailing-slash form is served
  automatically.
- **A route constraint that cannot match is invisible until something calls it.**
  `{id:int}` on a Guid PK made `mark-complete` dead. Sweep every `{id:...}`.
- **`Enum.TryParse` does not understand snake_case.** Use
  `Helpers.EnumQueryParser.TryParse<T>`. Inlining the old call next to a
  `using System;` resolves to `System.EnumQueryParser` and fails to compile.
- **`ToLowerInvariant()` destroys wire values.** `Helpers.SnakeCase.Of()` keeps
  `proposal_accepted`; `ToLowerInvariant` yields `proposalaccepted`.
- **Enums need a `StringEnumConverter`; projections need `SnakeCase.Of`.**
- **An enum column can hold a 0 sentinel that is not a valid member.**
  `UserRole` starts at 1 and `users.current_role` is 0 on every seeded row, so
  `SnakeCase.Of(0)` produced an empty label. Fall back to a reliable field.
- **A DTO is not the only mapper.** `AdminUserService` had three projections;
  fixing the one the list endpoint used took two attempts.
- **Read `ref/` for *value* semantics, not just field lists.** `freelancer_earnings`
  is a stored share (0.00 when absent), not `total - fee`; `contract_info` is per
  (project, freelancer), not per project.
- **A helper may be handed its own output.** `ToWireFormat(string)` was passed
  the already-converted `"delivered"`, threw, and 400'd the whole notifications
  list. Accept the wire values as identity.
- **Some DTOs are UTF-16** (`ReviewResponse.cs` BOM `FF FE`).
- **Adding a navigation property needs a matching relationship config**, or EF
  invents a shadow FK (`f.skill_id1`) and the query 500s at runtime.
- **The editor can fail on exact-text match** where `Get-Content` works; rewrite
  the file or edit by line index instead.
- **Beware PowerShell index arithmetic on `Select-String` results** — they are
  objects, not ints. Coerce with `[int](...).LineNumber`.

## Shared helpers

| Helper | Use |
|---|---|
| `SnakeCase.Of(enum)` | enum → `in_progress` |
| `EnumQueryParser.TryParse<T>` | snake_case/PascalCase query param → enum |
| `PaginationLinks.Apply(...)` | DRF `next`/`previous` |
| `NotificationWireFormat.ToWireFormat(...)` | notification `type` strings |

  `data?.results` and `data?.count`.


## Verification layers

Layered by what each layer can see. No layer covers the next.

| Layer | Script | Catches | Blind to |
|---|---|---|---|
| 1. Route resolution | `route_smoke.py` | unroutable paths | anything about the body |
| 2. Key coverage | `key_coverage.py` | keys the client reads that are absent | values, casing, types |
| 3. Enum/status literals | `enum_sweep.py` | `=== "in_progress"` that never matches | keys, routes |
| 4. Run the app | `npm run dev` | null derefs, wrong nested shape, `.map()` on undefined | nothing |

`parity_check.py` is retained for the reference field-list comparison but is a
**weaker** signal: a response can differ from the reference and still render, or match
it and still crash. Layers 2 and 3 both found bugs that broke entire screens and
appeared in no field list.

```powershell
# layer 1
python scripts/route_smoke.py --base https://localhost:5001 --token <jwt>
python scripts/route_smoke.py --base ... --account client:<tok> --account admin:<tok>

# layer 2
python scripts/key_coverage.py --base https://localhost:5001 --token <jwt>
python scripts/key_coverage.py --extract-only     # no server needed

# layer 3
python scripts/enum_sweep.py --base https://localhost:5001 --token <jwt>
python scripts/enum_sweep.py --extract-only      # no server needed
```

All exit non-zero on failure, so they can gate a build.

### Traps in these three scripts

- The client's axios paths are relative to `VITE_BASE_URL`, which ends in `/api`.
  Probing without that prefix 404s every route.
- A 404 with a body is the *application* answering; only an empty 404 is an
  unroutable path. Both problem+json and text/plain count.
- Entity keys are inconsistent: `User.Id` and `FreelancerTodo.Id` are Guid,
  `Category.Id` / `Project.Id` / `Contract.Id` are int. Probe both shapes.
- An empty `results` array exposes only the envelope, so row-level keys are
  unverifiable. That is a data gap, not a shape failure — say "no rows to check".
- `status` is **not one vocabulary.** A Contract, Project and Payment each have
  their own domain, and the client compares each against a different set.
  Pooling them makes a correct API look broken; scope the probe per field.
- Most `status === "x"` comparisons in the client are local UI state (form
  validity, async lifecycle), not API data. Filter those out or every field
  reports junk.
- A value can only be checked where rows exist. Probe with a token for the role
  that actually owns the records, or the result is a hollow pass.

### What layers 2 and 3 have found

- `GET /api/contracts/` returned a bare array while ClientContracts.jsx reads
  `data?.results` / `.count` / `.next` / `.previous` — the list rendered empty
  and pagination was inert. Now wrapped in the DRF envelope.
- `GET /api/profile/freelancers-list/` had the same defect; FindTalent.jsx reads
  `data?.results` and `data?.count`.
- `NotificationWireFormat.ToWireFormat(string)` was handed its own output
  (`"delivered"`), threw, and returned **400 for the entire notifications list** —
  one bad row took down the whole screen.

## Frontend <-> backend wiring

The client is wired to the API through `client/.env` (gitignored; `client/.env.example`
is the committed template). Every axios path in `client/src` is **relative** to
`VITE_BASE_URL`, so the `/api` suffix is part of the value, not the individual calls.

| Var | Dev value | Notes |
|---|---|---|
| `VITE_BASE_URL` | `https://localhost:5001/api` | must end in `/api` |
| `VITE_WS_URL` | `wss://localhost:5001/ws` | bridge root; chat is `/ws/chat/{roomId}/` |
| `VITE_CLOUDINARY_CLOUD_NAME` | `dse2zvdkb` | public value |
| `VITE_CLOUDINARY_UPLOAD_PRESET` | `webofwork_uploads` | public value |
| `VITE_GOOGLE_CLIENT_ID` | OAuth web client | must match `Google:ClientId` on the server |
| `VITE_PAYPAL_CLIENT_ID` | sandbox client | no secret is ever a `VITE_` var |

### WebSocket paths

The server mounts its bridge at `/ws` and serves chat at `/ws/chat/{room}/` and
notifications at `/ws/notifications/`. The client previously asked for
`${VITE_WS_URL}/chat/{id}/` and `/notifications/` with a bare host, which 404'd —
chat and live notifications were dead. Setting `VITE_WS_URL` to include the `/ws`
prefix is what fixes it; do not move the prefix into the hook files.

`wss://` (not `ws://`) is required because the dev server is HTTPS.

### CORS and cookies

`Cors:AllowedOrigins` is `http://localhost:5173` and Vite defaults to 5173, so the
credentialed cross-origin request works and the `refresh_token` cookie is issued
(`SameSite=Lax` is correct for same-site cross-port). If the Vite port changes,
add the new origin to the server config — `withCredentials: true` means a missing
origin silently breaks refresh.

### Dev certificate

`dotnet dev-certs https --check` reports a valid, **trusted** certificate
(`CN=localhost`, in `CurrentUser\Root`), so the browser does not warn. If it ever
does, run `dotnet dev-certs https --trust`.

### Verified end to end

With both running (`dotnet run` on the API, `npm run dev` on the client):
CORS preflight returns `Allow-Credentials: true` with the client origin; login
issues both an access token and a refresh cookie; authenticated GETs across 8
client endpoints return 200; cookie-only `POST /auth/refresh/` returns 200 (the
axios interceptor path); and both `/ws/chat/{id}/` and `/ws/notifications/`
complete their handshake (401 with a bad token, not 404).

`route_smoke.py` reports **0 unresolved of 110** routes against the live server.

## Branding

The product is **WebOfWork**. `ref/the reference project` is the upstream Django project this
API was ported to, and the two names mean different things in different places.

**User-facing — rebranded to WebOfWork** (10 sites, all in `client/`):

| File | Was |
|---|---|
| `index.html` | `<title>` held the legacy product name |
| `package.json` | `"name"` held the legacy product name |
| `AdminLogin.jsx` | logo text + legacy-domain placeholder email |
| `AdminOverview.jsx`, `FreelancerDashboard.jsx` | welcome copy |
| `OtpModal.jsx` | `"Welcome To the reference"` toast |
| `WhyUs.jsx`, `TermsAndConditions.jsx` | body copy |
| `GlobalErrorBoundary.jsx` | legacy `_Shield_v1.0` string |

Verified in the built bundle: `WebOfWork` present, the legacy name absent.

**Code comments — intentionally NOT rebranded** (59 sites in `server/`). These are
provenance, not branding. They record *why* a shape exists, e.g.

    /// the reference reads escrow_details.freelancer_share here and returns 0.00 when
    /// the contract has no escrow row -- a stored share, not TotalAmount minus
    /// the platform fee.

Rewriting that to "WebOfWork reads..." would be actively wrong: it would claim we
designed it, and erase the reference that explains the non-obvious choice. If the
port is ever fully internalised, rewrite them to state the *rule*, not the source.
There are **zero** the reference references in backend string literals — email
`FromName` is already `WebOfWork`.

The favicon path in `index.html` was also wrong: `/client/public/favicon.ico`
(Vite serves `public/` at the root, so it resolved to a nonexistent
`client/public/public/`). Fixed to `/favicon.ico`.
