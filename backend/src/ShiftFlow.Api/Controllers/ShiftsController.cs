using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Shift management. Create and update are Supervisor-only; reads are also open
/// to a Manager. Every read is scoped inside the service through
/// <c>IAccessScope</c>: a shift on another supervisor's project responds as 404,
/// not 403, for a Supervisor caller (CLAUDE.md §7) — the rule never triggers for
/// a Manager, who sees every shift. An existing shift can only have its schedule
/// corrected, and only while it is Open with no applications — there is
/// deliberately no status write and no delete here (see the phase report).
/// </summary>
[ApiController]
[Route("api/shifts")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ShiftsController : ControllerBase
{
    private const string SupervisorOrManager = $"{RoleNames.Supervisor},{RoleNames.Manager}";

    private readonly ShiftService _shifts;

    public ShiftsController(ShiftService shifts)
    {
        _shifts = shifts;
    }

    /// <summary>Creates an Open shift on one of the calling supervisor's projects.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Supervisor)]
    [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftResponse>> Create(
        [FromBody] CreateShiftRequest request,
        CancellationToken cancellationToken)
    {
        var shift = await _shifts.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = shift.Id }, shift);
    }

    /// <summary>
    /// Lists the caller's shifts, newest start first — every shift, for a
    /// Manager. Optional query filters: <c>projectId</c>, <c>status</c>
    /// (<c>Open</c>/<c>Closed</c>), <c>fromUtc</c>, <c>toUtc</c> (both bound the
    /// start time, inclusive).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = SupervisorOrManager)]
    [ProducesResponseType(typeof(IReadOnlyList<ShiftResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ShiftResponse>>> List(
        [FromQuery] ShiftListFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _shifts.ListAsync(filter, cancellationToken));

    /// <summary>Gets a shift by id, within the caller's scope.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = SupervisorOrManager)]
    [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ShiftResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _shifts.GetAsync(id, cancellationToken));

    /// <summary>
    /// Corrects an Open, unapplied shift's start/end time. Refused with 409 when
    /// the shift is Closed, already has applications, or the supplied
    /// <c>rowVersion</c> is stale.
    /// </summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Supervisor)]
    [ProducesResponseType(typeof(ShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ShiftResponse>> Update(
        int id,
        [FromBody] UpdateShiftRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _shifts.UpdateAsync(id, request, cancellationToken));
}
