namespace ShiftFlow.Domain.Entities;

/// <summary>
/// A body of work owned by an <see cref="Employer" />. Experts are
/// assigned to it through <see cref="ExpertProject"/>; shifts are created under
/// it (docs/01-erd-and-schema.md §3-4). Unique on (EmployerId, Name).
/// </summary>
public class Project
{
    public int Id { get; set; }

    // FK -> Employers. CASCADE on delete.
    public int EmployerId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public Employer Employer { get; set; } = null!;

    public ICollection<ExpertProject> ExpertProjects { get; } = new List<ExpertProject>();

    public ICollection<Shift> Shifts { get; } = new List<Shift>();
}
