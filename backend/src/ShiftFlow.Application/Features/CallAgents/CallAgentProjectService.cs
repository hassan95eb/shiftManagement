using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.CallAgents.Dtos;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.CallAgents;

/// <summary>
/// Assigns CallAgents to, and removes them from, a supervisor's projects. The routes
/// start with the CallAgent (<c>/api/call-agents/{id}/projects/{projectId}</c>) but the
/// authorization check that matters is on the <em>project</em>: it must belong
/// to the calling supervisor, verified from the database, before anything is
/// written. Checking only that the CallAgent exists would let a supervisor assign
/// someone to another supervisor's project (CLAUDE.md §7).
/// </summary>
public sealed class CallAgentProjectService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public CallAgentProjectService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Assigns the CallAgent to the project. Idempotent: if the assignment already
    /// exists the call succeeds without a second write, so a repeated request
    /// never trips the composite primary key.
    /// </summary>
    public async Task AssignAsync(int callAgentId, int projectId, CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(projectId, cancellationToken);
        await GuardCallAgentExistsAsync(callAgentId, cancellationToken);

        var alreadyAssigned = await _db.CallAgentProjects.AnyAsync(
            ep => ep.CallAgentId == callAgentId && ep.ProjectId == project.Id, cancellationToken);
        if (alreadyAssigned)
        {
            return;
        }

        _db.CallAgentProjects.Add(new CallAgentProject
        {
            CallAgentId = callAgentId,
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
            _db.CallAgentProjects.Remove(_db.CallAgentProjects.Local.First(
                ep => ep.CallAgentId == callAgentId && ep.ProjectId == project.Id));

            if (!await IsAssignedAsync(callAgentId, project.Id, cancellationToken))
            {
                throw;
            }
        }
    }

    /// <summary>
    /// Removes the assignment. Blocked with a 409 when the CallAgent has an
    /// <see cref="ApplicationStatus.Approved"/> application for a shift on this
    /// project — that would contradict work already approved. Any
    /// <see cref="ApplicationStatus.Pending"/> applications for the project's
    /// shifts are rejected with a decision note in the same transaction.
    /// </summary>
    public async Task UnassignAsync(int callAgentId, int projectId, CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(projectId, cancellationToken);

        var assignment = await _db.CallAgentProjects.FirstOrDefaultAsync(
                             ep => ep.CallAgentId == callAgentId && ep.ProjectId == project.Id, cancellationToken)
                         ?? throw new NotFoundException("This CallAgent is not assigned to the project.");

        var hasApproved = await _db.ShiftApplications.AnyAsync(
            a => a.CallAgentId == callAgentId
                 && a.Status == ApplicationStatus.Approved
                 && a.Shift.ProjectId == project.Id,
            cancellationToken);
        if (hasApproved)
        {
            throw new BusinessRuleViolationException(
                "This CallAgent has an approved application for a shift on this project; "
                + "the assignment cannot be removed.");
        }

        var pending = await _db.ShiftApplications
            .Where(a => a.CallAgentId == callAgentId
                        && a.Status == ApplicationStatus.Pending
                        && a.Shift.ProjectId == project.Id)
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        foreach (var application in pending)
        {
            application.Status = ApplicationStatus.Rejected;
            application.DecidedByUserId = _currentUser.UserId;
            application.DecidedAtUtc = now;
            application.DecisionNote = "CallAgent unassigned from the project.";
        }

        _db.CallAgentProjects.Remove(assignment);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The calling supervisor's projects that this CallAgent is assigned to. Scoped
    /// to the caller's own projects so it cannot reveal which other supervisors an
    /// CallAgent works for.
    /// </summary>
    public async Task<IReadOnlyList<ProjectResponse>> ListProjectsForCallAgentAsync(
        int callAgentId,
        CancellationToken cancellationToken)
    {
        var supervisorId = _currentUser.RequireSupervisorId();
        await GuardCallAgentExistsAsync(callAgentId, cancellationToken);

        return await _db.CallAgentProjects
            .AsNoTracking()
            .Where(ep => ep.CallAgentId == callAgentId && ep.Project.SupervisorId == supervisorId)
            .OrderBy(ep => ep.Project.Name)
            .Select(ep => new ProjectResponse(
                ep.Project.Id,
                ep.Project.SupervisorId,
                ep.Project.Name,
                ep.Project.IsActive,
                ep.Project.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The CallAgents assigned to one of the calling supervisor's projects.</summary>
    public async Task<IReadOnlyList<CallAgentResponse>> ListCallAgentsForProjectAsync(
        int projectId,
        CancellationToken cancellationToken)
    {
        var project = await FindOwnedProjectAsync(projectId, cancellationToken);

        return await _db.CallAgentProjects
            .AsNoTracking()
            .Where(ep => ep.ProjectId == project.Id)
            .OrderBy(ep => ep.CallAgent.FullName)
            .Select(ep => new CallAgentResponse(
                ep.CallAgent.Id,
                ep.CallAgent.UserId,
                ep.CallAgent.FullName,
                ep.CallAgent.IsActive,
                ep.CallAgent.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Loads a project by id and owner. A miss — unknown id or another
    /// supervisor's project — is a <see cref="NotFoundException"/>, so the two
    /// cases are indistinguishable to the caller.
    /// </summary>
    private async Task<Project> FindOwnedProjectAsync(int projectId, CancellationToken cancellationToken)
    {
        var supervisorId = _currentUser.RequireSupervisorId();

        return await _db.Projects
                   .FirstOrDefaultAsync(p => p.Id == projectId && p.SupervisorId == supervisorId, cancellationToken)
               ?? throw new NotFoundException("Project not found.");
    }

    private async Task GuardCallAgentExistsAsync(int callAgentId, CancellationToken cancellationToken)
    {
        var exists = await _db.CallAgents.AnyAsync(e => e.Id == callAgentId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("CallAgent not found.");
        }
    }

    private Task<bool> IsAssignedAsync(int callAgentId, int projectId, CancellationToken cancellationToken) =>
        _db.CallAgentProjects.AnyAsync(
            ep => ep.CallAgentId == callAgentId && ep.ProjectId == projectId, cancellationToken);
}
