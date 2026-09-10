using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Shifts;

/// <summary>
/// Expert-facing read of the shifts an expert can apply to: those that are
/// <see cref="ShiftStatus.Open"/> and belong to a project the expert is assigned
/// to. Everything else — a closed shift, a shift on a project the expert is not
/// assigned to, an unknown id — is a <see cref="NotFoundException"/>, so project
/// membership cannot be probed (CLAUDE.md §7). There is no expert view of a
/// closed shift in this phase; when applications exist (phase 9) that phase adds
/// the expert's own-applications view.
/// </summary>
public sealed class OpenShiftService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public OpenShiftService(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Open shifts on the caller's assigned projects, earliest start first.
    /// When <paramref name="projectId"/> is given, the caller must be assigned
    /// to that project or the result is <see cref="NotFoundException"/> (not an
    /// empty list), so "not assigned" and "no open shifts" stay distinct only to
    /// someone who is assigned.
    /// </summary>
    public async Task<IReadOnlyList<ShiftResponse>> ListAsync(
        int? projectId,
        CancellationToken cancellationToken)
    {
        var expertId = _currentUser.RequireExpertId();

        if (projectId is { } pid)
        {
            var assigned = await _db.ExpertProjects
                .AnyAsync(ep => ep.ExpertId == expertId && ep.ProjectId == pid, cancellationToken);
            if (!assigned)
            {
                throw new NotFoundException("Project not found.");
            }
        }

        // Navigating Shift -> Project -> ExpertProjects (rather than a bare EXISTS
        // on ExpertProjects) lets SQL Server drive the plan from the caller's
        // handful of assigned projects into a per-project Index Seek on
        // IX_Shifts_ProjectId_Status — the index docs/01 §5 designates for
        // "open shifts on an expert's assigned projects" — instead of scanning
        // Shifts. Verified against the container; see the phase report.
        var query = _db.Shifts
            .AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Open
                        && s.Project.ExpertProjects.Any(ep => ep.ExpertId == expertId));

        if (projectId is { } p)
        {
            query = query.Where(s => s.ProjectId == p);
        }

        var shifts = await query
            .OrderBy(s => s.StartUtc)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return shifts.Select(ToResponse).ToList();
    }

    /// <summary>Gets one open shift on one of the caller's assigned projects by id.</summary>
    public async Task<ShiftResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var expertId = _currentUser.RequireExpertId();

        var shift = await _db.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == id
                     && s.Status == ShiftStatus.Open
                     && s.Project.ExpertProjects.Any(ep => ep.ExpertId == expertId),
                cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        return ToResponse(shift);
    }

    private static ShiftResponse ToResponse(Shift s) =>
        new(
            s.Id,
            s.ProjectId,
            s.StartUtc,
            s.EndUtc,
            s.Status.ToString(),
            s.CreatedAtUtc,
            Convert.ToBase64String(s.RowVersion));
}
