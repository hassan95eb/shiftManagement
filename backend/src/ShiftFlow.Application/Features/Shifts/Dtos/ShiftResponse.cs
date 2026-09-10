namespace ShiftFlow.Application.Features.Shifts.Dtos;

/// <summary>
/// One shift as returned by the API. <see cref="Status"/> is the stored string
/// form (<c>Open</c> / <c>Closed</c>). <see cref="RowVersion"/> is the base64
/// concurrency token; the caller sends it back unchanged in an
/// <see cref="UpdateShiftRequest"/>.
/// </summary>
public sealed record ShiftResponse(
    int Id,
    int ProjectId,
    DateTime StartUtc,
    DateTime EndUtc,
    string Status,
    DateTime CreatedAtUtc,
    string RowVersion);
