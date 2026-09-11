using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Application.Features.Experts.Dtos;
using ShiftFlow.Application.Features.Projects.Dtos;

namespace ShiftFlow.Api.Controllers;

/// <summary>
/// Employer-only expert management and expert ↔ project assignment. Experts are a
/// shared pool (no employer-ownership column in the schema), so the list and
/// lookup are not employer-scoped; the assignment endpoints are, via the
/// project (CLAUDE.md §7).
/// </summary>
[ApiController]
[Route("api/experts")]
[Authorize(Roles = RoleNames.Employer)]
[Produces("application/json")]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
public sealed class ExpertsController : ControllerBase
{
    private readonly ExpertService _experts;
    private readonly ExpertProjectService _assignments;

    public ExpertsController(ExpertService experts, ExpertProjectService assignments)
    {
        _experts = experts;
        _assignments = assignments;
    }

    /// <summary>Registers a new expert: creates the login and the profile in one transaction.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExpertResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExpertResponse>> Create(
        [FromBody] CreateExpertRequest request,
        CancellationToken cancellationToken)
    {
        var expert = await _experts.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = expert.Id }, expert);
    }

    /// <summary>Lists all experts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExpertResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExpertResponse>>> List(CancellationToken cancellationToken) =>
        Ok(await _experts.ListAsync(cancellationToken));

    /// <summary>Gets one expert by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ExpertResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExpertResponse>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _experts.GetAsync(id, cancellationToken));

    /// <summary>
    /// Assigns the expert to one of the calling employer's projects. Idempotent
    /// — assigning an already-assigned expert returns 204.
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
    /// Removes the expert from one of the calling employer's projects. Refused
    /// with 409 when the expert has an approved application for a shift on that
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

    /// <summary>Lists the calling employer's projects that this expert is assigned to.</summary>
    [HttpGet("{id:int}/projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ProjectResponse>>> ListProjects(
        int id,
        CancellationToken cancellationToken) =>
        Ok(await _assignments.ListProjectsForExpertAsync(id, cancellationToken));
}
