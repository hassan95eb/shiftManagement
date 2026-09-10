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
- **Apply-rule violations: which HTTP status and why.** `POST
  /api/shifts/{id}/applications` enforces all five §5 rules in the Application
  layer, each with its own message; the DB constraints
  (`UQ_ShiftApplications_Shift_Expert`, `UX_ShiftApplications_OneApproved`) stay
  a race backstop, not the primary check.
  - **Rule 2 — project membership → 404.** A shift on a project the caller is
    not assigned to is indistinguishable from an unknown id, so membership
    cannot be probed (CLAUDE.md §7). Same 404 as the expert shift read.
  - **Rules 1, 3, 4, 5 → 409** (`BusinessRuleViolation`). Once membership is
    established the shift's existence is not secret, so these report the real
    conflict: shift not Open ("no longer open for applications"), availability
    gap ("does not cover the whole of this shift"), duplicate ("already applied
    to this shift"), approved-shift overlap ("overlaps another shift you are
    already approved for"). This is a deliberate divergence from the
    expert-facing shift *read*, which 404s a Closed shift — there, existence is
    still being probed; here it is not.
  - **Check order** after the membership gate is cheapest-first: status →
    duplicate (one `Any`) → availability coverage → approved-overlap scan. A
    repeat submit therefore gets "already applied" rather than a stale coverage
    error if the expert's windows changed since.
- **Application withdrawal is deliberately not implemented**, because
  `UNIQUE(ShiftId, ExpertId)` would then permanently bar the expert from
  re-applying and the domain defines no `Withdrawn` status. The design docs
  define no withdraw endpoint and no `Withdrawn` status (`docs/01` §3-8 locks
  `Status` to `Pending | Approved | Rejected`), and `UNIQUE(ShiftId, ExpertId)`
  means a row kept as `Rejected` — or a new `Withdrawn` — would permanently bar
  re-applying: a behaviour change the brief never asked for. The only
  re-application-safe
  option is a hard delete of the `Pending` row, but that is a new
  Expert-initiated delete path (every FK from the Expert side is `NO ACTION` by
  design, `docs/01` §4) and belongs in its own phase with explicit sign-off, not
  a silent addition here. Approval is likewise one-directional ("Cancelling an
  approval is out of scope"), so forward-only application state is the design's
  intent.
- **Approve / reject endpoint shape.** `POST /api/applications/{id}/approval` and
  `POST /api/applications/{id}/rejection` — sub-resource POSTs, no body, both
  Employer-role and scoped to the caller's own projects (another employer's
  application is a 404, matching every other cross-tenant path). The design docs
  fix the *behaviour* of approve/reject, not the URL; this shape keeps the
  decision off `PUT` (it is not an idempotent field write) and mirrors the
  existing `POST /api/shifts/{id}/applications`.
- **Only a `Pending` application can be decided.** The §5 approve sequence lists
  "shift still Open" as its gate; a decided application on a still-Open shift is
  only reachable via a prior direct `reject`, and approving it would silently
  un-reject the row. Both `approval` and `rejection` therefore 409 on a
  non-`Pending` application. This is a precondition guard, not a new status or
  business rule.
- **`GET /api/shifts/{id}/recommendations` is Employer-only and read-only.** The
  ranking exists to help the employer choose whom to approve; the expert has no
  view of it. Rows are written solely by the Python recommender (build order
  step 13). The response is ordered server-side — score descending, then the
  §5 tie-break (fewer approved hours in the month of `Shift.StartUtc`, then
  earlier `AppliedAtUtc`) — so list position is the rank. Approved hours are
  summed in memory (no provider-agnostic SQL for a sum of durations); a shift
  has few applicants, so the set is small.
- **Rule 3 leans on the Prompt 6 merge.** Coverage is a single-window
  containment test, not a gap-stitching one: because overlapping and adjacent
  windows are merged on write, a shift spanning what used to be two touching
  windows (e.g. `10:00–14:00` over `08:00–12:00` + `12:00–16:00`, now stored as
  one `08:00–16:00` window) is accepted. Two windows with a real gap between
  them still fail.

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
- **Claude Code** — Prompt 7 (shifts): employer shift management and the
  expert-facing open-shift read, the `RowVersion` 409 path, and the shift
  visibility / edit-guardrail tests.
- **Claude Code** — dev seed (`chore`): a guarded, idempotent development seed
  (one employer, one expert, a project with the expert assigned, one open shift
  and a covering availability window) run on startup under `Development` /
  `AutoMigrate`, plus the demo-account entry above.
- **Claude Code** — Prompt 8 (applications): `POST /api/shifts/{id}/applications`
  with all five §5 apply rules enforced in `ApplicationService` (rule 2 → 404,
  rules 1/3/4/5 → 409), the dual-role `GET /api/applications` history, the
  no-withdrawal decision, and one passing + one failing test per rule including
  the adjacent-shift and merged-boundary cases.
- **Claude Code** — Prompt 9 (approval): the `ApprovalService` approve flow —
  one explicit transaction over the ownership check, the still-Open check, the
  at-approve-time re-check of apply rule 5, the approved row, the shift's move to
  `Closed`, and the sibling rejections — plus the one-application `reject`, the
  read-only `GET /api/shifts/{id}/recommendations` (score desc, §5 tie-break),
  the `IAppDbContext.BeginTransactionAsync` seam, and the approval / rejection /
  ranking tests.
