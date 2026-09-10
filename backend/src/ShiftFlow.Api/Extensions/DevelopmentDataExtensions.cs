using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Api.Extensions;

/// <summary>
/// Startup glue for local, hands-on testing: apply any pending migrations and
/// insert the development login seed (<see cref="DevelopmentSeeder"/>). Both
/// steps run only in the Development environment or when the <c>AutoMigrate</c>
/// configuration flag is set, and both are safe to run on every startup.
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

        var seeder = new DevelopmentSeeder(
            db,
            services.GetRequiredService<IPasswordHasher>(),
            services.GetRequiredService<IClock>(),
            services.GetRequiredService<ILogger<DevelopmentSeeder>>());

        await seeder.SeedAsync(cancellationToken);
    }
}
