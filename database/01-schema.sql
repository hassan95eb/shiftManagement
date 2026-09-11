-- ---------------------------------------------------------------------------
-- ShiftFlow - table schema (tables, columns, PK/FK, CHECK constraints)
--
-- GENERATED FILE - DO NOT EDIT BY HAND.
-- Produced from the EF Core migrations with:
--   dotnet ef migrations script --idempotent --no-transactions
-- (see backend/src/ShiftFlow.Infrastructure/Persistence/Migrations).
-- Regenerate this file after every migration; never patch it directly.
-- Source of truth for the shape below: docs/01-erd-and-schema.md.
--
-- Run 01-schema.sql and 02-indexes.sql in order, as a pair. 02-indexes.sql
-- depends on the tables created here, and it also inserts the
-- __EFMigrationsHistory rows that mark each migration as applied.
--
-- The session must have SET QUOTED_IDENTIFIER ON (with sqlcmd, pass -I). The
-- filtered index in 02-indexes.sql will not create without it.
-- ---------------------------------------------------------------------------

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [Username] nvarchar(64) NOT NULL,
        [PasswordHash] nvarchar(256) NOT NULL,
        [Role] nvarchar(16) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2(0) NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('Employer', 'Expert'))
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Employers] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_Employers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Employers_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Experts] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [FullName] nvarchar(128) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_Experts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Experts_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Projects] (
        [Id] int NOT NULL IDENTITY,
        [EmployerId] int NOT NULL,
        [Name] nvarchar(128) NOT NULL,
        [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_Projects] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Projects_Employers_EmployerId] FOREIGN KEY ([EmployerId]) REFERENCES [Employers] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Availabilities] (
        [Id] int NOT NULL IDENTITY,
        [ExpertId] int NOT NULL,
        [StartUtc] datetime2(0) NOT NULL,
        [EndUtc] datetime2(0) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_Availabilities] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Availabilities_Range] CHECK ([EndUtc] > [StartUtc]),
        CONSTRAINT [FK_Availabilities_Experts_ExpertId] FOREIGN KEY ([ExpertId]) REFERENCES [Experts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpertRatings] (
        [Id] int NOT NULL IDENTITY,
        [ExpertId] int NOT NULL,
        [Period] char(7) NOT NULL,
        [Score] decimal(2,1) NOT NULL,
        [CreatedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_ExpertRatings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ExpertRatings_Score] CHECK ([Score] BETWEEN 1.0 AND 5.0),
        CONSTRAINT [FK_ExpertRatings_Experts_ExpertId] FOREIGN KEY ([ExpertId]) REFERENCES [Experts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [ExpertProjects] (
        [ExpertId] int NOT NULL,
        [ProjectId] int NOT NULL,
        [AssignedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_ExpertProjects] PRIMARY KEY ([ExpertId], [ProjectId]),
        CONSTRAINT [FK_ExpertProjects_Experts_ExpertId] FOREIGN KEY ([ExpertId]) REFERENCES [Experts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ExpertProjects_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [ProjectId] int NOT NULL,
        [StartUtc] datetime2(0) NOT NULL,
        [EndUtc] datetime2(0) NOT NULL,
        [Status] nvarchar(16) NOT NULL DEFAULT N'Open',
        [CreatedAtUtc] datetime2(0) NOT NULL,
        [RowVersion] rowversion NOT NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Shifts_Range] CHECK ([EndUtc] > [StartUtc]),
        CONSTRAINT [CK_Shifts_Status] CHECK ([Status] IN ('Open', 'Closed')),
        CONSTRAINT [FK_Shifts_Projects_ProjectId] FOREIGN KEY ([ProjectId]) REFERENCES [Projects] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [Recommendations] (
        [Id] int NOT NULL IDENTITY,
        [ShiftId] int NOT NULL,
        [ExpertId] int NOT NULL,
        [Score] decimal(5,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [ComputedAtUtc] datetime2(0) NOT NULL,
        CONSTRAINT [PK_Recommendations] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Recommendations_Score] CHECK ([Score] BETWEEN 0 AND 100),
        CONSTRAINT [FK_Recommendations_Experts_ExpertId] FOREIGN KEY ([ExpertId]) REFERENCES [Experts] ([Id]),
        CONSTRAINT [FK_Recommendations_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260909225615_InitialCreate'
)
BEGIN
    CREATE TABLE [ShiftApplications] (
        [Id] int NOT NULL IDENTITY,
        [ShiftId] int NOT NULL,
        [ExpertId] int NOT NULL,
        [Status] nvarchar(16) NOT NULL,
        [AppliedAtUtc] datetime2(0) NOT NULL,
        [DecidedByUserId] int NULL,
        [DecidedAtUtc] datetime2(0) NULL,
        [DecisionNote] nvarchar(256) NULL,
        CONSTRAINT [PK_ShiftApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ShiftApplications_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected')),
        CONSTRAINT [FK_ShiftApplications_Experts_ExpertId] FOREIGN KEY ([ExpertId]) REFERENCES [Experts] ([Id]),
        CONSTRAINT [FK_ShiftApplications_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ShiftApplications_Users_DecidedByUserId] FOREIGN KEY ([DecidedByUserId]) REFERENCES [Users] ([Id])
    );
END;
GO
