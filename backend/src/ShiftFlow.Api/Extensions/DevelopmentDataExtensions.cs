using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Api.Extensions;

/// <summary>
/// Startup convenience for local, hands-on testing: apply any pending migrations
/// and insert the scenario seed (<see cref="SeedData"/>). Both steps run only in
/// the <c>Development</c> environment or when the <c>AutoMigrate</c> configuration
/// flag is set, and both are safe to run on every startup.
/// <para>
/// A production deployment leaves <c>AutoMigrate</c> unset and runs
/// <c>dotnet ef database update</c> (or the generated <c>database/*.sql</c>) as a
/// separate, reviewed deploy step — the API process never migrates a real
/// database, and the scenario seed never reaches one.
/// </para>
/// </summary>
public static class DevelopmentDataExtensions
{
    public static async Task UseDevelopmentSeedAsync(
        this WebApplication app,
        CancellationToken cancellationToken = default)
    {
        var enabled = app.Environment.IsDevelopment()
            || app.Configuration.GetValue<bool>("AutoMigrate");
        if (!enabled)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var seeder = new SeedData(
            db,
            services.GetRequiredService<IPasswordHasher>(),
            services.GetRequiredService<ILogger<SeedData>>());

        await seeder.SeedAsync(cancellationToken);
    }
}
