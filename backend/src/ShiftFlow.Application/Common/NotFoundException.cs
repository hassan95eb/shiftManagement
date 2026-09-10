namespace ShiftFlow.Application.Common;

/// <summary>
/// Raised when a resource the caller asked for does not exist — or exists but
/// belongs to someone else. Cross-tenant access deliberately surfaces as this,
/// not as <see cref="ForbiddenAccessException"/>, so a caller cannot tell "not
/// yours" apart from "no such id" and probe for other employers' ids
/// (CLAUDE.md §7). Mapped to HTTP 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message = "The requested resource was not found.")
        : base(message)
    {
    }
}
