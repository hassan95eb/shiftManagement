namespace ShiftFlow.Domain.Entities;

/// <summary>
/// Join row for the Expert–Project N:N assignment. Composite primary key
/// (ExpertId, ProjectId) blocks duplicate assignment on its own
/// (docs/01-erd-and-schema.md §3-5). Expert side CASCADE, Project side NO ACTION
/// (docs/01 §4).
/// </summary>
public class ExpertProject
{
    // Composite PK + FK -> Experts.
    public int ExpertId { get; set; }

    // Composite PK + FK -> Projects.
    public int ProjectId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    // Navigation
    public Expert Expert { get; set; } = null!;

    public Project Project { get; set; } = null!;
}
