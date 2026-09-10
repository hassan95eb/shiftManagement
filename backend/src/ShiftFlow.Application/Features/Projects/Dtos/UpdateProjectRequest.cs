using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Projects.Dtos;

/// <summary>
/// Body of <c>PUT /api/projects/{id}</c>. Setting <see cref="IsActive"/> to
/// <c>false</c> is the supported way to retire a project that already has shifts
/// — a hard delete is refused in that case (see <c>ProjectService.DeleteAsync</c>).
/// <see cref="IsActive"/> is a nullable <c>bool</c> so a request that omits it is
/// rejected rather than silently read as "deactivate".
/// </summary>
public sealed class UpdateProjectRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;

    [Required]
    public bool? IsActive { get; init; }
}
