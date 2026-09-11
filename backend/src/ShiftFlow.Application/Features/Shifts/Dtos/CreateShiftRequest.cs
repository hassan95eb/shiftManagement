using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Shifts.Dtos;

/// <summary>
/// Body of <c>POST /api/shifts</c>. The shift is created <see cref="ShiftFlow.Domain.Enums.ShiftStatus.Open"/>;
/// the caller does not choose a status. The project must belong to the calling
/// supervisor — checked against the database, not trusted from the body. Both
/// bounds are nullable so an omitted one is rejected with the uniform 400 shape
/// instead of being read as <c>0001-01-01</c>.
/// </summary>
public sealed class CreateShiftRequest
{
    [Required]
    public int? ProjectId { get; init; }

    [Required]
    public DateTime? StartUtc { get; init; }

    [Required]
    public DateTime? EndUtc { get; init; }
}
