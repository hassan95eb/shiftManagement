using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShiftFlow.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build an <see cref="AppDbContext"/> at design time.
/// The API does not wire up DI until the auth phase (CLAUDE.md §10), so the
/// tooling has no host to borrow a context from; this factory fills that gap
/// and nothing else.
/// <para>
/// The connection string comes from <c>ConnectionStrings__Default</c> when set
/// (that is the name used by <c>.env.example</c> and Infrastructure's
/// <c>AddInfrastructure</c>); otherwise it falls back to the local
/// docker-compose <c>db</c> service with the placeholder password from
/// <c>.env.example</c>. No real secret lives here.
/// </para>
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalComposeFallback =
        "Server=localhost,1433;Database=ShiftFlow;User Id=sa;" +
        "Password=Your_Strong_Passw0rd!;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? LocalComposeFallback;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString, sql =>
                sql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;

        return new AppDbContext(options);
    }
}
