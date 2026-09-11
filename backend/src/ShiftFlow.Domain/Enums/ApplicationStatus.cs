namespace ShiftFlow.Domain.Enums;

/// <summary>
/// State of a CallAgent's application to a shift. Stored as NVARCHAR(16) with a
/// CHECK constraint (docs/01-erd-and-schema.md §3-8).
/// </summary>
public enum ApplicationStatus
{
    Pending,
    Approved,
    Rejected,
}
