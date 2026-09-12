# ShiftFlow

A call-center shift-management panel. Hourly specialists (**CallAgents**) declare when
they are available and apply for open shifts; an **Supervisor** manages projects, opens
shifts, and approves or rejects applications. One CallAgent is approved per shift, inside a
single transaction that also rejects the other applicants. A standalone Python script
(built in a later phase) ranks each shift's applicants and writes a score and a
human-readable reason back to the database.

This repository is the **backend**: an ASP.NET Core API, EF Core persistence against
SQL Server, an xUnit suite, the Python recommender, and a `docker compose` stack that runs
the database and API together. The React frontend and the `web` (nginx) service land in
their own phases (see `CLAUDE.md` §10).

## Architecture

```
Api  →  Application  →  Domain  ←  Infrastructure
```

| Layer | Holds | Depends on |
|---|---|---|
| **Domain** | Entities, enums, domain exceptions. No EF Core, no ASP.NET. | nothing |
| **Application** | Use-case services, DTOs, validators, the five business rules, the approval transaction. Talks to the database through `IAppDbContext`. | Domain |
| **Infrastructure** | `AppDbContext` + one EF configuration per entity, migrations, JWT issuance, BCrypt hashing, the system clock, the scenario seed. | Domain, Application |
| **Api** | Controllers (HTTP shape only — no business logic), the uniform error middleware, DI wiring, Swagger. | Application, Infrastructure |

Deliberate choices:

- **No repository classes.** Services compose LINQ directly against `IAppDbContext`, implemented by `AppDbContext`.
- **No `DateTime.UtcNow` in logic.** Time is injected via `IClock`, so month-boundary logic is deterministic in tests. Everything is UTC; every timestamp column ends in `Utc`.
- **Ownership is enforced at the resource level, not just by role.** A correct role with the wrong resource id gets `404`, so ids cannot be probed (see [Business Rules](#business-rules)).
- **One error shape.** Every failure — thrown domain/application exception or model-binding error — is returned as the same `ApiError` JSON object.

## Tech Stack

| Concern | Choice |
|---|---|
| API | ASP.NET Core (.NET 10) + EF Core |
| Database | SQL Server 2022, in Docker |
| Auth | JWT access token (HS256), **no refresh token** |
| Recommendation | standalone Python + `pymssql` *(later phase)* |
| Frontend | React + TypeScript + Vite + TanStack Query *(later phase)* |
| Tests | xUnit + SQLite in-memory |
| API docs | Swagger / Swashbuckle |
| Infra | Docker Compose with a healthcheck on the DB |

## Quick Start (Docker)

`docker-compose.yml` has the **database** and **api** services plus the one-shot
**recommender** (behind a profile); the `web` (nginx) service is added in the full-compose
phase (`CLAUDE.md` §10 step 14).

```bash
cp .env.example .env          # then edit MSSQL_SA_PASSWORD to a strong value
docker compose up --build     # SQL Server + API
```

`api` waits for the database's healthcheck, then — because it runs with
`ASPNETCORE_ENVIRONMENT=Development` — applies migrations and inserts the
[scenario seed](#seed-scenario-map) on startup. A cold start on an empty volume ends with a
ready database. Both secrets (`Jwt__Key`, and the SA password the connection string is built
from) come from `.env`.

Once it is up:

| URL | What you should see |
|---|---|
| `http://localhost:8080/swagger` | Swagger UI |
| `POST http://localhost:8080/api/auth/login` with `{"username":"supervisor","password":"Demo!Pass1"}` | `200` with an `accessToken` |
| `GET http://localhost:8080/api/shifts/1/recommendations` + `Authorization: Bearer <token>` | `ada` 76.10, `nate` 71.50, `kite` 70.90 |

To run the API from source instead, see [Manual Setup](#manual-setup) — that path is
unchanged and does not need the `api` container.

## Manual Setup

Running without Docker needs the .NET 10 SDK and a reachable SQL Server.

### Required configuration

`appsettings.json` ships only non-secret defaults (`Jwt` issuer/audience/lifetime) and
**empty** placeholders for the two secrets. `dotnet run` fails on startup if either is
missing, and the errors are not self-explanatory:

- an empty `ConnectionStrings:Default` surfaces later as a connection failure on the first request;
- an empty or short `Jwt:Key` fails fast in `IValidateOptions` — the key must be **at least 32 bytes**.

| Variable | Required | Notes |
|---|---|---|
| `ConnectionStrings__Default` | **yes** | e.g. `Server=localhost,1433;Database=ShiftFlow;User Id=sa;Password=Your_Strong_Passw0rd!;TrustServerCertificate=True` |
| `Jwt__Key` | **yes** | signing key, ≥ 32 bytes; generate with `openssl rand -base64 48` |
| `Jwt__Issuer` | no | defaults to `shiftflow` |
| `Jwt__Audience` | no | defaults to `shiftflow` |
| `Jwt__AccessTokenLifetimeMinutes` | no | defaults to `60` |
| `ATTENDANCE__STALENESSSECONDS` | no | defaults to `120`; maximum age of an active session's heartbeat |
| `ATTENDANCE__HEARTBEATSECONDS` | no | defaults to `60`; reserved for the future React client interval |
| `RATING__DOWNTIMECAPHOURS` | no | defaults to `8`; approved downtime hours allowed per agent and Jalali calendar month |
| `LEAVE__ANNUALDAYSDEFAULT` | no | defaults to `26`; annual allowance assigned when a CallAgent is created |
| `AutoMigrate` | no | `true` runs migrate + seed outside `Development`; leave unset in production |
| `ASPNETCORE_ENVIRONMENT` | no | `Development` (the default in `launchSettings.json`) enables Swagger and migrate + seed |

The `__` (double underscore) form is for environment variables; the `:` form below is for
`appsettings` / user-secrets.

### Recommended: user-secrets

Keep the secrets out of the repo and out of your shell history:

```bash
cd backend/src/ShiftFlow.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost,1433;Database=ShiftFlow;User Id=sa;Password=Your_Strong_Passw0rd!;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
dotnet run
```

The API listens on `http://localhost:5023` (and `https://localhost:7194`); Swagger UI is at
`/swagger`.

### Database schema

In `Development` (or with `AutoMigrate=true`) the API runs `Database.MigrateAsync()` and the
scenario seed itself, so there is nothing else to do. To build the schema by hand instead —
or to seed a database the API will not migrate — run, in order:

```bash
sqlcmd -I -S localhost -U sa -P "<pw>" -d ShiftFlow -i database/01-schema.sql
sqlcmd -I -S localhost -U sa -P "<pw>" -d ShiftFlow -i database/02-indexes.sql
sqlcmd -I -S localhost -U sa -P "<pw>" -d ShiftFlow -i database/03-seed.sql
```

> **Production** does **not** migrate or seed from the API process. It runs
> `dotnet ef database update` (or the reviewed `database/*.sql` pair) as a separate deploy
> step, with `AutoMigrate` unset, so a real database is never touched by app startup and
> the scenario seed never reaches it.

## Demo Accounts

> **Development data only.** These accounts come from the scenario seed, which runs in the
> `Development` environment or when `AutoMigrate` is set. They must never exist in a real
> deployment.

Every account's password is `Demo!Pass1`.

| Role | Username | In the scenario |
|---|---|---|
| Supervisor | `supervisor` | owns **Retail Support** and **Billing Support**, decides applications |
| Supervisor | `rival` | owns a separate project — use it to confirm the ownership boundary (`404`, not `403`) |
| CallAgent | `ada` | applies to the pool shift; already approved for another shift (drives apply rule 5) |
| CallAgent | `grace` | availability ends before the pool shift (apply rule 3 — fail) |
| CallAgent | `lin` | availability covers the pool shift exactly (apply rules 3 & 4) |
| CallAgent | `omar` | no availability on the pool day; already approved on the closed shift |
| CallAgent | `nate` | clean pool applicant, has a recommendation row |
| CallAgent | `kite` | clean pool applicant; no rating row, so scores the `3.0` default |
| CallAgent | `rosa` | assigned to no Northwind project (apply rule 2 — fail) |

See the [seed scenario map](#seed-scenario-map) for exactly which rows demonstrate which
rule.

## API Documentation

Swagger UI at `/swagger` in `Development`. All routes are under `/api`. Every endpoint
except `login` requires `Authorization: Bearer <token>`; the token's role must match.

| Method & path | Role | Purpose |
|---|---|---|
| `POST /api/auth/login` | anonymous | exchange username + password for a JWT |
| `POST /api/attendance/heartbeat` | CallAgent | refresh the current open attendance session |
| `POST /api/attendance/logout` | CallAgent | close the current open attendance session |
| `GET /api/attendance/active` | Supervisor or Manager | fresh active agents; Supervisor scoped, Manager global |
| `GET /api/projects` | Supervisor | list your projects |
| `POST /api/projects` | Supervisor | create a project |
| `GET /api/projects/{id}` | Supervisor | one project |
| `PUT /api/projects/{id}` | Supervisor | rename / toggle a project |
| `DELETE /api/projects/{id}` | Supervisor | delete a project (cascades to shifts) |
| `GET /api/projects/{id}/call-agents` | Supervisor | CallAgents assigned to a project |
| `POST /api/call-agents` | Supervisor | register a CallAgent (with an initial password) |
| `GET /api/call-agents` | Supervisor | list CallAgents |
| `GET /api/call-agents/{id}` | Supervisor | one CallAgent |
| `POST /api/call-agents/{id}/projects/{projectId}` | Supervisor | assign a CallAgent to a project |
| `DELETE /api/call-agents/{id}/projects/{projectId}` | Supervisor | unassign (rejects the CallAgent's pending applications on that project) |
| `GET /api/call-agents/{id}/projects` | Supervisor | a CallAgent's project assignments |
| `POST /api/availability` | CallAgent | add an availability window (overlapping / adjacent windows are merged on insert) |
| `GET /api/availability` | CallAgent | your windows |
| `GET /api/availability/{id}` | CallAgent | one window |
| `PUT /api/availability/{id}` | CallAgent | resize a window (may merge; the response body is the source of truth for the id) |
| `DELETE /api/availability/{id}` | CallAgent | delete a window (blocked if it covers an approved shift) |
| `POST /api/shifts` | Supervisor | open a shift on one of your projects |
| `GET /api/shifts` | Supervisor | your shifts, filterable by project / status / date |
| `GET /api/shifts/{id}` | Supervisor | one shift |
| `PUT /api/shifts/{id}` | Supervisor | correct a shift's window (only while Open and unapplied; optimistic-concurrency `409`) |
| `GET /api/shifts/open` | CallAgent | open shifts on your assigned projects |
| `GET /api/shifts/open/{id}` | CallAgent | one open shift you can apply to |
| `POST /api/shifts/{shiftId}/applications` | CallAgent | apply to a shift (the five rules below) |
| `GET /api/applications` | Supervisor or CallAgent | application history, scoped to the caller, filterable by shift / status |
| `POST /api/applications/{applicationId}/approval` | Supervisor | approve — closes the shift, rejects the siblings, all in one transaction |
| `POST /api/applications/{applicationId}/rejection` | Supervisor | reject this one application; the shift stays Open |
| `GET /api/agent-requests` | CallAgent, Supervisor, or Manager | scoped request history; optional `status` filter; pending first |
| `POST /api/agent-requests` | CallAgent | request whole-shift leave or bounded downtime |
| `POST /api/agent-requests/{id}/approval` | Supervisor | approve a request on one of your projects |
| `POST /api/agent-requests/{id}/rejection` | Supervisor | reject a request on one of your projects |
| `GET /api/shifts/{shiftId}/recommendations` | Supervisor | the applicant ranking the Python script wrote |

### Error shape

```json
{ "status": 409, "error": "BusinessRuleViolation", "message": "…", "details": { } }
```

`details` is present only for `400` validation errors.

| Status | `error` | When |
|---|---|---|
| 400 | `ValidationFailed` | malformed body / failed validator (`details` lists the fields) |
| 401 | `InvalidCredentials` | bad login, or a missing / invalid token |
| 403 | `Forbidden` | authenticated but the wrong role for the route |
| 404 | `NotFound` | unknown id **or** a resource owned by someone else |
| 409 | `BusinessRuleViolation` | an apply rule or an approval precondition failed |
| 409 | `ConcurrencyConflict` | a stale `RowVersion` on a shift edit / approval |
| 500 | `InternalServerError` | unexpected; no detail leaked |

## Business Rules

### Applying to a shift — `POST /api/shifts/{shiftId}/applications`

All five are enforced in `ApplicationService`, in this order (cheapest first after the
membership gate). The database constraints (`UQ_ShiftApplications_Shift_CallAgent`, the
filtered one-approved index) are a race backstop, not the primary check.

| # | Rule | Failure | Example (scenario seed) |
|---|---|---|---|
| 2 | The CallAgent is assigned to the shift's project | `404` — indistinguishable from an unknown id, so membership can't be probed | `rosa` (Overflow Desk only) → any Retail shift |
| 1 | The shift's status is `Open` | `409` "no longer open for applications" | any Retail CallAgent → the 2026-11-11 **Closed** shift |
| 4 | No existing application by this CallAgent for this shift (any status) | `409` "already applied to this shift" | `lin` applies to the pool shift twice |
| 3 | One availability window covers the **whole** shift | `409` "does not cover the whole of this shift" | `grace` (06:00–14:00) or `omar` (no window) → the 08:00–16:00 pool shift |
| 5 | No overlap with a shift the CallAgent is already **approved** for — half-open: `existing.Start < new.End AND existing.End > new.Start`, so back-to-back shifts (10–14, 14–18) are fine | `409` "overlaps another shift you are already approved for" | `ada` (approved 2026-11-12 09:00–17:00) → the overlapping 08:00–16:00 shift that day |

Rule 3 is a single-window containment test, not gap-stitching: because adjacent windows are
merged on write, a shift spanning what used to be two touching windows is covered by the one
merged window. Two windows with a real gap still fail.

### Approving an application — `POST /api/applications/{id}/approval`

One CallAgent per shift. Inside **one transaction**:

1. verify the supervisor owns the shift's project (else `404`);
2. verify the shift is still `Open` (else `409`);
3. verify the application is still `Pending` (else `409`);
4. **re-check apply rule 5** against current state — the CallAgent may have been approved for a clashing shift since applying;
5. application → `Approved`, recording `DecidedByUserId` / `DecidedAtUtc`;
6. shift → `Closed`;
7. every other `Pending` application on that shift → `Rejected`, with `DecisionNote = "Shift filled by another CallAgent."`

If the final write fails (e.g. the filtered unique index catches a race, or the shift's
`RowVersion` moved), the whole thing rolls back: the shift stays `Open` and no sibling is
rejected.

**Rejecting** (`.../rejection`) touches only that one application; the shift stays `Open` for
the rest. Cancelling an approval is out of scope, and a decided application cannot be
decided again.

## Scoring Formula

Computed by the Python recommender (later phase); any C# mirror must match exactly. Weights
and `MonthlyCap` come from configuration, never hard-coded.

```
RatingScore       = (previous-month rating ?? 3.0) / 5
WorkloadScore     = 1 - min(ApprovedHours / MonthlyCap, 1)          # MonthlyCap = 160
AvailabilityScore = shift duration / covering availability window duration

FinalScore = (0.30 * RatingScore + 0.30 * WorkloadScore + 0.40 * AvailabilityScore) * 100
```

- "Previous month" and `ApprovedHours` are relative to the month of **`Shift.StartUtc`**, not today.
- A CallAgent with no rating row scores `3.0`.
- Ranking is over the **applicants of one shift**. Tie-break: fewer approved hours first, then earlier `AppliedAtUtc`.
- `Reason` is traceable to the three weighted components.

### Worked example — `ada` on the pool shift (scenario seed)

The pool shift is 2026-11-10 **08:00–16:00** (8 h). `ada`:

| Component | Inputs | Score | Weighted (× 100) |
|---|---|---|---|
| Rating | 2026-10 rating = **4.6** → 4.6 / 5 | 0.92 | 0.30 × 0.92 × 100 = **27.6** |
| Workload | approved in 2026-11 = one 8 h shift → 1 − 8/160 | 0.95 | 0.30 × 0.95 × 100 = **28.5** |
| Availability | 8 h shift inside a 16 h window (06:00–22:00) → 8 / 16 | 0.50 | 0.40 × 0.50 × 100 = **20.0** |
| **Total** | | | **76.1** |

`Reason: Rating 4.6/5 -> 27.6 | Workload 8h -> 28.5 | Availability 8/16h -> 20.0 | Total 76.1`

Seeded ranking for that shift: `ada` 76.1 → `nate` 71.5 → `kite` 70.9 (`kite` has no rating
row, so 3.0 / 5).

## Database Design

Full ERD, every column, constraint, index, and delete rule: [docs/01-erd-and-schema.md](docs/01-erd-and-schema.md).

Shape highlights: `INT IDENTITY` keys; `DATETIME2(0)` UTC timestamps; enums as `NVARCHAR`
+ `CHECK`, mapped `HasConversion<string>()`; `DECIMAL` for scores; `RowVersion` on `Shifts`
for optimistic concurrency; a filtered `UNIQUE INDEX UX_ShiftApplications_OneApproved` as
the last-ditch guard against two approvals racing. The Supervisor → Project → Shift path
cascades on delete; every FK from the CallAgent side is `NO ACTION` (SQL Server rejects two
cascade paths into one table).

### Generated SQL scripts

- `database/01-schema.sql` and `database/02-indexes.sql` are **generated** from
  the EF Core migrations (`dotnet ef migrations script --idempotent
  --no-transactions`, then split at the tables/indexes boundary). Do not
  hand-edit them — regenerate after every migration. Only `database/03-seed.sql`
  is written by hand. Run 01 and 02 in order, as a pair, with
  `SET QUOTED_IDENTIFIER ON` (`sqlcmd -I`); 02 also stamps the
  `__EFMigrationsHistory` row.
- The `UNIQUE` constraints in the ERD (`UQ_Users_Username`,
  `UQ_Projects_Supervisor_Name`, `UQ_ShiftApplications_Shift_CallAgent`, …) are
  implemented as **named unique indexes**, not `ALTER TABLE … ADD CONSTRAINT …
  UNIQUE`. This is EF Core's default; it is functionally equivalent and keeps
  the names from the ERD.
- `Shifts.RowVersion` is a real `rowversion` column. `sys.types` reports it
  under the legacy synonym **`timestamp`** — same 8-byte type, nothing to fix.

### Index usage — shift listings

Checked against the running SQL Server container with actual execution plans, not
by reasoning. `IX_Shifts_Status_StartUtc` is seek-served with the sort eliminated
for the covered "open shifts ordered by start time" shape it was designed for
(`docs/01` §5). The CallAgent-facing open-shift listing is instead served by a seek
on `IX_Shifts_ProjectId_Status`: the join to the CallAgent's handful of assigned
projects makes `ProjectId` the selective leading column, and no scan of `Shifts`
happens. The multi-column supervisor list (`SELECT *`-style, filtered by
`Status`/date) falls back to a clustered scan because `Open` is not selective at
scale; adding `INCLUDE` columns to make it index-served was considered and
rejected — a schema change not justified for this data volume.

### Seed scenario map

`ShiftFlow.Infrastructure.Persistence.SeedData` (run on startup in `Development` /
`AutoMigrate`) and `database/03-seed.sql` (for the raw-script path) insert the **same
logical rows**. Neither runs the other. The data is one self-consistent set — every seeded
application is one the apply rules would actually have allowed — arranged so a reviewer can
see each rule pass and fail with nothing built by hand.

Row shorthand: **P** = pool shift (Retail, 2026-11-10 08:00–16:00, `Open`); **C** = closed
Retail shift (2026-11-11 08:00–16:00, `Closed`); **A** = ada's approved shift (Retail,
2026-11-12 09:00–17:00, `Closed`); **N12** = Retail 2026-11-12 08:00–16:00 (`Open`);
**B** = Billing 2026-11-10 12:00–20:00 (`Open`).

| Rule | See it **pass** | See it **fail** |
|---|---|---|
| **1 — shift is Open** | apply as `lin` → **P** | apply as `lin` → **C** (rule 1 is checked before availability, so `lin`'s window not covering **C** is irrelevant) |
| **2 — project membership** | apply as `lin` → **P** (assigned to Retail) | apply as `rosa` → **P** (Overflow Desk only) → `404` |
| **3 — availability covers the shift** | apply as `lin` (exact 08:00–16:00 window) → **P** | apply as `grace` (window 06:00–14:00) or `omar` (no window that day) → **P** |
| **4 — no duplicate** | first apply as `lin` → **P** | second apply as `lin` → **P** |
| **5 — no overlap with an approved shift** | apply as `ada` → **B** (different project, no time overlap with **A**) | apply as `ada` → **N12** (overlaps **A** 09:00–17:00) |
| **approval cascade** | already materialised on **C**: `omar` `Approved`, `kite` `Rejected` with note `"Shift filled by another CallAgent."`, shift `Closed` | — |
| **recommendation ranking** | `GET /api/shifts/{P}/recommendations` as `supervisor` → `ada` 76.1, `nate` 71.5, `kite` 70.9 | — |
| **ownership boundary (§7)** | any read as `supervisor` | the same id as `rival` → `404` |

Pool-shift applicants from the seed: `ada`, `nate`, `kite` (all `Pending`, each with a
recommendation row). Add `lin` through the API for a fourth. `Ratings` carry `2026-10`
(the month before the pool shift) for `ada`, `grace`, `lin`, `omar`, `nate`; `ada` also has
a `2026-09` row; `kite` has none, so it scores the `3.0` default.

## Testing

```bash
dotnet test backend/ShiftFlow.sln
```

- **SQLite in-memory**, not EF Core InMemory — the latter ignores the unique and `CHECK` constraints that several of these tests exist to prove. A fresh relational database is built per test from the real EF model.
- Time is a `TestClock`; the current user is a `StubCurrentUser`; BCrypt is faked for speed.
- Files are organised **by rule**, not by class (`ApplyForShift_AvailabilityTests`, `ApproveApplication_OverlapRecheckTests`, …).

Coverage includes the two tests the assignment names explicitly — availability covers the
shift → application succeeds; an overlapping approved shift exists → application is rejected —
plus every other apply rule (pass and fail), the approval transaction (cascade, rollback,
rule-5 re-check, already-decided, cross-tenant `404`), the availability merge algorithm, the
recommendation ranking and tie-break, JWT issuance/validation, resource-level authorization
across every feature, and the scenario seed's consistency and idempotency.

## Assumptions

Every ambiguous point in the brief and the decision taken. Kept as a running list.

- **CallAgent passwords have no strength policy.** `POST /api/call-agents` enforces only
  that a password is present and within a length limit; there is no minimum
  length, character-class or breach check. The brief does not define one, and it
  is a policy decision rather than a domain rule.
- **The supervisor sets the CallAgent's initial password.** A production system would
  email the new specialist an invitation link and let them choose their own
  password; here the supervisor supplies it directly in the create request to keep
  the flow to a single endpoint.
- **OpenAPI advisory (NU1903).** The scaffold's `Microsoft.AspNetCore.OpenApi`
  reference pulls a transitive `Microsoft.OpenApi 2.0.0` with a known advisory.
  Left as a visible build warning for now (`TreatWarningsAsErrors` on the src
  projects excludes `NU1903` via `WarningsNotAsErrors`). The fix is bundled with
  choosing the OpenAPI/Swagger stack in Prompt 4; remove the exclusion then.
- **Availability windows that touch are merged.** `08:00–12:00` and
  `12:00–16:00` are stored as one `08:00–16:00` window: a shift spanning the
  boundary instant needs unbroken coverage, so the merge boundary is closed
  (`<=`). This is deliberately the opposite of the shift-overlap rule
  (CLAUDE.md §5 rule 5), which is half-open so back-to-back shifts do not
  conflict.
- **`PUT /api/availability/{id}` may return a body whose `id` differs from the
  path `id`.** A window whose bounds change as part of a merge is replaced, not
  edited in place, so the merged window that now spans the requested interval
  can be a different row. This is expected, not a bug. Clients must treat the
  response body as the source of truth for the window's id and bounds and must
  not keep using the id they sent in the path.
- **Availability timestamps are truncated to whole seconds.** The storage
  column is `datetime2(0)`, so the request validator drops any sub-second part
  before the merge runs — the arithmetic uses the same precision the database
  keeps, and a re-post of a stored window is a no-op.
- **No supervisor-facing read of a CallAgent's availability.** The Prompt 6
  endpoints are CallAgent-role only (create, list, update, delete, all scoped to
  the caller). A supervisor's need to know whether a CallAgent covers a shift is
  met server-side at approval time (build order step 11), so exposing an
  availability read to supervisors now would be unused surface (CLAUDE.md §2).
- **Apply-rule violations: which HTTP status and why.** `POST
  /api/shifts/{id}/applications` enforces all five §5 rules in the Application
  layer, each with its own message; the DB constraints
  (`UQ_ShiftApplications_Shift_CallAgent`, `UX_ShiftApplications_OneApproved`) stay
  a race backstop, not the primary check.
  - **Rule 2 — project membership → 404.** A shift on a project the caller is
    not assigned to is indistinguishable from an unknown id, so membership
    cannot be probed (CLAUDE.md §7). Same 404 as the CallAgent shift read.
  - **Rules 1, 3, 4, 5 → 409** (`BusinessRuleViolation`). Once membership is
    established the shift's existence is not secret, so these report the real
    conflict: shift not Open ("no longer open for applications"), availability
    gap ("does not cover the whole of this shift"), duplicate ("already applied
    to this shift"), approved-shift overlap ("overlaps another shift you are
    already approved for"). This is a deliberate divergence from the
    CallAgent-facing shift *read*, which 404s a Closed shift — there, existence is
    still being probed; here it is not.
  - **Check order** after the membership gate is cheapest-first: status →
    duplicate (one `Any`) → availability coverage → approved-overlap scan. A
    repeat submit therefore gets "already applied" rather than a stale coverage
    error if the CallAgent's windows changed since.
- **No application withdrawal in this phase.** The design docs define no
  withdraw endpoint and no `Withdrawn` status (`docs/01` §3-8 locks `Status` to
  `Pending | Approved | Rejected`), and `UNIQUE(ShiftId, CallAgentId)` means a row
  kept as `Rejected` — or a new `Withdrawn` — would permanently bar re-applying:
  a behaviour change the brief never asked for. The only re-application-safe
  option is a hard delete of the `Pending` row, but that is a new
  CallAgent-initiated delete path (every FK from the CallAgent side is `NO ACTION` by
  design, `docs/01` §4) and belongs in its own phase with explicit sign-off, not
  a silent addition here. Approval is likewise one-directional ("Cancelling an
  approval is out of scope"), so forward-only application state is the design's
  intent.
- **Rule 3 leans on the Prompt 6 merge.** Coverage is a single-window
  containment test, not a gap-stitching one: because overlapping and adjacent
  windows are merged on write, a shift spanning what used to be two touching
  windows (e.g. `10:00–14:00` over `08:00–12:00` + `12:00–16:00`, now stored as
  one `08:00–16:00` window) is accepted. Two windows with a real gap between
  them still fail.
- **The scenario seed runs only under `Development` / `AutoMigrate`, and only
  from the API process.** It is guarded (checks for the `supervisor` account and
  returns if present), uses fixed absolute 2026 dates so it never depends on the
  wall clock, and is mirrored row-for-row by `database/03-seed.sql` for the
  raw-script setup path. It is development data — nine accounts sharing one
  password — and is documented as such; no password is written to the log.
  Production migrates and seeds (if at all) as a separate deploy step.
- **The scoring formula is expressed in three independent places and nothing
  enforces that they agree.** `SeedData.cs` (the stored `Score` / `Reason`), the
  README worked example, and `python/scoring.py` each encode it separately. The
  Python tests pin the seeded rows, so a change to the *formula* would break
  them — but a change to the *seed* would surface nowhere. Binding a `Scoring`
  section in the backend and deriving the seed's recommendation rows from it is
  the open item that would collapse this to one source of truth.
- **Approved leave preserves the original assignment pointer until cover.**
  Approval moves the shift to `Released` but leaves `AssignedCallAgentId`
  pointing at the agent on leave. V6 overwrites that pointer when a cover fills
  the shift, so V8 must derive committed and excused hours from the durable
  `AgentRequests` row rather than the shift's current assignment.

## At Scale

What a production system would add, and why it is out of scope here:

- **Multi-timezone.** Everything is UTC end to end; conversion to local time is a frontend concern. Per-CallAgent timezones and DST handling would touch availability, shift display, and the coverage rule.
- **More shift statuses.** `Confirmed`, `Completed`, `NoShow`, cancellation, and re-opening a closed shift are all real call-center needs the brief does not ask for. Adding them touches the approval transaction and the state machine.
- **Refresh tokens / logout / rotation.** The brief specifies a bare access token; a real system needs refresh tokens, revocation, and short access-token lifetimes.
- **Shift capacity > 1.** The design fixes one CallAgent per shift; multi-slot shifts would change the approval flow and the filtered unique index.
- **Rating ingestion.** `Ratings` is seed-only. A real system feeds it from a QA pipeline or customer surveys, with its own history and audit.
- **A `ScoringWeights` table + admin UI.** Weights live in configuration; making them runtime-editable is a feature the brief does not need.
- **Soft delete / audit history tables.** Deletes are physical and FK-controlled; a compliance context would want tombstones and full audit trails.
- **Application-level DB resilience.** Startup ordering relies on the Compose healthcheck (`condition: service_healthy`), not on connection retry in the app — enough for one local SQL Server container, but a managed database (failovers, transient throttling) would want `EnableRetryOnFailure` on the EF Core provider and retry-aware transactions.
- **Leave-quota approval serialization.** Two concurrent approvals on different
  shifts can both observe one remaining day. The filtered unique index protects
  one shift from duplicate approved leave, not one agent's annual allowance;
  production scale needs a serialized quota decision or an equivalent lock.

## AI Tools Used

Kept as a running log (docs/02 §11).

- **Claude Code** — repository scaffold: solution and project layout, root config files
  (`.gitignore`, `.editorconfig`, `.env.example`), `docker-compose.yml` (db service),
  and the CI workflow.
- **Claude Code** — Prompt 2 (persistence): the `IAppDbContext` and `IClock`
  abstractions, `AppDbContext` with a per-entity EF Core configuration each, and
  tests asserting the mapping matches `docs/01`.
- **Claude Code** — Prompt 3 (migration): EF Core migration tooling, the
  `InitialCreate` migration verified against a running SQL Server container, the
  generated `database/01-schema.sql` / `02-indexes.sql` scripts with a seed
  placeholder, and the docs describing that generated-script contract.
- **Claude Code** — Prompt 4 (auth): identity abstractions and their
  Infrastructure implementations, the `/api/auth/login` endpoint, the uniform
  `ApiError` shape and API composition root, the move to Swashbuckle for OpenAPI
  (clearing NU1903), auth tests, and fixes making `ICurrentUser` and JWT
  startup fail loudly on bad input.
- **Claude Code** — Prompt 5 (projects & experts): employer-scoped project CRUD,
  expert registration and expert–project assignment, fail-closed employer/expert
  id accessors, and cross-employer authorization tests.
- **Claude Code** — Prompt 6 (availability): the merge-on-insert/update algorithm,
  the `AvailabilityService` use cases and Expert-role endpoints, the approved-shift
  coverage guard on update and delete, and their tests.
- **Claude Code** — Prompt 7 (shifts): employer shift management and the
  expert-facing open-shift read, the `RowVersion` 409 path, and the shift
  visibility / edit-guardrail tests.
- **Claude Code** — dev seed (`chore`): a guarded, idempotent development seed
  (one employer, one expert, a project with the expert assigned, one open shift
  and a covering availability window) run on startup under `Development` /
  `AutoMigrate`. Superseded by the Prompt 10 scenario seed.
- **Claude Code** — Prompt 8 (applications): `POST /api/shifts/{id}/applications`
  with all five §5 apply rules enforced in `ApplicationService` (rule 2 → 404,
  rules 1/3/4/5 → 409), the dual-role `GET /api/applications` history, the
  no-withdrawal decision, and one passing + one failing test per rule including
  the adjacent-shift and merged-boundary cases.
- **Claude Code** — Prompt 9 (approval): `ApprovalService` — approve inside one
  transaction (own the project, shift still Open, application still Pending,
  re-check rule 5, close the shift, cascade-reject the siblings) and single-row
  reject; the explicit-transaction seam on `IAppDbContext`; the read-only
  `GET /api/shifts/{id}/recommendations` ranking with the §5 tie-break; and the
  approval / rejection / ranking tests.
- **Claude Code** — Prompt 10 (seed & docs): the `SeedData` scenario seed and its
  hand-written `database/03-seed.sql` mirror (replacing the `chore` dev seed),
  the gated migrate + seed with no password logged, tests for the
  previously-uncovered decision branches (already-decided, cross-project
  overlap, unknown reject id), and this README.
- **Claude Code** — Prompt 11 (scoring): `python/scoring.py` — standard-library
  only, mirroring the §5 formula (including the sum-of-one-decimal-parts rule
  that makes the seeded `nate` row `71.50`, not `71.46`) with all arithmetic in
  `Decimal`/`ROUND_HALF_UP`, and the four-level tie-break as a sort key;
  `python/config.py` resolving the `SCORING__*` and `MSSQL_*` variables into a
  frozen dataclass that falls back to the §5 defaults; the reserved scoring
  lines activated in `.env.example`; `python/README.md`; a load-bearing test
  that reproduces the seeded `ada` / `nate` / `kite` rows byte-for-byte plus
  rating-default, monthly-cap, availability and rounding-convention coverage;
  and a README note that the §5 formula still has three unenforced copies.
- **Claude Code** — Prompt 12 (recommender): `python/db.py` — a `pymssql`
  connection from the resolved config, the per-shift reads (pending applicants,
  covering window, previous-month rating, approved-shift spans), and one
  idempotent `MERGE` on `(ShiftId, ExpertId)` that rewrites only changed rows
  and drops rows for pairs not scored in the run; `python/recommendation.py`,
  the entry point that ranks each Open shift's own applicants and skips anyone
  who would now fail an apply rule; `docker/recommender.Dockerfile` and the
  profile-gated one-shot `recommender` service in `docker-compose.yml`, gated on
  the db healthcheck. Verified against the seed: `76.10` / `71.50` / `70.90`
  with identical `Reason` strings, second run a no-op.
- **Claude Code** — Prompt 13 (docker api): `docker/api.Dockerfile` (multi-stage
  SDK build → aspnet:10.0 runtime image), the `api` service in
  `docker-compose.yml` depending on the db healthcheck, both secrets sourced
  from `.env` with the connection string assembled around `MSSQL_HOST=db`, and
  the Quick Start section. Local `dotnet run` / user-secrets unchanged.
