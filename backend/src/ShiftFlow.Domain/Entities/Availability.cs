namespace ShiftFlow.Domain.Entities;

/// <summary>
/// A single date-based window during which a CallAgent is available. Overlapping
/// or adjacent windows are merged into one on insert by the service layer, so
/// the database never holds two touching windows for one CallAgent
/// (docs/01-erd-and-schema.md §3-6). CHECK (EndUtc &gt; StartUtc).
/// </summary>
public class Availability
{
    public int Id { get; set; }

    // FK -> CallAgents. CASCADE on delete.
    public int CallAgentId { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Navigation
    public CallAgent CallAgent { get; set; } = null!;
}
