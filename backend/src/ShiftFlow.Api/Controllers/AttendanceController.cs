using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Attendance;
using ShiftFlow.Application.Features.Attendance.Dtos;

namespace ShiftFlow.Api.Controllers;

[ApiController]
[Route("api/attendance")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class AttendanceController : ControllerBase
{
    private readonly AttendanceService _attendance;

    public AttendanceController(AttendanceService attendance)
    {
        _attendance = attendance;
    }

    [HttpPost("heartbeat")]
    [Authorize(Roles = RoleNames.CallAgent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Heartbeat(CancellationToken cancellationToken)
    {
        await _attendance.HeartbeatAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("logout")]
    [Authorize(Roles = RoleNames.CallAgent)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _attendance.LogoutAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("active")]
    [Authorize(Roles = RoleNames.Supervisor + "," + RoleNames.Manager)]
    [ProducesResponseType(typeof(ActiveAttendanceResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ActiveAttendanceResponse>> Active(CancellationToken cancellationToken) =>
        Ok(await _attendance.GetActiveAsync(cancellationToken));
}
