using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Application.Features.Projects.Validators;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Projects;

/// <summary>
/// Project use cases. Creation, renaming/deactivation, deletion and supervisor
/// reassignment are Manager-only; a Supervisor keeps read access to their own
/// projects only. Every read goes through <see cref="IAccessScope"/>, so a
/// Supervisor reading another supervisor's project sees a
/// <see cref="NotFoundException"/> exactly like an unknown id (CLAUDE.md §7),
/// while a Manager sees every project.
/// </summary>
public sealed class ProjectService
{
    private readonly IAppDbContext _db;
    private readonly IAccessScope _accessScope;
    private readonly IClock _clock;

    public ProjectService(IAppDbContext db, IAccessScope accessScope, IClock clock)
    {
        _db = db;
        _accessScope = accessScope;
        _clock = clock;
    }

    /// <summary>
    /// Creates a project owned by <see cref="CreateProjectRequest.SupervisorId"/>.
    /// Manager-only: the owner is named in the body, never inferred from the
    /// caller. An unknown supervisor id is a 400 field error, not a 404 — a
    /// Manager sees every supervisor, so there is nothing to hide.
    /// </summary>
    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var name = ProjectRequestValidator.ValidateAndNormalize(request);

        await GuardSupervisorExistsAsync(request.SupervisorId, cancellationToken);
        await GuardNameIsFreeAsync(request.SupervisorId, name, exceptProjectId: null, cancellationToken);

        var project = new Project
        {
            SupervisorId = request.SupervisorId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(project);
    }

    /// <summary>The caller's projects — every project, for a Manager.</summary>
    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var query = _accessScope.RestrictToOwnSupervisor(_db.Projects.AsNoTracking(), p => p.SupervisorId);

        return await query
            .OrderBy(p => p.Name)
            .Select(p => new ProjectResponse(p.Id, p.SupervisorId, p.Name, p.IsActive, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Gets a project by id, within the caller's scope.</summary>
    public async Task<ProjectResponse> GetAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await FindInScopeAsync(projectId, cancellationToken);
        return ToResponse(project);
    }

    /// <summary>Renames a project and/or toggles its active flag. Manager-only.</summary>
    public async Task<ProjectResponse> UpdateAsync(
        int projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var (name, isActive) = ProjectRequestValidator.ValidateAndNormalize(request);

        var project = await FindInScopeAsync(projectId, cancellationToken);

        await GuardNameIsFreeAsync(project.SupervisorId, name, exceptProjectId: project.Id, cancellationToken);

        project.Name = name;
        project.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(project);
    }

    /// <summary>
    /// Moves the project to a different supervisor. Manager-only. A no-op 200
    /// when the target is the project's current supervisor; otherwise the target
    /// must exist (400 if not) and must not already have a project with this
    /// name (409 if so) — <c>UQ_Projects_Supervisor_Name</c> is per supervisor,
    /// so this re-runs the same name guard against the target before saving,
    /// instead of letting the unique index turn the conflict into a 500.
    /// </summary>
    public async Task<ProjectResponse> ReassignSupervisorAsync(
        int projectId,
        ReassignProjectSupervisorRequest request,
        CancellationToken cancellationToken)
    {
        var project = await FindInScopeAsync(projectId, cancellationToken);

        if (project.SupervisorId == request.SupervisorId)
        {
            return ToResponse(project);
        }

        await GuardSupervisorExistsAsync(request.SupervisorId, cancellationToken);

        var taken = await _db.Projects.AnyAsync(
            p => p.SupervisorId == request.SupervisorId && p.Name == project.Name && p.Id != project.Id,
            cancellationToken);
        if (taken)
        {
            throw new BusinessRuleViolationException(
                $"Supervisor {request.SupervisorId} already has a project named \"{project.Name}\".");
        }

        project.SupervisorId = request.SupervisorId;
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(project);
    }

    /// <summary>
    /// Hard-deletes an empty project. Manager-only. The schema cascades Project →
    /// Shifts → ShiftApplications, so a project that has any shift is refused with a
    /// 409 pointing the caller at deactivation (IsActive = false) instead —
    /// deleting it would erase approved work history. CallAgent-project
    /// assignments do not block the delete; the Project → CallAgentProjects FK is
    /// NO ACTION, so those rows are removed here explicitly.
    /// </summary>
    public async Task DeleteAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await FindInScopeAsync(projectId, cancellationToken);

        var hasShifts = await _db.Shifts.AnyAsync(s => s.ProjectId == project.Id, cancellationToken);
        if (hasShifts)
        {
            throw new BusinessRuleViolationException(
                "This project has shifts and cannot be deleted. "
                + "Deactivate it instead by setting isActive to false.");
        }

        var assignments = await _db.CallAgentProjects
            .Where(ep => ep.ProjectId == project.Id)
            .ToListAsync(cancellationToken);

        _db.CallAgentProjects.RemoveRange(assignments);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a project by id, within the caller's scope: every project for a
    /// Manager, only the caller's own for a Supervisor. A miss — unknown id or
    /// another supervisor's project — is a <see cref="NotFoundException"/>.
    /// </summary>
    private async Task<Project> FindInScopeAsync(int projectId, CancellationToken cancellationToken)
    {
        return await _accessScope
                   .RestrictToOwnSupervisor(_db.Projects.Where(p => p.Id == projectId), p => p.SupervisorId)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("Project not found.");
    }

    private async Task GuardSupervisorExistsAsync(int supervisorId, CancellationToken cancellationToken)
    {
        var exists = await _db.Supervisors.AnyAsync(s => s.Id == supervisorId, cancellationToken);
        if (!exists)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["supervisorId"] = ["No supervisor exists with this id."],
            });
        }
    }

    private async Task GuardNameIsFreeAsync(
        int supervisorId,
        string name,
        int? exceptProjectId,
        CancellationToken cancellationToken)
    {
        var taken = await _db.Projects.AnyAsync(
            p => p.SupervisorId == supervisorId
                 && p.Name == name
                 && (exceptProjectId == null || p.Id != exceptProjectId),
            cancellationToken);

        if (taken)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = [$"Supervisor {supervisorId} already has a project with this name."],
            });
        }
    }

    private static ProjectResponse ToResponse(Project p) =>
        new(p.Id, p.SupervisorId, p.Name, p.IsActive, p.CreatedAtUtc);
}
