namespace ShiftFlow.Domain.Entities;

/// <summary>
/// A body of work owned by an <see cref="Supervisor" />. CallAgents are
/// assigned to it through <see cref="CallAgentProject"/>; shifts are created under
/// it (docs/01-erd-and-schema.md §3-4). Unique on (SupervisorId, Name).
/// </summary>
public class Project
{
    public int Id { get; set; }

    // FK -> Supervisors. CASCADE on delete.
    public int SupervisorId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Supervisor Supervisor { get; set; } = null!;

    public ICollection<CallAgentProject> CallAgentProjects { get; } = new List<CallAgentProject>();

    public ICollection<Shift> Shifts { get; } = new List<Shift>();
}
