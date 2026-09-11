namespace ShiftFlow.Domain.Entities;

/// <summary>
/// Join row for the CallAgent–Project N:N assignment. Composite primary key
/// (CallAgentId, ProjectId) blocks duplicate assignment on its own
/// (docs/01-erd-and-schema.md §3-5). CallAgent side CASCADE, Project side NO ACTION
/// (docs/01 §4).
/// </summary>
public class CallAgentProject
{
    // Composite PK + FK -> CallAgents.
    public int CallAgentId { get; set; }

    // Composite PK + FK -> Projects.
    public int ProjectId { get; set; }

    public DateTime AssignedAtUtc { get; set; }

    // Navigation
    public CallAgent CallAgent { get; set; } = null!;

    public Project Project { get; set; } = null!;
}
