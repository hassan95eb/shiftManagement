# ShiftFlow

> Skeleton README. Section order follows docs/02-repository-structure.md §11.
> Each `TODO` is filled in during the build phase that produces the relevant code.

## Overview

TODO — one paragraph describing the call center shift management panel, plus a screenshot.

## Architecture

TODO — layer diagram (`Api → Application → Domain ← Infrastructure`) and the reasoning
behind it. See CLAUDE.md §4.

## Tech Stack

TODO — table of choices (ASP.NET Core + EF Core, SQL Server, React + TypeScript + Vite +
TanStack Query, JWT, Python + pymssql, Docker Compose, xUnit + SQLite in-memory, Swagger).

## Quick Start (Docker)

TODO — three commands, no more.

## Manual Setup

TODO — how to run without Docker.

## Demo Accounts

> **Development only.** These accounts are created by the startup development
> seed, which runs in the `Development` environment or when `AutoMigrate` is set.
> They must never exist in a real deployment. The full phase-10 seed replaces
> this.

| Role | Username | Password |
|---|---|---|
| Employer | `demo-employer` | `Demo!Pass1` |
| Expert | `demo-expert` | `Demo!Pass1` |

The same seed also creates one project (`Demo Project`) owned by the employer
with the expert assigned to it, one open shift starting tomorrow at 09:00 UTC,
and an availability window on the expert that fully covers that shift — enough to
walk login and the apply flow through Swagger. Running the API again does not
duplicate any of it. The two usernames are logged to the console on startup.

## API Documentation

TODO — link to Swagger UI and a table of endpoints.

## Business Rules

TODO — the five apply-to-shift rules with examples (CLAUDE.md §5).

## Scoring Formula

TODO — formula, configurable weights, and a worked example (CLAUDE.md §5).

## Database Design

TODO — link to [docs/01-erd-and-schema.md](docs/01-erd-and-schema.md).

### Generated SQL scripts

- `database/01-schema.sql` and `database/02-indexes.sql` are **generated** from
  the EF Core migrations (`dotnet ef migrations script --idempotent
  --no-transactions`, then split at the tables/indexes boundary). Do not
  hand-edit them — regenerate after every migration. Only `database/03-seed.sql`
  is written by hand. Run 01 and 02 in order, as a pair, with
  `SET QUOTED_IDENTIFIER ON` (`sqlcmd -I`); 02 also stamps the
  `__EFMigrationsHistory` row.
- The `UNIQUE` constraints in the ERD (`UQ_Users_Username`,
  `UQ_Projects_Employer_Name`, `UQ_ShiftApplications_Shift_Expert`, …) are
  implemented as **named unique indexes**, not `ALTER TABLE … ADD CONSTRAINT …
  UNIQUE`. This is EF Core's default; it is functionally equivalent and keeps
  the names from the ERD.
- `Shifts.RowVersion` is a real `rowversion` column. `sys.types` reports it
  under the legacy synonym **`timestamp`** — same 8-byte type, nothing to fix.

### Index usage — shift listings

Checked against the running SQL Server container with actual execution plans, not
by reasoning. `IX_Shifts_Status_StartUtc` is seek-served with the sort eliminated
for the covered "open shifts ordered by start time" shape it was designed for
(`docs/01` §5). The expert-facing open-shift listing is instead served by a seek
on `IX_Shifts_ProjectId_Status`: the join to the expert's handful of assigned
projects makes `ProjectId` the selective leading column, and no scan of `Shifts`
happens. The multi-column employer list (`SELECT *`-style, filtered by
`Status`/date) falls back to a clustered scan because `Open` is not selective at
scale; adding `INCLUDE` columns to make it index-served was considered and
rejected — a schema change not justified for this data volume.

## Testing

TODO — how to run the tests and what is covered (CLAUDE.md §8).

## Assumptions

TODO — every ambiguous point in the brief and the decision taken. Keep this running.

- **Expert passwords have no strength policy.** `POST /api/experts` enforces only
  that a password is present and within a length limit; there is no minimum
  length, character-class or breach check. The brief does not define one, and it
  is a policy decision rather than a domain rule.
- **The employer sets the expert's initial password.** A production system would
  email the new specialist an invitation link and let them choose their own
  password; here the employer supplies it directly in the create request to keep
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
- **No employer-facing read of an expert's availability.** The Prompt 6
  endpoints are Expert-role only (create, list, update, delete, all scoped to
  the caller). An employer's need to know whether an expert covers a shift is
  met server-side at approval time (build order step 11), so exposing an
  availability read to employers now would be unused surface (CLAUDE.md §2).

## At Scale

TODO — what a production system would add (multi-timezone, more shift statuses,
refresh tokens, …) and why it was left out here.

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
- **Claude Code** — dev seed (`chore`): a guarded, idempotent development seed
  (one employer, one expert, a project with the expert assigned, one open shift
  and a covering availability window) run on startup under `Development` /
  `AutoMigrate`, plus the demo-account entry above.
