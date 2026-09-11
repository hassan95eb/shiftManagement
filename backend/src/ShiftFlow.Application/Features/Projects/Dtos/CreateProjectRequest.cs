using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Projects.Dtos;

/// <summary>Body of <c>POST /api/projects</c>. The owning supervisor comes from the token, never the body.</summary>
public sealed class CreateProjectRequest
{
    [Required]
    [MaxLength(128)]
    public string Name { get; init; } = string.Empty;
}
