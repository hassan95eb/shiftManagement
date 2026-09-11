using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Application.Features.CallAgents.Dtos;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Project management. Create, update, delete and supervisor reassignment are
/// Manager-only; a Supervisor keeps read access to their own projects. Every
/// read is scoped inside the service through <c>IAccessScope</c>: another
/// supervisor's project responds as 404, not 403, for a Supervisor caller — the
/// rule never triggers for a Manager, who sees everything (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ProjectsController : ControllerBase
{
    private const string SupervisorOrManager = $"{RoleNames.Supervisor},{RoleNames.Manager}";

    private readonly ProjectService _projects;
    private readonly CallAgentProjectService _assignments;

    public ProjectsController(ProjectService projects, CallAgentProjectService assignments)
    {
        _projects = projects;
        _assignments = assignments;
    }

    /// <summary>Creates a project for the supervisor named in the body. Manager-only.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Manager)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projects.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>Lists the caller's projects — every project, for a Manager.</summary>
    [HttpGet]
    [Authorize(Roles = SupervisorOrManager)]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _projects.ListAsync(cancellationToken));

    /// <summary>Gets a project by id, within the caller's scope.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = SupervisorOrManager)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _projects.GetAsync(id, cancellationToken));

    /// <summary>Renames a project and/or toggles its active flag. Manager-only.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = RoleNames.Manager)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Update(
        int id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _projects.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Reassigns the project to a different supervisor. Manager-only. Refused
    /// with 409 when the target supervisor already has a project with this name.
    /// </summary>
    [HttpPut("{id:int}/supervisor")]
    [Authorize(Roles = RoleNames.Manager)]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProjectResponse>> ReassignSupervisor(
        int id,
        [FromBody] ReassignProjectSupervisorRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _projects.ReassignSupervisorAsync(id, request, cancellationToken));

    /// <summary>
    /// Deletes a project that has no shifts. Manager-only. A project with shifts
    /// is refused with 409 — deactivate it with <c>PUT</c> instead.
    /// </summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = RoleNames.Manager)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _projects.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists the CallAgents assigned to a project, within the caller's scope.</summary>
    [HttpGet("{id:int}/call-agents")]
    [Authorize(Roles = SupervisorOrManager)]
    [ProducesResponseType(typeof(IReadOnlyList<CallAgentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<CallAgentResponse>>> ListCallAgents(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _assignments.ListCallAgentsForProjectAsync(id, cancellationToken));
}
