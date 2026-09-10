namespace ShiftFlow.Application.Features.Projects.Dtos;

/// <summary>A project as returned to its owning employer.</summary>
public sealed record ProjectResponse(
    int Id,
    int EmployerId,
    string Name,
    bool IsActive,
    DateTime CreatedAtUtc);
