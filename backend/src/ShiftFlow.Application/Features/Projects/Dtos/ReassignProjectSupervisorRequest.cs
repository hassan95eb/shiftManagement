namespace ShiftFlow.Application.Features.Projects.Dtos;

/// <summary>Body of <c>PUT /api/projects/{id}/supervisor</c> — Manager-only project reassignment.</summary>
public sealed class ReassignProjectSupervisorRequest
{
    public int SupervisorId { get; init; }
}
