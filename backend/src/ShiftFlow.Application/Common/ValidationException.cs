namespace ShiftFlow.Application.Common;

/// <summary>
/// Raised when input fails validation inside a use-case service (as opposed to
/// controller model binding, which the Api turns into the same error shape).
/// Mapped to HTTP 400.
/// </summary>
public sealed class ValidationException : Exception
{
    private static readonly IReadOnlyDictionary<string, string[]> None =
        new Dictionary<string, string[]>();

    public ValidationException(string message)
        : base(message)
    {
        Errors = None;
    }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    /// <summary>Field name → messages. Empty when the failure is not field-specific.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }
}
