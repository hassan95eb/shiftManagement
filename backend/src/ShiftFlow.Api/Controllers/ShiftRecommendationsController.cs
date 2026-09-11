using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Recommendations;
using ShiftFlow.Application.Features.Recommendations.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Supervisor-only, read-only: the applicant ranking for one of the caller's own
/// shifts, best score first with the CLAUDE.md §5 tie-break applied. The rows
/// are written by the Python recommender in a later phase; this endpoint never
/// writes. A shift on another supervisor's project — or an unknown id — responds
/// as 404 (CLAUDE.md §7); an empty array means the recommender has not run yet.
/// </summary>
[ApiController]
[Route("api/shifts/{shiftId:int}/recommendations")]
[Authorize(Roles = RoleNames.Supervisor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ShiftRecommendationsController : ControllerBase
{
    private readonly RecommendationService _recommendations;

    public ShiftRecommendationsController(RecommendationService recommendations)
    {
        _recommendations = recommendations;
    }

    /// <summary>Lists the ranked applicants of the shift, highest score first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecommendationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RecommendationResponse>>> List(
        int shiftId,
        CancellationToken cancellationToken) =>
        Ok(await _recommendations.ListForShiftAsync(shiftId, cancellationToken));
}
