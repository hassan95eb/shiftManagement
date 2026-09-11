using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Projects.Dtos;

/// <summary>
/// Body of <c>POST /api/projects</c>. Creation is Manager-only, so the owning
/// supervisor cannot be inferred from the caller's own token — it is named
/// explicitly here and validated against <c>Supervisors</c>.
/// </summary>
public sealed class CreateProjectRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    public int SupervisorId { get; init; }
}
