namespace ShiftFlow.Application.Common;

/// <summary>
/// Raised when the caller is authenticated but not allowed to see or act on the
/// target resource — the right role pointed at someone else's project, shift or
/// application (CLAUDE.md §7). Mapped to HTTP 403.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message = "You do not have access to this resource.")
        : base(message)
    {
    }
}
