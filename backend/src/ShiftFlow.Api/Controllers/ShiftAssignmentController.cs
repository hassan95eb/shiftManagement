using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Supervisor-only: assign a CallAgent to a shift directly, or remove that
/// assignment — the direct-fill path alongside the existing application-based
/// one (docs/04-v2-prompts.md V3). Neither action touches
/// <c>ShiftApplications</c>. Every read is scoped through
/// <c>ShiftAssignmentService</c>'s <c>IAccessScope</c> use, so a shift on
/// another supervisor's project responds as 404 (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/shifts/{shiftId:int}/assignment")]
[Authorize(Roles = RoleNames.Supervisor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ShiftAssignmentController : ControllerBase
{
    private readonly ShiftAssignmentService _assignments;

    public ShiftAssignmentController(ShiftAssignmentService assignments)
    {
        _assignments = assignments;
    }

    /// <summary>
    /// Directly assigns a CallAgent to the shift: <c>Open</c> or <c>Released</c> →
    /// <c>Assigned</c>. 409 when the shift is not in one of those statuses, the
    /// CallAgent is not a member of the shift's project, the CallAgent already
    /// has an overlapping commitment, or the supplied <c>rowVersion</c> is stale.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftResponse>> Assign(
        int shiftId,
        [FromBody] AssignShiftRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _assignments.AssignAsync(shiftId, request, cancellationToken));

    /// <summary>
    /// Removes the shift's assignment: <c>Assigned</c> → <c>Open</c>. 409 when the
    /// shift is not currently <c>Assigned</c>.
    /// </summary>
    [HttpDelete]
    [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftResponse>> Unassign(int shiftId, CancellationToken cancellationToken) =>
        Ok(await _assignments.UnassignAsync(shiftId, cancellationToken));
}
