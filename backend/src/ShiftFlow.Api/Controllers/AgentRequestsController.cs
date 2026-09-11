using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.AgentRequests;
using ShiftFlow.Application.Features.AgentRequests.Dtos;

namespace ShiftFlow.Api.Controllers;

[ApiController]
[Route("api/agent-requests")]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class AgentRequestsController : ControllerBase
{
    private readonly AgentRequestService _requests;

    public AgentRequestsController(AgentRequestService requests)
    {
        _requests = requests;
    }

    [HttpPost]
    [Authorize(Roles = RoleNames.CallAgent)]
    [ProducesResponseType(typeof(AgentRequestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AgentRequestResponse>> Create(
        [FromBody] CreateAgentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _requests.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("{id:int}/approval")]
    [Authorize(Roles = RoleNames.Supervisor)]
    [ProducesResponseType(typeof(AgentRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AgentRequestResponse>> Approve(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _requests.ApproveAsync(id, cancellationToken));

    [HttpPost("{id:int}/rejection")]
    [Authorize(Roles = RoleNames.Supervisor)]
    [ProducesResponseType(typeof(AgentRequestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AgentRequestResponse>> Reject(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _requests.RejectAsync(id, cancellationToken));
}
