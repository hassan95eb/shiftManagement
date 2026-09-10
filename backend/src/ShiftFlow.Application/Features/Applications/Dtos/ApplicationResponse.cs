namespace ShiftFlow.Application.Features.Applications.Dtos;

/// <summary>
/// One shift application as returned by the API. The same shape serves the
/// expert's own history and the employer's per-shift list. <see cref="Status"/>
/// is the stored string form (<c>Pending</c> / <c>Approved</c> /
/// <c>Rejected</c>); the three <c>Decided*</c> fields are the decision audit
/// trail and stay null until the approval flow runs (docs/01-erd-and-schema.md
/// §3-8).
/// </summary>
public sealed record ApplicationResponse(
    int Id,
    int ShiftId,
    int ExpertId,
    string Status,
    DateTime AppliedAtUtc,
    int? DecidedByUserId,
    DateTime? DecidedAtUtc,
    string? DecisionNote);
