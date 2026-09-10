using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Application.Features.Experts.Dtos;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Employer-only project management. Every action is scoped to the caller's own
/// projects inside the service; another employer's project responds as 404, not
/// 403 (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/projects")]
[Authorize(Roles = RoleNames.Employer)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ProjectsController : ControllerBase
{
    private readonly ProjectService _projects;
    private readonly ExpertProjectService _assignments;

    public ProjectsController(ProjectService projects, ExpertProjectService assignments)
    {
        _projects = projects;
        _assignments = assignments;
    }

    /// <summary>Creates a project owned by the calling employer.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProjectResponse>> Create(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _projects.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, project);
    }

    /// <summary>Lists the calling employer's projects.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _projects.ListAsync(cancellationToken));

    /// <summary>Gets one of the calling employer's projects by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _projects.GetAsync(id, cancellationToken));

    /// <summary>Renames a project and/or toggles its active flag.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProjectResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProjectResponse>> Update(
        int id,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _projects.UpdateAsync(id, request, cancellationToken));

    /// <summary>
    /// Deletes a project that has no shifts. A project with shifts is refused
    /// with 409 — deactivate it with <c>PUT</c> instead.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await _projects.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Lists the experts assigned to one of the calling employer's projects.</summary>
    [HttpGet("{id:int}/experts")]
    [ProducesResponseType(typeof(IReadOnlyList<ExpertResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ExpertResponse>>> ListExperts(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _assignments.ListExpertsForProjectAsync(id, cancellationToken));
}
