namespace ShiftFlow.Domain.Entities;

/// <summary>
/// A monthly performance rating for a CallAgent. Seed-only — no API or UI writes
/// it; a CallAgent with no row scores as 3.0 in the recommendation
/// (docs/01-erd-and-schema.md §3-9, CLAUDE.md §5). Unique on (CallAgentId, Period).
/// </summary>
public class Rating
{
    public int Id { get; set; }

    // FK -> CallAgents. CASCADE on delete.
    public int CallAgentId { get; set; }

    // Month key in 'yyyy-MM' format, e.g. "2026-08" (CHAR(7)).
    public string Period { get; set; } = null!;

    // DECIMAL(2,1), CHECK (Score BETWEEN 1.0 AND 5.0).
    public decimal Score { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public CallAgent CallAgent { get; set; } = null!;
}
