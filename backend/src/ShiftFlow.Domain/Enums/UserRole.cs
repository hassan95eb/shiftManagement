namespace ShiftFlow.Domain.Enums;

/// <summary>
/// Account type. Stored as NVARCHAR(16) with a CHECK constraint
/// (docs/01-erd-and-schema.md §3-1).
/// </summary>
public enum UserRole
{
    Supervisor,
    CallAgent,
}
