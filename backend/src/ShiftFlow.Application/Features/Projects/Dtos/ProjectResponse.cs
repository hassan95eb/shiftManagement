namespace ShiftFlow.Application.Features.Projects.Dtos;

/// <summary>A project as returned to its owning supervisor.</summary>
public sealed record ProjectResponse(
    int Id,
    int SupervisorId,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc);
