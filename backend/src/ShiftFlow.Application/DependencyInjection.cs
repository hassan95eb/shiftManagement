using Microsoft.Extensions.DependencyInjection;

namespace ShiftFlow.Application;

/// <summary>
/// Composition root hook for the Application layer. Use-case services and their
/// validators are registered here as later phases add them (CLAUDE.md §10).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
