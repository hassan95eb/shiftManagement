# 04 — v2 Implementation Prompts (scope change)

This document supersedes the v1 prompt plan for all work after v1.0.0.
It covers **backend and data only**. Frontend prompts are deliberately not written
yet: the UI is designed first, implementation follows.

---

## How to use this document

- One prompt = one branch = one review = one merge.
- Branch names follow the prompt number here: `feat/v1-rename`, `feat/v2-manager`, …
- Each prompt ends with a phase report in **the same fixed format already used
  for the v1 prompts**. That format was never captured in a file in this
  repository — it lives only in the review conversations that approved each
  v1 phase. Carry it forward from there; do not invent a new one.
- The branch is never pushed before its report is approved in chat.
- Where this document and `CLAUDE.md` conflict, `CLAUDE.md` wins.

## Global rules for every prompt

1. **At most one EF migration per prompt.** If a prompt would produce two, it was
   scoped wrong — stop and say so in the report.
2. After any migration, regenerate `database/01-schema.sql` and `02-indexes.sql`
   with `dotnet ef migrations script`. Only `03-seed.sql` is hand-written.
3. Conventional Commits, with a body explaining the reasoning and referencing the
   relevant doc under `docs/`.
4. No AI attribution anywhere: no `Co-Authored-By`, no `Generated with`, no
   `--author` override, no AI markers in code comments. The README's
   `AI Tools Used` section still stays accurate.
5. `DATETIME2(0)` in UTC with the `Utc` column suffix; enums as `NVARCHAR` +
   CHECK; `DECIMAL` never `FLOAT`; `INT IDENTITY` keys.
6. Cascade rule unchanged: the Supervisor → Project → Shift path cascades; every
   FK coming from the Call Agent side is `NO ACTION`.
7. Tests use SQLite in-memory, not EF Core InMemory. Time comes from `IClock`.
8. A resource the caller may not see returns **404, never 403**.

---

## Migration order (do not reorder)

```
V1  rename                → 20xx_RenameEmployerSupervisorExpertCallAgent
V2  manager               → 20xx_AddManagerRole
V3  assigned shifts       → 20xx_AddShiftAssignment
V4  attendance            → 20xx_AddAttendanceSessions
V5  agent requests        → 20xx_AddAgentRequests
V6  cover applications    → 20xx_AddApplicationKind
V7  supervisor evaluation → 20xx_AddSupervisorEvaluations
V8  rating breakdown      → 20xx_AddRatingBreakdown
```

V0, V9, V10, V11, V12 produce no migration.

---

# V0 — Redesign the schema document

**Branch:** `docs/v0-erd-v2`
**Migration:** none. **No code in this prompt at all.**

## Goal

Rewrite `docs/01-erd-and-schema.md` to describe the v2 model, so every later
prompt has one authoritative reference.

## Tasks

Rewrite the document to cover:

- Three roles: `Manager` > `Supervisor` > `CallAgent`, with the access rules for
  each.
- Renamed tables `Supervisors` and `CallAgents`, with every FK column renamed to
  match.
- New tables: `AttendanceSessions`, `AgentRequests`, `SupervisorEvaluations`.
- Changed tables: `Users` (role CHECK), `Shifts` (`AssignedCallAgentId`, extended
  status), `CallAgents` (`AnnualLeaveDays`), `Ratings` (`Breakdown`),
  `ShiftApplications` (`Kind`).
- The full Rating and Score formulas from Appendix A of this document, verbatim.
- The shift state machine: `Open → Assigned → Released → Closed`, and which
  transition each operation owns.
- An updated ERD diagram.
- A short "what changed from v1 and why" section at the end.

## Acceptance

- Every table, column, index and constraint named in prompts V1–V12 appears in
  this document.
- No code, no migration, no package change in the diff.

---

# V1 — Global rename

**Branch:** `feat/v1-rename`
**Migration:** `RenameEmployerSupervisorExpertCallAgent`

## Goal

Rename `Employer → Supervisor` and `Expert → CallAgent` across the entire
solution, the database, the Python side and the docs. **Zero behaviour change.**

## Tasks

- Domain entities, DTOs, services, controllers, auth policies, `ICurrentUser`.
- EF configurations; migration using `RenameTable` / `RenameColumn` (not
  drop-and-create), including FK, index and constraint names.
- API routes: `/api/experts` → `/api/call-agents`. Leave every other route shape
  alone.
- `database/03-seed.sql`, and regenerate `01-schema.sql` / `02-indexes.sql`.
- `python/db.py` queries and any column name literals.
- `docs/`, `README.md`, `CLAUDE.md`, `.env.example`.
- Existing tests: rename identifiers only.

## Rules

- **Nothing else ships in this prompt.** No new role, no new column, no logic
  change, no refactor of convenience.
- The login response field currently carrying the employer/expert id is renamed,
  not restructured.

## Acceptance

- The full existing test suite passes **without any test logic being edited** —
  identifier renames only.
- `grep -ri "employer\|expert" --exclude-dir=.git` returns only historical
  references in commit history and the README's changelog section.
- Applying the migration against a fresh SQL Server container reproduces the v1
  data shape with new names.

---

# V2 — Manager role and access scope

**Branch:** `feat/v2-manager`
**Migration:** `AddManagerRole`

## Goal

Introduce the Manager role, move project ownership to the Manager, and replace
ad-hoc supervisor filtering with one scope abstraction.

## Tasks

- Add `Manager` to the `Users.Role` CHECK constraint. **No `Managers` table** —
  a Manager has no profile attributes.
- Seed one Manager user.
- Introduce `IAccessScope` in Application with an implementation that returns
  "all" for a Manager and "own supervisor id" for a Supervisor. Replace every
  direct `EmployerId`/`SupervisorId` filter in services with it.
- Move project create/update/delete authorization from Supervisor to Manager.
  Project creation requires a `SupervisorId` in the body.
- Add `PUT /api/projects/{id}/supervisor` for reassignment (Manager only).
- Supervisor keeps read access to projects assigned to them.

## Rules

- Manager is **read-only** on shifts, applications, agent requests and
  evaluations. No override endpoint exists.
- The 404-not-403 rule still holds for Supervisors. A Manager sees everything, so
  the rule never triggers for them.
- `Projects.SupervisorId` stays `NOT NULL` — the cascade path depends on it.

## Acceptance

- A Supervisor calling `POST /api/projects` gets 403 (role-level, not
  resource-level).
- A Supervisor requesting another supervisor's project gets 404.
- A Manager requesting any project gets 200.
- Tests cover all three.

---

# V3 — Assigned shifts and the state machine

**Branch:** `feat/v3-shift-assignment`
**Migration:** `AddShiftAssignment`

## Goal

Support shifts the Supervisor assigns directly, alongside the existing
application-based open shifts.

## Tasks

- `Shifts.AssignedCallAgentId` — nullable FK, `NO ACTION`.
- Extend the status CHECK to `Open | Assigned | Released | Closed`.
- `POST /api/shifts/{id}/assignment` (Supervisor) — assigns an agent directly.
  Accepts a shift in `Open` (`Open → Assigned`) or, once V5 introduces
  `Released`, in `Released` (`Released → Assigned`) — the direct-fill path a
  Supervisor uses when no cover application arrives (see V6).
- `DELETE /api/shifts/{id}/assignment` — `Assigned → Open` only, allowed only
  while no attendance exists for that shift. It does not accept a `Released`
  shift.
- Extend the existing overlap check so an assigned shift blocks overlapping
  assignment and overlapping application for the same agent.

## Rules

- Status is never settable directly through an update endpoint. Each transition
  belongs to exactly one operation:
  - `Open → Assigned` — assignment endpoint
  - `Open → Closed` — the existing application-approval transaction
  - `Assigned → Released` — leave approval (V5)
  - `Released → Assigned` — the assignment endpoint (direct fill, this prompt)
    or cover approval (V6)
- Assignment does not go through `ShiftApplications` and creates no application
  row.
- The existing rule stands: a shift's time may be corrected only while `Open`
  with zero applications.

## Acceptance

- Assigning an agent who already has an overlapping shift returns 409.
- Assigning to a non-`Open` shift returns 409.
- `RowVersion` conflict on concurrent assignment returns 409, with a test.

---

# V4 — Attendance

**Branch:** `feat/v4-attendance`
**Migration:** `AddAttendanceSessions`

## Goal

Record presence, expose the live active-agent count, and derive absence.

## Tasks

- `AttendanceSessions(Id, CallAgentId, ShiftId, StartedAtUtc, LastSeenUtc,
  EndedAtUtc NULL)`, FKs `NO ACTION`, index on `(CallAgentId, StartedAtUtc)` and
  a filtered index on open sessions.
- On successful login as a Call Agent: if the current time falls inside a shift
  the agent is committed to, open a session (or reuse the open one for that
  shift).
- `POST /api/attendance/heartbeat` — updates `LastSeenUtc` on the open session.
  Client interval is 60 seconds.
- `POST /api/attendance/logout` — sets `EndedAtUtc`.
- `GET /api/attendance/active` (Supervisor scoped, Manager global) — agents whose
  open session has `LastSeenUtc` within the staleness threshold **and** whose
  current time is inside the shift window.
- Present hours for a shift = summed session overlap with the shift window,
  capped at the shift duration.
- Absence is **computed on read**, never stored: a committed shift that has ended
  with zero present hours and no approved leave or downtime.

## Rules

- No background job, no hosted service, no SignalR.
- Staleness threshold configurable, default 120 seconds.
- A session never counts time outside its shift window.
- An abandoned session (no logout, no heartbeat) simply goes stale — it is not
  retroactively closed.

## Acceptance

- A session with `LastSeenUtc` older than the threshold is excluded from
  `/active`, with a test using a fixed `IClock`.
- Overlapping sessions on one shift do not double-count hours.
- Absence computation is covered for: no session, partial session, session plus
  approved leave.

---

# V5 — Agent requests: leave and downtime

**Branch:** `feat/v5-agent-requests`
**Migration:** `AddAgentRequests`

## Goal

One request table serving both leave and downtime, with a Jalali leave year.

## Tasks

- `AgentRequests(Id, CallAgentId, ShiftId, RequestType, RequestedAtUtc, StartUtc,
  EndUtc, Reason, Status, DecidedByUserId, DecidedAtUtc, DecisionNote)`.
  `RequestType` CHECK `Leave | Downtime`; `Status` CHECK
  `Pending | Approved | Rejected`.
- `CallAgents.AnnualLeaveDays` `INT NOT NULL DEFAULT 26`.
- `ILeaveYear` in Application, backed by `System.Globalization.PersianCalendar`,
  resolving the current Jalali year boundary. Injected, never called statically,
  so tests can pin it.
- `POST /api/agent-requests` (Call Agent), `POST /api/agent-requests/{id}/approval`
  and `/rejection` (Supervisor).
- The request payload returned to the Supervisor includes the agent's remaining
  leave balance.
- Add the filtered unique index below, guarding against two `Leave` requests
  being approved on the same shift under concurrent approval:

  ```sql
  CREATE UNIQUE INDEX UX_AgentRequests_OneApprovedLeave
  ON AgentRequests (ShiftId)
  WHERE Status = 'Approved' AND RequestType = 'Leave';
  ```

  Filtered on `Leave` only — several approved `Downtime` rows on one shift are
  legal.

## Rules

- **Leave:** whole-day, tied to one committed shift. One approved leave request
  deducts exactly **one day** from the quota regardless of shift length.
- Balance is **computed**, never stored: `AnnualLeaveDays` minus approved leave
  days in the current Jalali year.
- The leave-day boundary is the **Tehran calendar date** (UTC+03:30, no DST), not
  the UTC date. Converting at the UTC boundary shifts any request near Nowruz
  into the wrong year — there must be a test for exactly this.
- Approving leave on an `Assigned` shift sets the shift to `Released` in the same
  transaction.
- Approved leave is never an absence and is removed from the Attendance
  denominator.
- **Downtime:** only for a shift that has ended or is in progress; `StartUtc` and
  `EndUtc` must sit inside the shift window; approved downtime does not touch the
  leave quota; monthly approved-downtime hours are capped at 8, configurable.
- The API contract stays UTC ISO. No Jalali string ever crosses the API boundary.

## Acceptance

- A leave request submitted on 1 Farvardin at 00:30 Tehran time is counted in the
  new Jalali year, not the previous one.
- Leave exceeding the remaining balance returns 409 at approval time, not only at
  request time.
- Downtime beyond the monthly cap returns 409.
- Downtime outside the shift window returns 400.
- A concurrency test proves double leave approval on the same shift fails at
  the database level (`UX_AgentRequests_OneApprovedLeave`).

---

# V6 — Cover after approved leave

**Branch:** `feat/v6-cover`
**Migration:** `AddApplicationKind`

## Goal

Let other agents pick up a released shift, reusing `ShiftApplications`.

## Tasks

- `ShiftApplications.Kind` CHECK `Extra | Cover`, default `Extra` for existing
  rows.
- Applying to a `Released` shift creates a `Cover` application.
- Approving a cover sets `AssignedCallAgentId` to the covering agent and the
  shift back to `Assigned`, inside one transaction, rejecting all other pending
  cover applications with a decision note.
- Rework the indexes: `UNIQUE(ShiftId, CallAgentId)` must not bar a second,
  legitimate cover round, and `UX_ShiftApplications_OneApproved` must permit one
  approved `Extra` and one approved `Cover` to be distinguishable. State the
  chosen index design in the phase report before implementing.

## Rules

- The agent who went on leave cannot apply to cover their own released shift.
- Rules are rechecked at approval time — the covering agent's overlap is
  revalidated then, not only at apply time.
- Withdrawal stays unimplemented, as in v1.
- A Released shift can also be filled directly through the assignment endpoint
  (V3) without any cover application arriving first. **Open question, not
  decided here:** does that direct fill need to reject any pending `Cover`
  applications on the same shift, the way a cover approval rejects competing
  ones? State the answer in this prompt's phase report before implementing.

## Acceptance

- Two agents applying to cover, one approved: the other is `Rejected` with a
  note, and the shift shows the covering agent.
- The original agent's approved leave is untouched by the cover.
- A concurrency test proves the database guard holds under parallel approval.

---

# V7 — Supervisor evaluations

**Branch:** `feat/v7-evaluations`
**Migration:** `AddSupervisorEvaluations`

## Goal

Give the Supervisor a real form for the qualitative half of the Rating.

## Tasks

- `SupervisorEvaluations(Id, CallAgentId, SupervisorId, Period, ScoreValue,
  Note, CreatedAtUtc, UpdatedAtUtc)`.
  `Period` is `NVARCHAR(7)` in `2026-08` form; `ScoreValue` is
  `DECIMAL(2,1)` with a CHECK of 1.0–5.0; `Note` is `NOT NULL` and non-empty.
- `UNIQUE(CallAgentId, SupervisorId, Period)` — note the three columns, not two.
- `POST` and `PUT /api/evaluations` (Supervisor), `GET` scoped by role.

## Rules

- When an agent worked under several Supervisors in one period, each may submit
  one evaluation and the **mean** is used downstream. This is why the unique key
  includes `SupervisorId`.
- Submit and edit window: period `P` is open until the end of month `P+1`. After
  that, 409.
- A Supervisor may only evaluate an agent who had at least one committed shift on
  one of their projects in that period.
- Manager reads evaluations; Manager does not write them.

## Acceptance

- Two supervisors, one agent, one period: both rows persist and the mean is what
  the rating engine reads.
- Editing after the window closes returns 409.
- An empty note returns 400.

---

# V8 — Rating engine (C#) and options binding

**Branch:** `feat/v8-rating-engine`
**Migration:** `AddRatingBreakdown`

## Goal

Compute the Rating from real data on the C# side, and finally bind both options
classes.

## Tasks

- `Ratings.Breakdown` `NVARCHAR(400) NULL`. In the same migration, alter
  `Ratings.Period` from `CHAR(7)` to `NVARCHAR(7)` to match
  `SupervisorEvaluations.Period` — still one migration for this prompt.
- Implement the rating computation exactly as Appendix A specifies.
- `POST /api/ratings/recompute?period=2026-08` (Manager only) — manual trigger,
  idempotent, same upsert semantics as the Python side.
- Bind `ScoringOptions` and `RatingOptions` from configuration. This closes the
  long-standing open item where the scoring formula lived only in `SeedData.cs`,
  the README and `scoring.py`.
- `SeedData.cs` now calls the same engine instead of carrying its own copy.

## Rules

- `Decimal` with `ROUND_HALF_UP` throughout. Never `double`.
- Rating rendered at 1 decimal place, matching the existing `Rating 4.6/5` form.
- An agent with zero committed shifts in the period gets **no `Ratings` row** —
  the existing `Rating default 3.0/5` path then applies unchanged.
- Missing supervisor evaluation → weights normalized, Rating comes from the
  automatic part alone.
- Breakdown format is fixed; see Appendix A.

## Acceptance

- Golden-value tests for: full attendance with evaluation, full attendance
  without evaluation, one unexcused absence, one late-notice leave, zero
  committed shifts, zero attended shifts.
- Recomputing the same period twice produces byte-identical rows.

---

# V9 — Score formula update

**Branch:** `feat/v9-score-workload`
**Migration:** none

## Goal

Point the Workload component at committed hours.

## Tasks

- `WorkloadScore = 1 - min(CommittedHours / 160, 1)`, where `CommittedHours` for
  the month of `Shift.StartUtc` = assigned shift hours + approved extra
  application hours + approved cover hours.
- Update `SeedData.cs`, `README.md` and `python/scoring.py` together.

## Rules

- **The Reason format does not change**, and neither does the rounding rule: the
  stored Score is the sum of the three 1-dp-rounded components, widened to 2 dp.
- Rating and Availability components are untouched.
- Hours worked shown in the profile come from attendance; Workload in the score
  comes from commitments. These are deliberately different numbers — say so in
  the README.

## Acceptance

- Existing scoring tests still pass after their Workload inputs are updated.
- C# and Python produce identical Reason strings for the same fixture.

---

# V10 — Stats, profile and history

**Branch:** `feat/v10-stats`
**Migration:** none

## Goal

Serve the profile and dashboard data for all three roles.

## Tasks

- `GET /api/stats` grows from two shapes to three: Manager, Supervisor,
  CallAgent.
- `GET /api/call-agents/{id}/profile` — hours worked (attendance), leave days
  used and remaining, current Rating with its Breakdown, attendance history,
  request history.
- Manager stats: agents currently working, shift coverage across all projects,
  pending request counts.
- Activity history is aggregated from existing tables. **No new activity-log
  table.**

## Rules

- Every number must come from real data. No invented or placeholder values.
- Scoping goes through `IAccessScope` — no new filtering logic.
- Paginate any history endpoint that can grow without bound.

## Acceptance

- A Supervisor requesting an agent outside their projects gets 404.
- Each of the three stat shapes has a test asserting its exact field set.

---

# V11 — Seed and tests

**Branch:** `feat/v11-seed-tests`
**Migration:** none

## Goal

Make every new rule visible to a reviewer without them running anything.

## Tasks

Rewrite `database/03-seed.sql` scenario-first, covering at minimum:

- One Manager, two Supervisors, six Call Agents across two projects.
- An agent with perfect attendance and a high evaluation.
- An agent with perfect attendance and **no** evaluation, to demonstrate weight
  normalization.
- An agent with one unexcused absence.
- An agent with one late-notice leave, showing it is not a free alternative to
  an absence.
- An agent with approved downtime, showing it is excluded from the denominator.
- A released shift covered by another agent.
- An agent with zero committed shifts, falling through to the 3.0 default.
- A leave request adjacent to the Nowruz boundary.

Add the cross-cutting tests that no single earlier prompt owns.

## Acceptance

- A fresh container plus seed yields a database where each scenario above is
  identifiable by a comment in the seed file.
- Seeded `Ratings` rows match what V8's engine computes for the same data.

---

# V12 — Python side and documentation

**Branch:** `feat/v12-python-docs`
**Migration:** none

## Goal

Reproduce the rating on the Python side and bring the docs up to date.

## Tasks

- New `python/rating.py --period 2026-08`, pure logic separated from `db.py` in
  the same way `scoring.py` already is, upserting `Ratings` including
  `Breakdown` via `MERGE`.
- Update `recommend.py` for the renamed tables and the new Workload source.
- A parity test that runs the C# engine and the Python engine over the same
  fixture and asserts identical `Rating` values and identical `Breakdown`
  strings.
- README: the three roles, the attendance model, the Jalali leave year, the
  Rating and Score formulas, the full env var table, and the pipeline order.
- Move anything cut from scope into the README's `Assumptions / At Scale`
  section.

## Rules

- **Pipeline order is load-bearing and must be documented:**
  `evaluations submitted → rating.py → recommend.py`. Running the recommender
  first makes it score against stale ratings.
- Env var names follow the ASP.NET path convention so one variable feeds both
  sides. The database still reaches Python as discrete `MSSQL_*` parts, since
  `pymssql` takes no connection string.
- Both `rating.py` and the C# recompute endpoint are idempotent and may be run in
  either order.

## Acceptance

- The parity test passes on the seeded dataset.
- `docker compose up` followed by the documented pipeline reproduces the seeded
  scores and ratings exactly.
- Tag `v2.0.0`.

---

# Appendix A — Formulas

## Rating

```
CommittedShifts = assigned + approved extra + approved cover
ExpectedHours   = committed shift hours
                  - approved leave hours
                  - approved downtime hours

Attendance      = min(PresentHours / ExpectedHours, 1)
Punctuality     = OnTimeShifts / AttendedShifts
Reliability     = max(1 - (UnexcusedAbsences + LateNoticeLeaves)
                          / CommittedShifts, 0)

AutoRaw         = 0.50*Attendance + 0.30*Punctuality + 0.20*Reliability
SupRaw          = (mean SupervisorScore - 1) / 4

Rating          = 1.0 + 4.0 * (0.50*AutoRaw + 0.50*SupRaw)
Rating          = 1.0 + 4.0 * AutoRaw          when no evaluation exists
```

- A component whose denominator is zero is **undefined**, not zero: Attendance
  is undefined when `ExpectedHours = 0`; Punctuality is undefined when
  `AttendedShifts = 0`.
- An undefined component is dropped from `AutoRaw` and the remaining automatic
  weights are renormalized proportionally — the same technique already used
  when no supervisor evaluation exists.
- If all three automatic components are undefined, no `Ratings` row is written
  for the period; the 3.0 default applies.
- On time: first heartbeat ≤ shift start + grace (default 5 minutes).
- Late-notice leave: requested less than 24 hours before shift start, **even if
  approved**.
- Unexcused absence: committed shift, ended, zero present hours, no approved
  leave or downtime.
- Period: the month **before** the shift's month — unchanged from v1.
- No committed shifts in the period → no `Ratings` row → default 3.0 applies.

### Breakdown format

```
Attendance 152/160h -> 0.475 | Punctuality 18/20 -> 0.270 | Reliability 1 absence, 1 late leave -> 0.180 | Auto 0.925 -> 0.463 | Supervisor 4.5/5 -> 0.438 | Rating 4.6/5
```

ASCII `->`, components at 3 dp, Rating at 1 dp. When no evaluation exists, the
`Supervisor` segment is replaced by `Supervisor none -> normalized`. When a
component's denominator is zero, its own segment is replaced the same way:
`Attendance n/a -> normalized` or `Punctuality n/a -> normalized`.

## Score — shape unchanged from v1

```
Score             = (0.30*RatingScore + 0.30*WorkloadScore + 0.40*AvailabilityScore) * 100
RatingScore       = (rating ?? 3.0) / 5
WorkloadScore     = 1 - min(CommittedHours / 160, 1)     ← committed, not attended
AvailabilityScore = shift duration / covering availability duration
```

Reason format, rounding and the sum-of-rounded-components rule are all unchanged.

## Appendix B — Configuration

```
SCORING__RATINGWEIGHT          0.30
SCORING__WORKLOADWEIGHT        0.30
SCORING__AVAILABILITYWEIGHT    0.40
SCORING__MONTHLYCAP            160
SCORING__RATINGDEFAULT         3.0

RATING__AUTOWEIGHT             0.50
RATING__SUPERVISORWEIGHT       0.50
RATING__ATTENDANCEWEIGHT       0.50
RATING__PUNCTUALITYWEIGHT      0.30
RATING__RELIABILITYWEIGHT      0.20
RATING__GRACEMINUTES           5
RATING__LEAVENOTICEHOURS       24
RATING__DOWNTIMECAPHOURS       8

LEAVE__ANNUALDAYSDEFAULT       26
ATTENDANCE__STALENESSSECONDS   120
ATTENDANCE__HEARTBEATSECONDS   60
```
