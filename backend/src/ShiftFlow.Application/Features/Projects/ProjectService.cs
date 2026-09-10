using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Application.Features.Projects.Validators;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Projects;

/// <summary>
/// Employer-facing project use cases. Every method is scoped to
/// <see cref="ICurrentUser.RequireEmployerId"/>: a project belonging to another
/// employer is treated exactly like a project that does not exist
/// (<see cref="NotFoundException"/>), so the caller cannot probe for other
/// employers' ids (CLAUDE.md §7).
/// </summary>
public sealed class ProjectService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ProjectService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<ProjectResponse> CreateAsync(CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();
        var name = ProjectRequestValidator.ValidateAndNormalize(request);

        await GuardNameIsFreeAsync(employerId, name, exceptProjectId: null, cancellationToken);

        var project = new Project
        {
            EmployerId = employerId,
            Name = name,
            IsActive = true,
            CreatedAtUtc = _clock.UtcNow,
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(project);
    }

    public async Task<IReadOnlyList<ProjectResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();

        return await _db.Projects
            .AsNoTracking()
            .Where(p => p.EmployerId == employerId)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectResponse(p.Id, p.EmployerId, p.Name, p.IsActive, p.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProjectResponse> GetAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await FindOwnedAsync(projectId, cancellationToken);
        return ToResponse(project);
    }

    public async Task<ProjectResponse> UpdateAsync(
        int projectId,
        UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();
        var (name, isActive) = ProjectRequestValidator.ValidateAndNormalize(request);

        var project = await FindOwnedAsync(projectId, cancellationToken);

        await GuardNameIsFreeAsync(employerId, name, exceptProjectId: project.Id, cancellationToken);

        project.Name = name;
        project.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(project);
    }

    /// <summary>
    /// Hard-deletes an empty project. The schema cascades Project → Shifts →
    /// ShiftApplications, so a project that has any shift is refused with a
    /// 409 pointing the caller at deactivation (IsActive = false) instead —
    /// deleting it would erase approved work history. Expert-project
    /// assignments do not block the delete; the Project → ExpertProjects FK is
    /// NO ACTION, so those rows are removed here explicitly.
    /// </summary>
    public async Task DeleteAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await FindOwnedAsync(projectId, cancellationToken);

        var hasShifts = await _db.Shifts.AnyAsync(s => s.ProjectId == project.Id, cancellationToken);
        if (hasShifts)
        {
            throw new BusinessRuleViolationException(
                "This project has shifts and cannot be deleted. "
                + "Deactivate it instead by setting isActive to false.");
        }

        var assignments = await _db.ExpertProjects
            .Where(ep => ep.ProjectId == project.Id)
            .ToListAsync(cancellationToken);

        _db.ExpertProjects.RemoveRange(assignments);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a project by id <em>and</em> owner in one query. A miss — unknown
    /// id or another employer's project — is a <see cref="NotFoundException"/>.
    /// </summary>
    private async Task<Project> FindOwnedAsync(int projectId, CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();

        return await _db.Projects
                   .FirstOrDefaultAsync(p => p.Id == projectId && p.EmployerId == employerId, cancellationToken)
               ?? throw new NotFoundException("Project not found.");
    }

    private async Task GuardNameIsFreeAsync(
        int employerId,
        string name,
        int? exceptProjectId,
        CancellationToken cancellationToken)
    {
        var taken = await _db.Projects.AnyAsync(
            p => p.EmployerId == employerId
                 && p.Name == name
                 && (exceptProjectId == null || p.Id != exceptProjectId),
            cancellationToken);

        if (taken)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["name"] = ["You already have a project with this name."],
            });
        }
    }

    private static ProjectResponse ToResponse(Project p) =>
        new(p.Id, p.EmployerId, p.Name, p.IsActive, p.CreatedAtUtc);
}
