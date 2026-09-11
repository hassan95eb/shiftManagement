-- ---------------------------------------------------------------------------
-- ShiftFlow - indexes (secondary, unique, filtered) + migration history stamps
--
-- GENERATED FILE - DO NOT EDIT BY HAND.
-- Produced from the EF Core migrations with:
--   dotnet ef migrations script --idempotent --no-transactions
-- (see backend/src/ShiftFlow.Infrastructure/Persistence/Migrations), then
-- split by STATEMENT KIND (see docs/02-repository-structure.md), not by
-- position in the generated file.
-- Regenerate this file after every migration; never patch it directly.
-- Source of truth for the shape below: docs/01-erd-and-schema.md.
--
-- Only CREATE INDEX / CREATE UNIQUE INDEX and index renames live here, plus
-- the __EFMigrationsHistory stamp for each migration, placed right after
-- that migration's own last guarded statement (never earlier -- an earlier
-- stamp would make a later statement guarded on the same migration id skip
-- itself as "already applied").
--
-- A CREATE INDEX carried over from an earlier migration is written against
-- the CURRENT table/column names, not the names that migration originally
-- used: by the time this file runs, 01-schema.sql has already renamed them,
-- so the original names no longer resolve. Its guard condition is left
-- alone (it is still that earlier migration's own work), and the index
-- itself still gets its original name -- the later migration's own rename
-- statement (copied verbatim) renames it to the current name right after,
-- exactly as EF generated it.
--
-- This file depends on the tables created by 01-schema.sql and, critically,
-- on 01-schema.sql's renames having already run -- it does not stand alone.
--
-- The session must have SET QUOTED_IDENTIFIER ON (with sqlcmd, pass -I). The
-- filtered index below (UX_ShiftApplications_OneApproved) will not create
-- without it.
-- ---------------------------------------------------------------------------

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Availabilities_Expert_Range] ON [Availabilities] ([CallAgentId], [StartUtc], [EndUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Employers_UserId] ON [Supervisors] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpertProjects_ProjectId] ON [CallAgentProjects] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ExpertRatings_Expert_Period] ON [Ratings] ([CallAgentId], [Period]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Experts_UserId] ON [CallAgents] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Projects_Employer_Name] ON [Projects] ([SupervisorId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Recommendations_ExpertId] ON [Recommendations] ([CallAgentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Recommendations_Shift_Score] ON [Recommendations] ([ShiftId], [Score] DESC);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Recommendations_Shift_Expert] ON [Recommendations] ([ShiftId], [CallAgentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShiftApplications_DecidedByUserId] ON [ShiftApplications] ([DecidedByUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShiftApplications_Expert_Status] ON [ShiftApplications] ([CallAgentId], [Status]) INCLUDE ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ShiftApplications_Shift_Status] ON [ShiftApplications] ([ShiftId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ShiftApplications_Shift_Expert] ON [ShiftApplications] ([ShiftId], [CallAgentId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_ShiftApplications_OneApproved] ON [ShiftApplications] ([ShiftId]) WHERE [Status] = ''Approved''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_ProjectId_Status] ON [Shifts] ([ProjectId], [Status]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_Status_StartUtc] ON [Shifts] ([Status], [StartUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Users_Username] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260909225615_InitialCreate', N'10.0.11');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Supervisors].[UQ_Employers_UserId]', N'UQ_Supervisors_UserId', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[CallAgents].[UQ_Experts_UserId]', N'UQ_CallAgents_UserId', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[CallAgentProjects].[IX_ExpertProjects_ProjectId]', N'IX_CallAgentProjects_ProjectId', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Ratings].[UQ_ExpertRatings_Expert_Period]', N'UQ_Ratings_CallAgent_Period', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[ShiftApplications].[UQ_ShiftApplications_Shift_Expert]', N'UQ_ShiftApplications_Shift_CallAgent', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[ShiftApplications].[IX_ShiftApplications_Expert_Status]', N'IX_ShiftApplications_CallAgent_Status', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Recommendations].[UQ_Recommendations_Shift_Expert]', N'UQ_Recommendations_Shift_CallAgent', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Recommendations].[IX_Recommendations_ExpertId]', N'IX_Recommendations_CallAgentId', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Projects].[UQ_Projects_Employer_Name]', N'UQ_Projects_Supervisor_Name', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Availabilities].[IX_Availabilities_Expert_Range]', N'IX_Availabilities_CallAgent_Range', 'INDEX';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911151445_RenameEmployerSupervisorExpertCallAgent', N'10.0.11');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911203653_AddShiftAssignment'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Shifts_AssignedCallAgentId] ON [Shifts] ([AssignedCallAgentId]) WHERE [AssignedCallAgentId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911203653_AddShiftAssignment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260911203653_AddShiftAssignment', N'10.0.11');
END;
GO
