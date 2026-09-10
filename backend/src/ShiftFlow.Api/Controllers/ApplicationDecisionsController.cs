using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Applications.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Employer-only: decide one pending application. <c>approval</c> runs the full
/// CLAUDE.md §5 transaction — approve this one, close the shift, reject the
/// shift's other pending applications — as a unit; <c>rejection</c> touches only
/// the one row and leaves the shift Open for the rest. An application on another
/// employer's project responds as 404, not 403 (CLAUDE.md §7); a shift that is
/// no longer Open, or an application already decided, responds as 409.
/// </summary>
[ApiController]
[Route("api/applications/{applicationId:int}")]
[Authorize(Roles = RoleNames.Employer)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
public sealed class ApplicationDecisionsController : ControllerBase
{
    private readonly ApprovalService _approval;

    public ApplicationDecisionsController(ApprovalService approval)
    {
        _approval = approval;
    }

    /// <summary>
    /// Approves the application, closes its shift, and rejects the shift's other
    /// pending applications (each stamped with a decision note and the deciding
    /// user). All of it commits together or not at all.
    /// </summary>
    [HttpPost("approval")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationResponse>> Approve(
        int applicationId,
        CancellationToken cancellationToken) =>
        Ok(await _approval.ApproveAsync(applicationId, cancellationToken));

    /// <summary>Rejects this one application; the shift stays Open for the others.</summary>
    [HttpPost("rejection")]
    [ProducesResponseType(typeof(ApplicationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApplicationResponse>> Reject(
        int applicationId,
        CancellationToken cancellationToken) =>
        Ok(await _approval.RejectAsync(applicationId, cancellationToken));
}
