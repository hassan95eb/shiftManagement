using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.AgentRequests;
using ShiftFlow.Application.Features.Attendance;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Application.Features.Ratings;
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
        services.AddSingleton<ILeaveYear, PersianLeaveYear>();

        services.AddOptions<RatingOptions>()
            .Bind(configuration.GetSection(RatingOptions.SectionName))
            .Validate(o => o.DowntimeCapHours > 0,
                "Rating:DowntimeCapHours must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<LeaveOptions>()
            .Bind(configuration.GetSection(LeaveOptions.SectionName))
            .Validate(o => o.AnnualDaysDefault > 0,
                "Leave:AnnualDaysDefault must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<AttendanceOptions>()
            .Bind(configuration.GetSection(AttendanceOptions.SectionName))
            .Validate(o => o.StalenessSeconds > 0,
                "Attendance:StalenessSeconds must be greater than zero.")
            .Validate(o => o.HeartbeatSeconds > 0,
                "Attendance:HeartbeatSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<JwtOptions>, JwtOptionsValidator>();

        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
