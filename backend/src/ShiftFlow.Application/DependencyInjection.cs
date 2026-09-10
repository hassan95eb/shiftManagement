using Microsoft.Extensions.DependencyInjection;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Auth;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Shifts;

namespace ShiftFlow.Application;

/// <summary>
/// Composition root hook for the Application layer. Use-case services are
/// registered here as later phases add them (CLAUDE.md §10).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AuthService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ExpertService>();
        services.AddScoped<ExpertProjectService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<OpenShiftService>();
        services.AddScoped<ApplicationService>();

        return services;
    }
}
