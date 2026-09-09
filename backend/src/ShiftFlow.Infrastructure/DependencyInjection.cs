using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Infrastructure.Identity;
using ShiftFlow.Infrastructure.Persistence;

namespace ShiftFlow.Infrastructure;

/// <summary>
/// Composition root hook for the Infrastructure layer: the EF Core context and
/// its <see cref="IAppDbContext"/> facade, the system clock, and the identity
/// services (password hashing, JWT issuance). The API calls this from
/// <c>Program.cs</c>; the connection string name matches
/// <c>ConnectionStrings__Default</c> and the JWT settings the <c>Jwt</c> section
/// (see <c>.env.example</c>).
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

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
            .Validate(
                o => !string.IsNullOrWhiteSpace(o.Key) && Encoding.UTF8.GetByteCount(o.Key) >= 32,
                "Jwt:Key must be supplied from the environment and be at least 32 bytes.")
            .Validate(o => o.AccessTokenLifetimeMinutes > 0, "Jwt:AccessTokenLifetimeMinutes must be positive.")
            .ValidateOnStart();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
