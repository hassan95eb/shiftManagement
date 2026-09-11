using Microsoft.Extensions.DependencyInjection;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.AgentRequests;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Attendance;
using ShiftFlow.Application.Features.Auth;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Recommendations;
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
        services.AddScoped<IAccessScope, AccessScope>();

        services.AddScoped<AuthService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<IAttendanceRecorder>(sp => sp.GetRequiredService<AttendanceService>());
        services.AddScoped<ProjectService>();
        services.AddScoped<CallAgentService>();
        services.AddScoped<CallAgentProjectService>();
        services.AddScoped<AvailabilityService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<ShiftAssignmentService>();
        services.AddScoped<OpenShiftService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<ApprovalService>();
        services.AddScoped<AgentRequestService>();
        services.AddScoped<RecommendationService>();

        return services;
    }
}
