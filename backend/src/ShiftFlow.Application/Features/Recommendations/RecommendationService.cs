using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Recommendations.Dtos;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Recommendations;

/// <summary>
/// Employer-facing, read-only view of a shift's applicant ranking. The rows are
/// produced by the standalone Python script (docs/01-erd-and-schema.md §3-10,
/// §6-4); this service never writes to <c>Recommendations</c>.
/// </summary>
/// <remarks>
/// Ordering is score descending, then the CLAUDE.md §5 tie-break: fewer approved
/// hours first — counted for the month of <c>Shift.StartUtc</c>, not "now" — then
/// earlier <c>AppliedAtUtc</c>. Summing shift durations has no provider-agnostic
/// SQL form, so the candidates' approved shifts for that month are pulled and
/// totalled in memory; a shift has few applicants, so the set stays small.
/// </remarks>
public sealed class RecommendationService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public RecommendationService(IAppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    /// <summary>
    /// The ranking for <paramref name="shiftId"/>, best first. A shift on another
    /// employer's project — or an unknown id — is a <see cref="NotFoundException"/>
    /// (404), so ids are not probeable (CLAUDE.md §7). An empty list means the
    /// recommender has not run for this shift yet.
    /// </summary>
    public async Task<IReadOnlyList<RecommendationResponse>> ListForShiftAsync(
        int shiftId,
        CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();

        var shift = await _db.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == shiftId && s.Project.EmployerId == employerId,
                cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        // The recommender only ever scores applicants, so an inner join to the
        // application row is exact and also supplies the AppliedAtUtc tie-breaker.
        var rows = await (
            from r in _db.Recommendations.AsNoTracking()
            where r.ShiftId == shiftId
            join a in _db.ShiftApplications.AsNoTracking()
                on new { r.ShiftId, r.ExpertId } equals new { a.ShiftId, a.ExpertId }
            select new Row(r.ExpertId, r.Score, r.Reason, r.ComputedAtUtc, a.AppliedAtUtc))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return Array.Empty<RecommendationResponse>();
        }

        // Approved hours are counted for the month of Shift.StartUtc (CLAUDE.md §5).
        var monthStart = new DateTime(shift.StartUtc.Year, shift.StartUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var expertIds = rows.Select(x => x.ExpertId).ToList();

        var approvedSpans = await _db.ShiftApplications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Approved
                        && expertIds.Contains(a.ExpertId)
                        && a.Shift.StartUtc >= monthStart
                        && a.Shift.StartUtc < monthEnd)
            .Select(a => new { a.ExpertId, a.Shift.StartUtc, a.Shift.EndUtc })
            .ToListAsync(cancellationToken);

        var approvedHoursByExpert = approvedSpans
            .GroupBy(x => x.ExpertId)
            .ToDictionary(g => g.Key, g => g.Sum(x => (x.EndUtc - x.StartUtc).TotalHours));

        return rows
            .OrderByDescending(x => x.Score)
            .ThenBy(x => approvedHoursByExpert.GetValueOrDefault(x.ExpertId, 0d))
            .ThenBy(x => x.AppliedAtUtc)
            .ThenBy(x => x.ExpertId)
            .Select(x => new RecommendationResponse(x.ExpertId, x.Score, x.Reason, x.ComputedAtUtc))
            .ToList();
    }

    private sealed record Row(
        int ExpertId,
        decimal Score,
        string Reason,
        DateTime ComputedAtUtc,
        DateTime AppliedAtUtc);
}
