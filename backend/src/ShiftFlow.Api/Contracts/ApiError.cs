using System.Text.Json.Serialization;

namespace ShiftFlow.Api.Contracts;

/// <summary>
/// The single JSON error shape for the whole API. Every failure — a thrown
/// domain/application exception caught by <c>ExceptionHandlingMiddleware</c> or a
/// model-binding failure rewritten by <c>ValidationExtensions</c> — is returned
/// as this object. <see cref="Details"/> is populated only for validation
/// errors.
/// </summary>
public sealed record ApiError
{
    public required int Status { get; init; }

    /// <summary>Stable machine-readable code, e.g. <c>BusinessRuleViolation</c>.</summary>
    public required string Error { get; init; }

    public required string Message { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? Details { get; init; }
}
