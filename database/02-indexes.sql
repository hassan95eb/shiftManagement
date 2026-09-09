-- ---------------------------------------------------------------------------
-- ShiftFlow - indexes (secondary, unique, filtered) + migration history stamp
--
-- GENERATED FILE - DO NOT EDIT BY HAND.
-- Produced from the EF Core migrations with:
--   dotnet ef migrations script --idempotent --no-transactions
-- (see backend/src/ShiftFlow.Infrastructure/Persistence/Migrations).
-- Regenerate this file after every migration; never patch it directly.
-- Source of truth for the shape below: docs/01-erd-and-schema.md.
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
