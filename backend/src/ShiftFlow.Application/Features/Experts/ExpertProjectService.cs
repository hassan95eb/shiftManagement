using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts.Dtos;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Experts;

/// <summary>
/// Assigns experts to, and removes them from, an employer's projects. The routes
/// start with the expert (<c>/api/experts/{id}/projects/{projectId}</c>) but the
/// authorization check that matters is on the <em>project</em>: it must belong
/// to the calling employer, verified from the database, before anything is
/// written. Checking only that the expert exists would let an employer assign
/// someone to another employer's project (CLAUDE.md §7).
/// </summary>
public sealed class ExpertProjectService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ExpertProjectService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Assigns the expert to the project. Idempotent: if the assignment already
    /// exists the call succeeds without a second write, so a repeated request
    /// never trips the composite primary key.
    /// </summary>
    public async Task AssignAsync(int expertId, int projectId, CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(projectId, cancellationToken);
        await GuardExpertExistsAsync(expertId, cancellationToken);

        var alreadyAssigned = await _db.ExpertProjects.AnyAsync(
            ep => ep.ExpertId == expertId && ep.ProjectId == project.Id, cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        _db.ExpertProjects.Add(new ExpertProject
        {
            ExpertId = expertId,
            ProjectId = project.Id,
            AssignedAtUtc = _clock.UtcNow,
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race with a concurrent identical assign: if the row the
            // other writer inserted is now there, the request is still
            // satisfied; otherwise the failure was something else, so rethrow.
            _db.ExpertProjects.Remove(_db.ExpertProjects.Local.First(
                ep => ep.ExpertId == expertId && ep.ProjectId == project.Id));

            if (!await IsAssignedAsync(expertId, project.Id, cancellationToken))
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Removes the assignment. Blocked with a 409 when the expert has an
    /// <see cref="ApplicationStatus.Approved"/> application for a shift on this
    /// project — that would contradict work already approved. Any
    /// <see cref="ApplicationStatus.Pending"/> applications for the project's
    /// shifts are rejected with a decision note in the same transaction.
    /// </summary>
    public async Task UnassignAsync(int expertId, int projectId, CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(projectId, cancellationToken);

        var assignment = await _db.ExpertProjects.FirstOrDefaultAsync(
                             ep => ep.ExpertId == expertId && ep.ProjectId == project.Id, cancellationToken)
                         ?? throw new NotFoundException("This expert is not assigned to the project.");

        var hasApproved = await _db.ShiftApplications.AnyAsync(
            a => a.ExpertId == expertId
                 && a.Status == ApplicationStatus.Approved
                 && a.Shift.ProjectId == project.Id,
            cancellationToken);
        if (hasApproved)
        {
            throw new BusinessRuleViolationException(
                "This expert has an approved application for a shift on this project; "
                + "the assignment cannot be removed.");
        }

        var pending = await _db.ShiftApplications
            .Where(a => a.ExpertId == expertId
                        && a.Status == ApplicationStatus.Pending
                        && a.Shift.ProjectId == project.Id)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var application in pending)
        {
            application.Status = ApplicationStatus.Rejected;
            application.DecidedByUserId = _currentUser.UserId;
            application.DecidedAtUtc = now;
            application.DecisionNote = "Expert unassigned from the project.";
        }

        _db.ExpertProjects.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The calling employer's projects that this expert is assigned to. Scoped
    /// to the caller's own projects so it cannot reveal which other employers an
    /// expert works for.
    /// </summary>
    public async Task<IReadOnlyList<ProjectResponse>> ListProjectsForExpertAsync(
        int expertId,
        CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();
        await GuardExpertExistsAsync(expertId, cancellationToken);

        return await _db.ExpertProjects
            .AsNoTracking()
            .Where(ep => ep.ExpertId == expertId && ep.Project.EmployerId == employerId)
            .OrderBy(ep => ep.Project.Name)
            .Select(ep => new ProjectResponse(
                ep.Project.Id,
                ep.Project.EmployerId,
                ep.Project.Name,
                ep.Project.IsActive,
                ep.Project.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The experts assigned to one of the calling employer's projects.</summary>
    public async Task<IReadOnlyList<ExpertResponse>> ListExpertsForProjectAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(projectId, cancellationToken);

        return await _db.ExpertProjects
            .AsNoTracking()
            .Where(ep => ep.ProjectId == project.Id)
            .OrderBy(ep => ep.Expert.FullName)
            .Select(ep => new ExpertResponse(
                ep.Expert.Id,
                ep.Expert.UserId,
                ep.Expert.User.Username,
                ep.Expert.FullName,
                ep.Expert.IsActive,
                ep.Expert.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a project by id and owner. A miss — unknown id or another
    /// employer's project — is a <see cref="NotFoundException"/>, so the two
    /// cases are indistinguishable to the caller.
    /// </summary>
    private async Task<Project> FindOwnedProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();

        return await _db.Projects
                   .FirstOrDefaultAsync(p => p.Id == projectId && p.EmployerId == employerId, cancellationToken)
               ?? throw new NotFoundException("Project not found.");
    }

    private async Task GuardExpertExistsAsync(int expertId, CancellationToken cancellationToken)
    {
        var exists = await _db.Experts.AnyAsync(e => e.Id == expertId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Expert not found.");
        }
    }

    private Task<bool> IsAssignedAsync(int expertId, int projectId, CancellationToken cancellationToken) =>
        _db.ExpertProjects.AnyAsync(
            ep => ep.ExpertId == expertId && ep.ProjectId == projectId, cancellationToken);
}
