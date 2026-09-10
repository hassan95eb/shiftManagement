using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Applications.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Application history, for both roles. An expert gets their own applications; an
/// employer gets the applications on shifts of their own projects. The role
/// split and the ownership scoping live in <see cref="ApplicationService"/>, so
/// this endpoint needs no role restriction beyond authentication — there is no
/// third role and neither branch can see the other's rows (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/applications")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
public sealed class ApplicationsController : ControllerBase
{
    private readonly ApplicationService _applications;

    public ApplicationsController(ApplicationService applications)
    {
        _applications = applications;
    }

    /// <summary>
    /// Lists the caller's applications, newest first. Optional query filters:
    /// <c>shiftId</c> and <c>status</c> (<c>Pending</c> / <c>Approved</c> /
    /// <c>Rejected</c>); each only narrows the set the caller's role already
    /// restricts them to.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApplicationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ApplicationResponse>>> List(
        [FromQuery] ApplicationListFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _applications.ListAsync(filter, cancellationToken));
}
