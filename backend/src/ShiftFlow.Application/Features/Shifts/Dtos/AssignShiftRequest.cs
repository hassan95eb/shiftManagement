using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Shifts.Dtos;

/// <summary>
/// Body of <c>POST /api/shifts/{id}/assignment</c> — a supervisor directly
/// assigning a CallAgent to their own <c>Open</c> or <c>Released</c> shift,
/// without going through <c>ShiftApplications</c> (docs/01-erd-and-schema.md §7).
/// </summary>
/// <remarks>
/// <see cref="RowVersion"/> is the base64 <c>RowVersion</c> string the caller
/// received from a prior <c>GET</c>, replayed as the concurrency token's
/// original value exactly like <see cref="UpdateShiftRequest.RowVersion"/> — a
/// concurrent assignment built on a stale read is rejected with 409 instead of
/// silently overwriting a newer version.
/// </remarks>
public sealed class AssignShiftRequest
{
    [Required]
    public int? CallAgentId { get; init; }

    [Required]
    public string? RowVersion { get; init; }
}
