using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Infrastructure;

/// <summary>
/// Composition root hook for the Infrastructure layer: the EF Core context and
/// its <see cref="IAppDbContext"/> facade, plus the system clock. The API calls
/// this from <c>Program.cs</c> in the auth phase (CLAUDE.md §10); the
/// connection string name matches <c>ConnectionStrings__Default</c> in
/// <c>.env.example</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("Default")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<IClock, SystemClock>();

        return services;
    }
}
