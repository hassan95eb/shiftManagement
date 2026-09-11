namespace ShiftFlow.Domain.Entities;

/// <summary>
/// Supervisor profile — owns projects and the shifts under them
/// (docs/01-erd-and-schema.md §3-2).
/// </summary>
public class Supervisor
{
    public int Id { get; set; }

    // FK -> Users, unique (1:1). CASCADE on delete.
    public int UserId { get; set; }

    public string Name { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public User User { get; set; } = null!;

    public ICollection<Project> Projects { get; } = new List<Project>();
}
