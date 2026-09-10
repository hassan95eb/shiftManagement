namespace ShiftFlow.Application.Features.Recommendations.Dtos;

/// <summary>
/// One ranked applicant of a shift, as written by the Python recommender
/// (docs/01-erd-and-schema.md §3-10). <see cref="Score"/> is 0–100 with two
/// decimals; <see cref="Reason"/> is the traceable component breakdown. The list
/// is returned already ordered — score descending, then the CLAUDE.md §5
/// tie-break — so the position in the response <i>is</i> the rank.
/// </summary>
public sealed record RecommendationResponse(
    int ExpertId,
    decimal Score,
    string Reason,
    DateTime ComputedAtUtc);
