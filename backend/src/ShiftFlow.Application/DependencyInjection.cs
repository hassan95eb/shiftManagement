using Microsoft.Extensions.DependencyInjection;
using ShiftFlow.Application.Features.Auth;

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

        return services;
    }
}
