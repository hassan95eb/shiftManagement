-- ---------------------------------------------------------------------------
-- ShiftFlow - scenario seed data
--
-- HAND-WRITTEN FILE (unlike 01-schema.sql / 02-indexes.sql, which are generated
-- from the EF Core migrations and must not be edited).
--
-- This is the SQL equivalent of ShiftFlow.Infrastructure.Persistence.SeedData:
-- the same logical rows, for the setup path that builds the database from the
-- generated scripts instead of letting the API migrate on startup. Run it once,
-- after 01-schema.sql and 02-indexes.sql, with SET QUOTED_IDENTIFIER ON
-- (sqlcmd -I). It is a no-op on a database that already carries the seed.
--
-- Every account's password is 'Demo!Pass1'. The hash below is a real BCrypt
-- hash (work factor 12) of that string, matching BCryptPasswordHasher; BCrypt
-- embeds its own salt, so one hash serves every row. DEVELOPMENT DATA ONLY.
--
-- Seed scenario map: see README.md -> Database Design -> Seed scenario map for
-- which rows demonstrate which of the five apply rules (CLAUDE.md ss5), passing
-- and failing, plus the completed approval and the recommendation ranking.
-- ---------------------------------------------------------------------------

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM [Users] WHERE [Username] = N'supervisor')
BEGIN
    PRINT 'ShiftFlow scenario seed already present; nothing inserted.';
    RETURN;
END;

BEGIN TRANSACTION;

DECLARE @seeded   datetime2(0) = '2026-10-01T00:00:00';
DECLARE @pwd      nvarchar(256) = N'$2a$12$qlH6tb.BC9jdDlt2vk2D5uGNM6BJH9SSvlnIcegg5QI0na9oDdY8O';

-- --- Users -----------------------------------------------------------------
INSERT INTO [Users] ([Username], [PasswordHash], [Role], [IsActive], [CreatedAtUtc])
VALUES
    (N'manager',   @pwd, N'Manager',    1, @seeded),
    (N'supervisor', @pwd, N'Supervisor', 1, @seeded),
    (N'rival',    @pwd, N'Supervisor', 1, @seeded),
    (N'ada',      @pwd, N'CallAgent',   1, @seeded),
    (N'grace',    @pwd, N'CallAgent',   1, @seeded),
    (N'lin',      @pwd, N'CallAgent',   1, @seeded),
    (N'omar',     @pwd, N'CallAgent',   1, @seeded),
    (N'nate',     @pwd, N'CallAgent',   1, @seeded),
    (N'kite',     @pwd, N'CallAgent',   1, @seeded),
    (N'rosa',     @pwd, N'CallAgent',   1, @seeded);

DECLARE @uSupervisor int = (SELECT [Id] FROM [Users] WHERE [Username] = N'supervisor');
DECLARE @uRival    int = (SELECT [Id] FROM [Users] WHERE [Username] = N'rival');
DECLARE @uAda      int = (SELECT [Id] FROM [Users] WHERE [Username] = N'ada');
DECLARE @uGrace    int = (SELECT [Id] FROM [Users] WHERE [Username] = N'grace');
DECLARE @uLin      int = (SELECT [Id] FROM [Users] WHERE [Username] = N'lin');
DECLARE @uOmar     int = (SELECT [Id] FROM [Users] WHERE [Username] = N'omar');
DECLARE @uNate     int = (SELECT [Id] FROM [Users] WHERE [Username] = N'nate');
DECLARE @uKite     int = (SELECT [Id] FROM [Users] WHERE [Username] = N'kite');
DECLARE @uRosa     int = (SELECT [Id] FROM [Users] WHERE [Username] = N'rosa');

-- --- Supervisors -----------------------------------------------------------
INSERT INTO [Supervisors] ([UserId], [Name], [CreatedAtUtc])
VALUES
    (@uSupervisor, N'Northwind Support',  @seeded),
    (@uRival,    N'Southwind Staffing', @seeded);

DECLARE @eNorthwind int = (SELECT [Id] FROM [Supervisors] WHERE [UserId] = @uSupervisor);
DECLARE @eSouthwind int = (SELECT [Id] FROM [Supervisors] WHERE [UserId] = @uRival);

-- --- CallAgents -----------------------------------------------------------
INSERT INTO [CallAgents] ([UserId], [FullName], [IsActive], [CreatedAtUtc])
VALUES
    (@uAda,   N'Ada Lovelace',   1, @seeded),
    (@uGrace, N'Grace Hopper',   1, @seeded),
    (@uLin,   N'Lin Yao',        1, @seeded),
    (@uOmar,  N'Omar Khayyam',   1, @seeded),
    (@uNate,  N'Nate Silver',    1, @seeded),
    (@uKite,  N'Kite Tanaka',    1, @seeded),
    (@uRosa,  N'Rosa Parks',     1, @seeded);

DECLARE @xAda   int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uAda);
DECLARE @xGrace int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uGrace);
DECLARE @xLin   int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uLin);
DECLARE @xOmar  int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uOmar);
DECLARE @xNate  int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uNate);
DECLARE @xKite  int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uKite);
DECLARE @xRosa  int = (SELECT [Id] FROM [CallAgents] WHERE [UserId] = @uRosa);

-- --- Projects ---------------------------------------------------------
INSERT INTO [Projects] ([SupervisorId], [Name], [IsActive], [CreatedAtUtc])
VALUES
    (@eNorthwind, N'Retail Support',  1, @seeded),
    (@eNorthwind, N'Billing Support', 1, @seeded),
    (@eSouthwind, N'Overflow Desk',   1, @seeded);

DECLARE @pRetail   int = (SELECT [Id] FROM [Projects] WHERE [SupervisorId] = @eNorthwind AND [Name] = N'Retail Support');
DECLARE @pBilling  int = (SELECT [Id] FROM [Projects] WHERE [SupervisorId] = @eNorthwind AND [Name] = N'Billing Support');
DECLARE @pOverflow int = (SELECT [Id] FROM [Projects] WHERE [SupervisorId] = @eSouthwind AND [Name] = N'Overflow Desk');

-- --- CallAgentProjects (assignments) ----------------------------------
INSERT INTO [CallAgentProjects] ([CallAgentId], [ProjectId], [AssignedAtUtc])
VALUES
    (@xAda,   @pRetail,   @seeded),
    (@xGrace, @pRetail,   @seeded),
    (@xLin,   @pRetail,   @seeded),
    (@xOmar,  @pRetail,   @seeded),
    (@xNate,  @pRetail,   @seeded),
    (@xKite,  @pRetail,   @seeded),
    (@xAda,   @pBilling,  @seeded),
    (@xRosa,  @pOverflow, @seeded);

-- --- Availabilities (already merged; no two windows touch) --------
-- grace covers only 06:00-14:00 on 2026-11-10 -> fails apply rule 3 for the
-- 08:00-16:00 Retail shift. omar has no window that day at all. lin covers it
-- exactly. ada/nate/kite cover it with room to spare.
INSERT INTO [Availabilities] ([CallAgentId], [StartUtc], [EndUtc], [CreatedAtUtc])
VALUES
    (@xAda,   '2026-11-10T06:00:00', '2026-11-10T22:00:00', @seeded),
    (@xAda,   '2026-11-12T06:00:00', '2026-11-12T22:00:00', @seeded),
    (@xGrace, '2026-11-10T06:00:00', '2026-11-10T14:00:00', @seeded),
    (@xLin,   '2026-11-10T08:00:00', '2026-11-10T16:00:00', @seeded),
    (@xOmar,  '2026-11-11T07:00:00', '2026-11-11T17:00:00', @seeded),
    (@xNate,  '2026-11-10T06:00:00', '2026-11-10T20:00:00', @seeded),
    (@xKite,  '2026-11-10T06:00:00', '2026-11-10T20:00:00', @seeded),
    (@xKite,  '2026-11-11T06:00:00', '2026-11-11T18:00:00', @seeded),
    (@xRosa,  '2026-11-10T06:00:00', '2026-11-10T22:00:00', @seeded);

-- --- Shifts ---------------------------------------------------------
INSERT INTO [Shifts] ([ProjectId], [StartUtc], [EndUtc], [Status], [CreatedAtUtc])
VALUES
    (@pRetail,  '2026-11-10T08:00:00', '2026-11-10T16:00:00', N'Open',   @seeded), -- applicant pool
    (@pBilling, '2026-11-10T12:00:00', '2026-11-10T20:00:00', N'Open',   @seeded), -- rule 5 pass target for ada
    (@pRetail,  '2026-11-11T08:00:00', '2026-11-11T16:00:00', N'Closed', @seeded), -- rule 1 fail target; completed decision
    (@pRetail,  '2026-11-12T08:00:00', '2026-11-12T16:00:00', N'Open',   @seeded), -- rule 5 fail target for ada
    (@pRetail,  '2026-11-12T09:00:00', '2026-11-12T17:00:00', N'Closed', @seeded); -- ada already approved here

DECLARE @sPool    int = (SELECT [Id] FROM [Shifts] WHERE [ProjectId] = @pRetail  AND [StartUtc] = '2026-11-10T08:00:00');
DECLARE @sBilling int = (SELECT [Id] FROM [Shifts] WHERE [ProjectId] = @pBilling AND [StartUtc] = '2026-11-10T12:00:00');
DECLARE @sClosed  int = (SELECT [Id] FROM [Shifts] WHERE [ProjectId] = @pRetail  AND [StartUtc] = '2026-11-11T08:00:00');
DECLARE @sNov12   int = (SELECT [Id] FROM [Shifts] WHERE [ProjectId] = @pRetail  AND [StartUtc] = '2026-11-12T08:00:00');
DECLARE @sAdaAppr int = (SELECT [Id] FROM [Shifts] WHERE [ProjectId] = @pRetail  AND [StartUtc] = '2026-11-12T09:00:00');

-- --- ShiftApplications --------------------------------------------
-- The pool on the open Retail shift: three pending applicants (add lin through
-- the API for a fourth).
INSERT INTO [ShiftApplications] ([ShiftId], [CallAgentId], [Status], [AppliedAtUtc], [DecidedByUserId], [DecidedAtUtc], [DecisionNote])
VALUES
    (@sPool, @xAda,  N'Pending', '2026-11-01T09:00:00', NULL, NULL, NULL),
    (@sPool, @xNate, N'Pending', '2026-11-01T10:00:00', NULL, NULL, NULL),
    (@sPool, @xKite, N'Pending', '2026-11-02T08:00:00', NULL, NULL, NULL),
    -- The completed decision on the Closed Retail shift: omar approved, kite
    -- rejected with the exact note the approval cascade writes.
    (@sClosed, @xOmar, N'Approved', '2026-10-28T09:00:00', @uSupervisor, '2026-10-30T12:00:00', NULL),
    (@sClosed, @xKite, N'Rejected', '2026-10-28T10:00:00', @uSupervisor, '2026-10-30T12:00:00', N'Shift filled by another CallAgent.'),
    -- ada already holds an approved 09:00-17:00 shift on 2026-11-12.
    (@sAdaAppr, @xAda, N'Approved', '2026-10-29T09:00:00', @uSupervisor, '2026-11-01T08:00:00', NULL);

-- --- Ratings (previous month = 2026-10; kite deliberately has none) --
INSERT INTO [Ratings] ([CallAgentId], [Period], [Score], [CreatedAtUtc])
VALUES
    (@xAda,   '2026-10', 4.6, @seeded),
    (@xAda,   '2026-09', 4.2, @seeded),
    (@xGrace, '2026-10', 3.9, @seeded),
    (@xLin,   '2026-10', 5.0, @seeded),
    (@xOmar,  '2026-10', 2.4, @seeded),
    (@xNate,  '2026-10', 3.1, @seeded);

-- --- Recommendations for the pool shift ---------------------------------
-- Read-only rows the Python recommender would write. Numbers follow the
-- CLAUDE.md ss5 formula for the 8h shift; Score is the sum of the three
-- one-decimal components in Reason.
INSERT INTO [Recommendations] ([ShiftId], [CallAgentId], [Score], [Reason], [ComputedAtUtc])
VALUES
    (@sPool, @xAda,  76.10, N'Rating 4.6/5 -> 27.6 | Workload 8h -> 28.5 | Availability 8/16h -> 20.0 | Total 76.1', '2026-11-03T06:00:00'),
    (@sPool, @xNate, 71.50, N'Rating 3.1/5 -> 18.6 | Workload 0h -> 30.0 | Availability 8/14h -> 22.9 | Total 71.5', '2026-11-03T06:00:00'),
    (@sPool, @xKite, 70.90, N'Rating default 3.0/5 -> 18.0 | Workload 0h -> 30.0 | Availability 8/14h -> 22.9 | Total 70.9', '2026-11-03T06:00:00');

COMMIT TRANSACTION;

PRINT 'ShiftFlow scenario seed inserted.';
GO
