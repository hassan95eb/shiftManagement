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

TODO — username and password for the Employer and Expert roles.

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

## Testing

TODO — how to run the tests and what is covered (CLAUDE.md §8).

## Assumptions

TODO — every ambiguous point in the brief and the decision taken. Keep this running.

- **OpenAPI advisory (NU1903).** The scaffold's `Microsoft.AspNetCore.OpenApi`
  reference pulls a transitive `Microsoft.OpenApi 2.0.0` with a known advisory.
  Left as a visible build warning for now (`TreatWarningsAsErrors` on the src
  projects excludes `NU1903` via `WarningsNotAsErrors`). The fix is bundled with
  choosing the OpenAPI/Swagger stack in Prompt 4; remove the exclusion then.

## At Scale

TODO — what a production system would add (multi-timezone, more shift statuses,
refresh tokens, …) and why it was left out here.

## AI Tools Used

Kept as a running log (docs/02 §11).

- **Claude Code** — repository scaffold: solution and project layout, root config files
  (`.gitignore`, `.editorconfig`, `.env.example`), `docker-compose.yml` (db service),
  and the CI workflow.
