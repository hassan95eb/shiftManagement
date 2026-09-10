using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Shifts.Dtos;

/// <summary>
/// Body of <c>PUT /api/shifts/{id}</c> — the only mutation an employer may make
/// to an existing shift, and only while it is still <c>Open</c> and no expert
/// has applied to it yet (see the phase report for why time is otherwise frozen
/// and why status is never set here).
/// </summary>
/// <remarks>
/// <see cref="RowVersion"/> is the base64 <c>RowVersion</c> string the caller
/// received from a prior <c>GET</c>. The service replays it as the concurrency
/// token's original value, so an edit built on a stale read is rejected with
/// 409 rather than clobbering a newer version.
/// </remarks>
public sealed class UpdateShiftRequest
{
    [Required]
    public DateTime? StartUtc { get; init; }

    [Required]
    public DateTime? EndUtc { get; init; }

    [Required]
    public string? RowVersion { get; init; }
}
