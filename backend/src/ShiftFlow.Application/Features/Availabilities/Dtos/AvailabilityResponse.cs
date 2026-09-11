namespace ShiftFlow.Application.Features.Availabilities.Dtos;

/// <summary>
/// One availability window as returned to its owning CallAgent. After a create or
/// update the window returned is the <i>merged</i> one that now hosts the
/// requested interval, which may span more than what the caller sent and may
/// carry a different <see cref="Id"/> than a window the caller was editing.
/// </summary>
public sealed record AvailabilityResponse(
    int Id,
    int CallAgentId,
    DateTime StartUtc,
    DateTime EndUtc,
    DateTime CreatedAtUtc);
