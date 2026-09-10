using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Shifts.Dtos;

/// <summary>
/// Optional filters for the employer shift list (<c>GET /api/shifts</c>), bound
/// from the query string. Every field is nullable; a null field is simply not
/// applied. <see cref="FromUtc"/> / <see cref="ToUtc"/> both bound
/// <see cref="ShiftFlow.Domain.Entities.Shift.StartUtc"/> inclusively.
/// </summary>
public sealed class ShiftListFilter
{
    public int? ProjectId { get; init; }

    public ShiftStatus? Status { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }
}
