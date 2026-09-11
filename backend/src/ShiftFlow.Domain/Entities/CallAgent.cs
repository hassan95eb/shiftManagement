namespace ShiftFlow.Domain.Entities;

/// <summary>
/// Hourly specialist — declares availability and applies for open shifts
/// (docs/01-erd-and-schema.md §3-3). Every FK pointing back here is NO ACTION
/// on delete (docs/01 §4).
/// </summary>
public class CallAgent
{
    public int Id { get; set; }

    // FK -> Users, unique (1:1). CASCADE on delete.
    public int UserId { get; set; }

    public string FullName { get; set; } = null!;

    public bool IsActive { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public User User { get; set; } = null!;

    public ICollection<CallAgentProject> CallAgentProjects { get; } = new List<CallAgentProject>();

    public ICollection<Availability> Availabilities { get; } = new List<Availability>();

    public ICollection<Rating> Ratings { get; } = new List<Rating>();

    public ICollection<ShiftApplication> ShiftApplications { get; } = new List<ShiftApplication>();

    public ICollection<Recommendation> Recommendations { get; } = new List<Recommendation>();

    // Shifts.AssignedCallAgentId. NO ACTION on delete (docs/01 §4).
    public ICollection<Shift> AssignedShifts { get; } = new List<Shift>();
}
