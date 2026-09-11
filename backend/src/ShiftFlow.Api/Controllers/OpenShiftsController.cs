using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// CallAgent-only read of the shifts the caller can apply to: Open shifts on
/// projects the caller is assigned to. Anything else — a closed shift, a shift
/// on a project the caller is not assigned to, an unknown id — responds as 404,
/// so project membership is not probeable (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/shifts/open")]
[Authorize(Roles = RoleNames.CallAgent)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class OpenShiftsController : ControllerBase
{
    private readonly OpenShiftService _openShifts;

    public OpenShiftsController(OpenShiftService openShifts)
    {
        _openShifts = openShifts;
    }

    /// <summary>
    /// Lists Open shifts on the calling CallAgent's assigned projects, earliest
    /// start first. Optional <c>projectId</c> narrows to one project; a project
    /// the caller is not assigned to responds as 404.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ShiftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ShiftResponse>>> List(
        [FromQuery] int? projectId,
        CancellationToken cancellationToken) =>
        Ok(await _openShifts.ListAsync(projectId, cancellationToken));

    /// <summary>Gets one Open shift on one of the calling CallAgent's assigned projects by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _openShifts.GetAsync(id, cancellationToken));
}
