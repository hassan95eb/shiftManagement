namespace ShiftFlow.Domain.Entities;

/// <summary>
/// The recommendation score and human-readable reason for one (shift, CallAgent)
/// pair, written only by the Python script via MERGE. Unique on
/// (ShiftId, CallAgentId), so re-running the script updates in place rather than
/// inserting duplicates (docs/01-erd-and-schema.md §3-10). Shift side CASCADE,
/// CallAgent side NO ACTION (docs/01 §4).
/// </summary>
public class Recommendation
{
    public int Id { get; set; }

    // FK -> Shifts. CASCADE on delete.
    public int ShiftId { get; set; }

    // FK -> CallAgents. NO ACTION on delete.
    public int CallAgentId { get; set; }

    // DECIMAL(5,2), CHECK (Score BETWEEN 0 AND 100).
    public decimal Score { get; set; }

    // Traceable breakdown, e.g.
    // "Rating 4.7/5 -> 28.2 | Workload 32h -> 24.0 | Availability exact -> 40.0 | Total 92.2".
    public string Reason { get; set; } = null!;

    public DateTime ComputedAtUtc { get; set; }

    // Navigation
    public Shift Shift { get; set; } = null!;

    public CallAgent CallAgent { get; set; } = null!;
}
