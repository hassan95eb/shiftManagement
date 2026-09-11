namespace ShiftFlow.Application.Features.Shifts.Dtos;

/// <summary>
/// One shift as returned by the API. <see cref="Status"/> is the stored string
/// form (<c>Open</c> / <c>Assigned</c> / <c>Released</c> / <c>Closed</c>).
/// <see cref="AssignedCallAgentId"/> is set only through the assignment endpoint
/// (V3) or a Cover application approval (V6) — never through a plain apply/approve.
/// <see cref="RowVersion"/> is the base64 concurrency token; the caller sends it
/// back unchanged in an <see cref="UpdateShiftRequest"/> or an
/// <see cref="AssignShiftRequest"/>.
/// </summary>
public sealed record ShiftResponse(
    int Id,
    int ProjectId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Status,
    int? AssignedCallAgentId,
    DateTime CreatedAtUtc,
    string RowVersion);
