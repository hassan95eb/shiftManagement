# ShiftFlow recommender

A standalone script that ranks the **applicants of each Open shift** and writes a
`Score` plus a traceable `Reason` into `Recommendations`. The scoring itself is a
set of pure functions; the database work is a thin layer around them.

| File | Purpose |
|---|---|
| `scoring.py` | Pure functions. Standard library only — no I/O, no third-party imports. |
| `config.py` | Resolves weights, `MonthlyCap`, the rating default and the DB connection parts from environment variables. |
| `db.py` | `pymssql` connection, the per-shift read queries, and the idempotent `MERGE` into `Recommendations`. |
| `recommendation.py` | Entry point — `python recommendation.py`. Loops Open shifts, filters applicants by the apply rules, calls `scoring.py`, merges the results. |
| `test_scoring.py` | pytest. Reproduces the three seeded `Recommendations` rows byte for byte. |
| `requirements.txt` | `pymssql` for `db.py`, `pytest` for the tests. |

## Running the recommender

```bash
# Local — needs the DB reachable and the .env at the repo root (or MSSQL_* /
# SCORING__* exported). Reads .env automatically.
cd python
python -m pip install -r requirements.txt
python recommendation.py

# Docker — one-shot job behind a Compose profile, never started by `up`:
docker compose run --rm recommender
```

It is **idempotent**: the `MERGE` is keyed on `(ShiftId, ExpertId)`, so a second
run with no state change inserts nothing, updates nothing, and leaves
`ComputedAtUtc` untouched. An applicant who can no longer be scored — no
availability window covers the shift (so `AvailabilityScore` is undefined), lost
project membership, or picked up an overlapping approved shift — is **skipped**,
and any recommendation a previous run wrote for that pair is **removed** through
the `MERGE`'s `WHEN NOT MATCHED BY SOURCE` arm. Only `Recommendations` is ever
written (docs/01 §4).

## The formula (CLAUDE.md §5, not reinterpreted)

```
RatingScore       = (previous-month rating or 3.0) / 5
WorkloadScore     = 1 - min(ApprovedHours / MonthlyCap, 1)      # MonthlyCap = 160
AvailabilityScore = shift duration / covering availability window duration

FinalScore        = (0.30*Rating + 0.30*Workload + 0.40*Availability) * 100
```

- **"Previous month"** is the month before `Shift.StartUtc`'s month. **`ApprovedHours`**
  are counted for the month of `Shift.StartUtc`, not the current month. Resolving
  both from the database is the caller's job; `scoring.py` takes them as arguments.
- An expert with **no rating row** scores as `3.0`.
- Ranking is over the **applicants of one shift**.

## The `Reason` string

The format matches
`backend/src/ShiftFlow.Infrastructure/Persistence/SeedData.cs` and
`database/03-seed.sql` **exactly** — the recommender overwrites those seeded rows
on its first run, so a reviewer must never see two different explanations for one
score. The three seeded rows for the pool shift (2026-11-10 08:00–16:00, 8 h):

| Expert | `Score` | `Reason` |
|---|---|---|
| `ada` | `76.10` | `Rating 4.6/5 -> 27.6 \| Workload 8h -> 28.5 \| Availability 8/16h -> 20.0 \| Total 76.1` |
| `nate` | `71.50` | `Rating 3.1/5 -> 18.6 \| Workload 0h -> 30.0 \| Availability 8/14h -> 22.9 \| Total 71.5` |
| `kite` | `70.90` | `Rating default 3.0/5 -> 18.0 \| Workload 0h -> 30.0 \| Availability 8/14h -> 22.9 \| Total 70.9` |

Format rules:

- Four segments joined by ` | ` (space, pipe, space); component arrow is ASCII ` -> `.
- `Rating {rating}/5 -> {weighted}` — one decimal on both numbers. A missing
  rating row renders as `Rating default 3.0/5 -> 18.0`: the word `default`, the
  fallback value still shown, so the reason stays traceable.
- `Workload {hours}h -> {weighted}`.
- `Availability {shift}/{window}h -> {weighted}` — `shift/window`, one trailing `h`.
- `Total {sum}` — the sum of the three components, at **one** decimal.
- Every weighted component is at one decimal; `Total` is at one decimal; the
  stored `Score` is the same value widened to two decimals for `DECIMAL(5,2)`.

### Why the total is a sum of rounded parts

`FinalScore` / `Score` is **`rating_component + workload_component +
availability_component`, each already rounded to one decimal**, then quantized to
two decimals. It is *not* the raw formula rounded to two decimals.

`nate` is the row where the two strategies disagree:

```
raw   : (0.30*3.1/5 + 0.30*1 + 0.40*8/14) * 100 = 71.457142...  -> 2 dp = 71.46
parts : 18.6 + 30.0 + 22.9                        = 71.5          -> stored 71.50
```

The seed stores `71.50`. `scoring.py` produces `71.50`. Computing it the other
way would produce numbers that look right and silently disagree with every
seeded row.

### Rounding

Every step uses `decimal.Decimal` with `ROUND_HALF_UP`, so a result never depends
on binary floating-point representation and `x.x5` always rounds away from zero
(`1.25 -> 1.3`, not the banker's `1.2`). `test_rounding_is_half_up_not_bankers`
pins this.

### Conventions this module sets (not matched to an existing case)

The seed exercises only whole hours and non-exact availability. Two format
decisions therefore have no seeded row to match and are fixed here:

- **Fractional hours** render `:g`-style — a whole number stays `8h`, a half hour
  reads `12.5h`. (`scoring.py` implements the `:g` intent directly because
  `Decimal` with `:g` can slip into scientific notation.)
- **An exact availability match** renders `Availability 8/8h -> 40.0`, i.e. the
  same numeric `shift/window` form as every other row. CLAUDE.md §5's example
  wording `Availability exact -> 40.0` was **not** adopted, for consistency.

## The tie-break

`sort_key(applicant)` is a `sorted(key=...)` function implementing CLAUDE.md §5:

1. `FinalScore` **descending**
2. fewer `ApprovedHours` first
3. earlier `AppliedAtUtc` first
4. lower `ExpertId` first

`rank(applicants)` applies it. `applied_at_utc` may be any consistently ordered
type (`datetime`, ISO-8601 string); every item in one ranking must use the same
type.

## Configuration

`config.py` reads these. A missing or blank variable falls back to the default.

### Scoring — `load_scoring_config()`

| Variable | Default | Meaning |
|---|---|---|
| `SCORING__RATINGWEIGHT` | `0.30` | Rating weight |
| `SCORING__WORKLOADWEIGHT` | `0.30` | Workload weight |
| `SCORING__AVAILABILITYWEIGHT` | `0.40` | Availability weight |
| `SCORING__MONTHLYCAP` | `160` | Hours cap for `WorkloadScore` |
| `SCORING__RATINGDEFAULT` | `3.0` | Score for an expert with no rating row |

ASP.NET binds `Scoring:RatingWeight` from `SCORING__RATINGWEIGHT`, so one `.env`
is meant to feed both the API and this script and keep the two scoring
implementations from diverging (docs/02 §5). **The first four names are declared
in `.env.example`; this phase adds `SCORING__RATINGDEFAULT`.**

> As of this phase the backend has **no `ScoringOptions` binding** — nothing on
> the C# side reads these variables yet; the seed hard-codes the CLAUDE.md §5
> values. The shared names are a forward contract, not something the backend
> honours today.

### Database — `load_database_config()`

Discrete parts, because `pymssql` takes `host` / `port` / `user` / `password` /
`database` directly — there is no ADO.NET connection string to parse. Reuses the
`docker-compose` names; `MSSQL_HOST` and `MSSQL_SA_USER` are read here with the
same defaults the backend's `.env.example` uses.

| Variable | Default | Meaning |
|---|---|---|
| `MSSQL_HOST` | `localhost` | SQL Server host |
| `MSSQL_PORT` | `1433` | SQL Server port |
| `MSSQL_DB` | `ShiftFlow` | Database name |
| `MSSQL_SA_USER` | `sa` | Login |
| `MSSQL_SA_PASSWORD` | *(none)* | Password — no default; `recommendation.py` exits with an error if it is unset |

## Worked example — `ada` on the pool shift

Pool shift 2026-11-10 **08:00–16:00** (8 h). `ada`:

| Component | Inputs | Ratio | Weighted (×100), 1 dp |
|---|---|---|---|
| Rating | 2026-10 rating `4.6` → `4.6 / 5` | `0.92` | `0.30 × 0.92 × 100` → **`27.6`** |
| Workload | approved in 2026-11 = one 8 h shift → `1 − 8/160` | `0.95` | `0.30 × 0.95 × 100` → **`28.5`** |
| Availability | 8 h shift in a 16 h window (06:00–22:00) → `8 / 16` | `0.50` | `0.40 × 0.50 × 100` → **`20.0`** |
| **Total** | `27.6 + 28.5 + 20.0` | | **`76.1`** → stored `76.10` |

```
Rating 4.6/5 -> 27.6 | Workload 8h -> 28.5 | Availability 8/16h -> 20.0 | Total 76.1
```

## Running the tests

```bash
cd python
python -m pip install -r requirements.txt
python -m pytest
```
