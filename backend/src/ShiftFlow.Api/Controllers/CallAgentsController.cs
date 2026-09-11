using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Application.Features.CallAgents.Dtos;
using ShiftFlow.Application.Features.Projects.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Supervisor-only CallAgent management and CallAgent ↔ project assignment. CallAgents are a
/// shared pool (no supervisor-ownership column in the schema), so the list and
/// lookup are not supervisor-scoped; the assignment endpoints are, via the
/// project (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/call-agents")]
[Authorize(Roles = RoleNames.Supervisor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class CallAgentsController : ControllerBase
{
    private readonly CallAgentService _callAgents;
    private readonly CallAgentProjectService _assignments;

    public CallAgentsController(CallAgentService callAgents, CallAgentProjectService assignments)
    {
        _callAgents = callAgents;
        _assignments = assignments;
    }

    /// <summary>Registers a new CallAgent: creates the login and the profile in one transaction.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(CallAgentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CallAgentResponse>> Create(
        [FromBody] CreateCallAgentRequest request,
        CancellationToken cancellationToken)
    {
        var callAgent = await _callAgents.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = callAgent.Id }, callAgent);
    }

    /// <summary>Lists all CallAgents.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CallAgentResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CallAgentResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _callAgents.ListAsync(cancellationToken));

    /// <summary>Gets one CallAgent by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CallAgentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CallAgentResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _callAgents.GetAsync(id, cancellationToken));

    /// <summary>
    /// Assigns the CallAgent to one of the calling supervisor's projects. Idempotent
    /// — assigning an already-assigned CallAgent returns 204.
    /// </summary>
    [HttpPost("{id:int}/projects/{projectId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AssignToProject(
        int id,
        int projectId,
        CancellationToken cancellationToken)
    {
        await _assignments.AssignAsync(id, projectId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Removes the CallAgent from one of the calling supervisor's projects. Refused
    /// with 409 when the CallAgent has an approved application for a shift on that
    /// project; pending applications for it are rejected.
    /// </summary>
    [HttpDelete("{id:int}/projects/{projectId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveFromProject(
        int id,
        int projectId,
        CancellationToken cancellationToken)
    {
        await _assignments.UnassignAsync(id, projectId, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists the calling supervisor's projects that this CallAgent is assigned to.</summary>
    [HttpGet("{id:int}/projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> ListProjects(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _assignments.ListProjectsForCallAgentAsync(id, cancellationToken));
}
