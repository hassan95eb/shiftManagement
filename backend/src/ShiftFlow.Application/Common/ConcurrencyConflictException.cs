namespace ShiftFlow.Application.Common;

/// <summary>
/// Raised when a write loses an optimistic-concurrency check: the row's
/// <c>RowVersion</c> no longer matches the one the caller last read, so someone
/// else changed it in between (docs/01-erd-and-schema.md §3-7). Distinct from
/// <see cref="ShiftFlow.Domain.Exceptions.BusinessRuleViolationException"/> —
/// both map to HTTP 409, but this one means "reload and retry", not "the request
/// was invalid". Mapped to HTTP 409.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
