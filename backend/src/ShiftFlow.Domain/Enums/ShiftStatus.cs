namespace ShiftFlow.Domain.Enums;

/// <summary>
/// Lifecycle of a shift. Stored as NVARCHAR(16) with a CHECK constraint,
/// default <see cref="Open"/> (docs/01-erd-and-schema.md §3-7).
/// </summary>
public enum ShiftStatus
{
    Open,
    Closed,
}
