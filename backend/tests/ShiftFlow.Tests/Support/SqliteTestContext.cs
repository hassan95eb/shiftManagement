using System;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Tests.Support;

/// <summary>
/// A fresh relational database per test, backed by SQLite in-memory. The schema
/// is created from the real EF model so unique and CHECK constraints are in
/// force (CLAUDE.md §8) — EF Core InMemory would ignore exactly the constraints
/// several of these tests rely on.
/// </summary>
/// <remarks>
/// The connection is held open for the lifetime of the instance; an in-memory
/// SQLite database exists only while at least one connection to it is open.
/// The one SQL-Server-ism in the model, <c>Users.CreatedAtUtc</c>'s
/// <c>DEFAULT (SYSUTCDATETIME())</c>, is stripped from the generated DDL — the
/// services always set the column explicitly through <see cref="IClock"/>, so
/// the default never needs to fire.
/// </remarks>
public sealed class SqliteTestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = new AppDbContext(BuildOptions());

        Db.Database.ExecuteSqlRaw(ToSqliteDdl(Db.Database.GenerateCreateScript()));
    }

    public AppDbContext Db { get; }

    /// <summary>A second context on the same database — for asserting on state a service wrote.</summary>
    public AppDbContext NewContext() => new(BuildOptions());

    private DbContextOptions<AppDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;

    /// <summary>
    /// Rewrites the two SQL-Server-isms in the generated DDL so SQLite accepts it:
    /// drop the <c>SYSUTCDATETIME()</c> default (services always set the column),
    /// and give <c>Shifts.RowVersion</c> a static default since SQLite has no
    /// <c>rowversion</c> type to fill it (this phase only ever inserts shifts).
    /// </summary>
    private static string ToSqliteDdl(string ddl)
    {
        ddl = Regex.Replace(ddl, @" DEFAULT \(SYSUTCDATETIME\(\)\)", string.Empty);
        ddl = ddl.Replace(
            @"""RowVersion"" BLOB NOT NULL",
            @"""RowVersion"" BLOB NOT NULL DEFAULT x'0000000000000000'");
        return ddl;
    }

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
