using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Applications.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// CallAgent-only: apply to a shift. The shift comes from the route and the CallAgent
/// from the token — there is no request body. Every apply rule is enforced in
/// <see cref="ApplicationService"/>; a shift on a project the caller is not
/// assigned to responds as 404 (not 403), so membership is not probeable
/// (CLAUDE.md §7). The other rule violations respond as 409.
/// </summary>
[ApiController]
[Route("api/shifts/{shiftId:int}/applications")]
[Authorize(Roles = RoleNames.CallAgent)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ShiftApplicationsController : ControllerBase
{
    private readonly ApplicationService _applications;

    public ShiftApplicationsController(ApplicationService applications)
    {
        _applications = applications;
    }

    /// <summary>
    /// Applies the calling CallAgent to the shift. Responds 404 when the shift is
    /// not on one of the caller's assigned projects, and 409 when the shift is
    /// not Open, the caller has already applied, their availability does not
    /// cover the whole shift, or the shift overlaps one they are already
    /// approved for.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponse>> Apply(int shiftId, CancellationToken cancellationToken)
    {
        var application = await _applications.ApplyAsync(shiftId, cancellationToken);

        // The application is now visible in GET /api/applications; there is no
        // single-application endpoint in this design, so that collection is the
        // Location.
        return Created("/api/applications", application);
    }
}
