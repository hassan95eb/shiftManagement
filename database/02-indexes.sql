-- ---------------------------------------------------------------------------
-- ShiftFlow - indexes (secondary, unique, filtered), every migration after
-- the first (currently: the Expert/Employer -> CallAgent/Supervisor rename),
-- and the migration history stamps.
--
-- GENERATED FILE - DO NOT EDIT BY HAND.
-- Produced from the EF Core migrations with:
--   dotnet ef migrations script --idempotent --no-transactions
-- (see backend/src/ShiftFlow.Infrastructure/Persistence/Migrations).
-- Regenerate this file after every migration; never patch it directly.
-- Source of truth for the shape below: docs/01-erd-and-schema.md.
--
-- Run 01-schema.sql and 02-indexes.sql in order, as a pair. This file depends
-- on the tables created by 01-schema.sql, and it also inserts the
-- __EFMigrationsHistory rows that mark each migration as applied. Everything
-- past the InitialCreate indexes must stay in this file rather than moving to
-- 01: a later migration's renames target objects (indexes included) that
-- InitialCreate creates, so migration order — all of InitialCreate, then all
-- of the next migration, in sequence — has to survive the split.
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
    CREATE INDEX [IX_Availabilities_Expert_Range] ON [Availabilities] ([ExpertId], [StartUtc], [EndUtc]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Employers_UserId] ON [Employers] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ExpertProjects_ProjectId] ON [ExpertProjects] ([ProjectId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_ExpertRatings_Expert_Period] ON [ExpertRatings] ([ExpertId], [Period]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Experts_UserId] ON [Experts] ([UserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [UQ_Projects_Employer_Name] ON [Projects] ([EmployerId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Recommendations_ExpertId] ON [Recommendations] ([ExpertId]);
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
    CREATE UNIQUE INDEX [UQ_Recommendations_Shift_Expert] ON [Recommendations] ([ShiftId], [ExpertId]);
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
    CREATE INDEX [IX_ShiftApplications_Expert_Status] ON [ShiftApplications] ([ExpertId], [Status]) INCLUDE ([ShiftId]);
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
    CREATE UNIQUE INDEX [UQ_ShiftApplications_Shift_Expert] ON [ShiftApplications] ([ShiftId], [ExpertId]);
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
    EXEC sp_rename N'[Employers]', N'Supervisors', 'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Supervisors].[PK_Employers]', N'PK_Supervisors', 'INDEX';
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
    EXEC sp_rename N'dbo.FK_Employers_Users_UserId', N'FK_Supervisors_Users_UserId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Experts]', N'CallAgents', 'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[CallAgents].[PK_Experts]', N'PK_CallAgents', 'INDEX';
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
    EXEC sp_rename N'dbo.FK_Experts_Users_UserId', N'FK_CallAgents_Users_UserId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[ExpertProjects]', N'CallAgentProjects', 'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[CallAgentProjects].[ExpertId]', N'CallAgentId', 'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[CallAgentProjects].[PK_ExpertProjects]', N'PK_CallAgentProjects', 'INDEX';
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
    EXEC sp_rename N'dbo.FK_ExpertProjects_Experts_ExpertId', N'FK_CallAgentProjects_CallAgents_CallAgentId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'dbo.FK_ExpertProjects_Projects_ProjectId', N'FK_CallAgentProjects_Projects_ProjectId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[ExpertRatings]', N'Ratings', 'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Ratings].[ExpertId]', N'CallAgentId', 'COLUMN';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Ratings].[PK_ExpertRatings]', N'PK_Ratings', 'INDEX';
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
    EXEC sp_rename N'dbo.CK_ExpertRatings_Score', N'CK_Ratings_Score', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'dbo.FK_ExpertRatings_Experts_ExpertId', N'FK_Ratings_CallAgents_CallAgentId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    ALTER TABLE [Users] DROP CONSTRAINT [CK_Users_Role];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    UPDATE [Users] SET [Role] = 'Supervisor' WHERE [Role] = 'Employer';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    UPDATE [Users] SET [Role] = 'CallAgent' WHERE [Role] = 'Expert';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[ShiftApplications].[ExpertId]', N'CallAgentId', 'COLUMN';
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
    EXEC sp_rename N'dbo.FK_ShiftApplications_Experts_ExpertId', N'FK_ShiftApplications_CallAgents_CallAgentId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Recommendations].[ExpertId]', N'CallAgentId', 'COLUMN';
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
    EXEC sp_rename N'dbo.FK_Recommendations_Experts_ExpertId', N'FK_Recommendations_CallAgents_CallAgentId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Projects].[EmployerId]', N'SupervisorId', 'COLUMN';
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
    EXEC sp_rename N'dbo.FK_Projects_Employers_EmployerId', N'FK_Projects_Supervisors_SupervisorId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC sp_rename N'[Availabilities].[ExpertId]', N'CallAgentId', 'COLUMN';
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
    EXEC sp_rename N'dbo.FK_Availabilities_Experts_ExpertId', N'FK_Availabilities_CallAgents_CallAgentId', N'OBJECT';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260911151445_RenameEmployerSupervisorExpertCallAgent'
)
BEGIN
    EXEC(N'ALTER TABLE [Users] ADD CONSTRAINT [CK_Users_Role] CHECK ([Role] IN (''Supervisor'', ''CallAgent''))');
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
