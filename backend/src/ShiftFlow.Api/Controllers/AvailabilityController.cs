using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.Availabilities.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Expert-only management of the caller's own availability windows. Every action
/// is scoped to the expert from the token inside the service; another expert's
/// window responds as 404, not 403 (CLAUDE.md §7). There is deliberately no
/// employer-facing read here — the endpoint list for this phase is create, list,
/// update, delete only.
/// </summary>
[ApiController]
[Route("api/availability")]
[Authorize(Roles = RoleNames.Expert)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class AvailabilityController : ControllerBase
{
    private readonly AvailabilityService _availability;

    public AvailabilityController(AvailabilityService availability)
    {
        _availability = availability;
    }

    /// <summary>
    /// Adds a window for the calling expert, merging it into any existing window
    /// it overlaps or touches. Returns the merged window that now spans the
    /// requested interval.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AvailabilityResponse>> Create(
        [FromBody] AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var window = await _availability.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = window.Id }, window);
    }

    /// <summary>Lists the calling expert's windows, earliest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilityResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AvailabilityResponse>>> List(
        CancellationToken cancellationToken) =>
        Ok(await _availability.ListAsync(cancellationToken));

    /// <summary>Gets one of the calling expert's windows by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AvailabilityResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _availability.GetAsync(id, cancellationToken));

    /// <summary>
    /// Moves or resizes a window and re-merges. Refused with 409 when the new
    /// shape would leave one of the expert's approved shifts uncovered.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AvailabilityResponse>> Update(
        int id,
        [FromBody] AvailabilityRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _availability.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Deletes a window. Refused with 409 when it is the window covering one of
    /// the expert's approved shifts.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _availability.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
