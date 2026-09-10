using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Applications.Dtos;

/// <summary>
/// Optional filters for <c>GET /api/applications</c>, bound from the query
/// string. Every field is nullable and a null field is simply not applied. Both
/// filters narrow whichever set the caller's role already restricts them to —
/// an expert's own applications, or the applications on an employer's own shifts
/// — so neither can widen visibility (CLAUDE.md §7).
/// </summary>
public sealed class ApplicationListFilter
{
    /// <summary>Restrict to applications for this shift.</summary>
    public int? ShiftId { get; init; }

    /// <summary>Restrict to applications in this state.</summary>
    public ApplicationStatus? Status { get; init; }
}
