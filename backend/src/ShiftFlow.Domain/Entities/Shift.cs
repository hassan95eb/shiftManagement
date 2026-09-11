using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Domain.Entities;

/// <summary>
/// An open slot on a project that one CallAgent can be approved for. Both bounds
/// are full <c>DATETIME2</c> values, so an overnight shift (22:00–02:00) needs
/// no extra flag (docs/01-erd-and-schema.md §2, §3-7).
/// </summary>
public class Shift
{
    public int Id { get; set; }

    // FK -> Projects. CASCADE on delete.
    public int ProjectId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    // Default 'Open'. CHECK (Status IN ('Open','Assigned','Released','Closed')).
    public ShiftStatus Status { get; set; }

    // FK -> CallAgents, nullable, NO ACTION. Set by the assignment endpoint (V3),
    // both for a direct fill and for a Cover approval (V6) — never through
    // ShiftApplications (docs/01 §3-7).
    public int? AssignedCallAgentId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Optimistic-concurrency token (ROWVERSION). A second concurrent writer
    // gets DbUpdateConcurrencyException -> 409 (docs/01 §3-7).
    public byte[] RowVersion { get; set; } = null!;

    // Navigation
    public Project Project { get; set; } = null!;

    public CallAgent? AssignedCallAgent { get; set; }

    public ICollection<ShiftApplication> ShiftApplications { get; } = new List<ShiftApplication>();

    public ICollection<Recommendation> Recommendations { get; } = new List<Recommendation>();
}
