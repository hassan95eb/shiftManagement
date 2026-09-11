using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Applications.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Applications;

/// <summary>
/// The apply-to-shift use case and the two application-history reads. The five
/// business rules of CLAUDE.md §5 are enforced here, in the Application layer,
/// each with its own message — the database constraints
/// (<c>UQ_ShiftApplications_Shift_CallAgent</c>, the filtered one-approved index)
/// are a race backstop, not the primary check.
/// </summary>
/// <remarks>
/// <para><b>Rule → HTTP status.</b></para>
/// <list type="bullet">
///   <item>
///     Rule 2 (project membership) → <b>404</b>. A shift on a project the caller
///     is not assigned to is indistinguishable from one that does not exist, so
///     membership cannot be probed (CLAUDE.md §7). This is the same 404 the
///     CallAgent-facing shift read returns.
///   </item>
///   <item>
///     Rule 1 (shift not Open), rule 3 (availability coverage), rule 4
///     (duplicate) and rule 5 (approved-shift overlap) → <b>409</b>
///     (<see cref="BusinessRuleViolationException"/>). Once membership is
///     established the shift's existence is not secret, so these report the
///     actual conflict with the current state rather than hiding behind a 404.
///   </item>
/// </list>
/// <para>
/// The rules are checked cheapest-first after the membership gate: status, then
/// duplicate (one <c>Any</c>), then availability coverage, then the approved
/// overlap scan. A repeat submit therefore gets "already applied" rather than a
/// stale coverage error if the CallAgent's windows changed since.
/// </para>
/// </remarks>
public sealed class ApplicationService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ApplicationService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Records a <see cref="ApplicationStatus.Pending"/> application by the
    /// calling CallAgent for <paramref name="shiftId"/>, once all five apply rules
    /// pass.
    /// </summary>
    public async Task<ApplicationResponse> ApplyAsync(int shiftId, CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();

        // Rule 2 — project membership. Folded together with "unknown id": a shift
        // whose project the caller is not assigned to is a 404, never a 403, so
        // the membership cannot be probed (CLAUDE.md §7). Status is deliberately
        // not filtered here — a closed shift on the caller's project must reach
        // rule 1 and its own message, not this 404.
        var shift = await _db.Shifts
            .AsNoTracking()
            .FirstOrDefaultAsync(
                s => s.Id == shiftId
                     && s.Project.CallAgentProjects.Any(ep => ep.CallAgentId == callAgentId),
                cancellationToken)
            ?? throw new NotFoundException("Shift not found.");

        // Rule 1 — the shift must be Open.
        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleViolationException(
                "This shift is no longer open for applications.");
        }

        // Rule 4 — no duplicate application. Any prior row for this (shift,
        // CallAgent) pair blocks a re-apply regardless of its status, because
        // UNIQUE(ShiftId, CallAgentId) would reject the insert anyway.
        var alreadyApplied = await _db.ShiftApplications
            .AnyAsync(a => a.ShiftId == shiftId && a.CallAgentId == callAgentId, cancellationToken);
        if (alreadyApplied)
        {
            throw new BusinessRuleViolationException(
                "You have already applied to this shift.");
        }

        // Rule 3 — the whole shift must fall inside one availability window.
        // Phase 6 merges overlapping and adjacent windows on write, so this is a
        // single-window containment test: a shift spanning what used to be two
        // touching windows is now covered by the one merged window.
        var covered = await _db.Availabilities
            .AsNoTracking()
            .AnyAsync(
                a => a.CallAgentId == callAgentId
                     && a.StartUtc <= shift.StartUtc
                     && a.EndUtc >= shift.EndUtc,
                cancellationToken);
        if (!covered)
        {
            throw new BusinessRuleViolationException(
                "Your availability does not cover the whole of this shift.");
        }

        // Rule 5 — no overlap with an already-approved shift. Half-open
        // comparison (docs/01 §6), so back-to-back shifts such as 10–14 and
        // 14–18 do not conflict.
        var overlapsApproved = await _db.ShiftApplications
            .AsNoTracking()
            .AnyAsync(
                a => a.CallAgentId == callAgentId
                     && a.Status == ApplicationStatus.Approved
                     && a.Shift.StartUtc < shift.EndUtc
                     && a.Shift.EndUtc > shift.StartUtc,
                cancellationToken);
        if (overlapsApproved)
        {
            throw new BusinessRuleViolationException(
                "This shift overlaps another shift you are already approved for.");
        }

        var application = new ShiftApplication
        {
            ShiftId = shiftId,
            CallAgentId = callAgentId,
            Status = ApplicationStatus.Pending,
            AppliedAtUtc = _clock.UtcNow,
        };

        _db.ShiftApplications.Add(application);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    /// <summary>
    /// Application history for the caller. An <b>CallAgent</b> sees their own
    /// applications; an <b>supervisor</b> sees the applications on shifts of their
    /// own projects. Newest <see cref="ShiftApplication.AppliedAtUtc"/> first.
    /// The optional <paramref name="filter"/> only narrows that set further.
    /// </summary>
    public async Task<IReadOnlyList<ApplicationResponse>> ListAsync(
        ApplicationListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _db.ShiftApplications.AsNoTracking();

        query = _currentUser.Role == UserRole.Supervisor
            ? ScopeToSupervisor(query)
            : ScopeToCallAgent(query);

        if (filter.ShiftId is { } shiftId)
        {
            query = query.Where(a => a.ShiftId == shiftId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderByDescending(a => a.AppliedAtUtc)
            .ThenByDescending(a => a.Id)
            .Select(a => ToResponse(a))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<ShiftApplication> ScopeToCallAgent(IQueryable<ShiftApplication> query)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        return query.Where(a => a.CallAgentId == callAgentId);
    }

    private IQueryable<ShiftApplication> ScopeToSupervisor(IQueryable<ShiftApplication> query)
    {
        var supervisorId = _currentUser.RequireSupervisorId();
        return query.Where(a => a.Shift.Project.SupervisorId == supervisorId);
    }

    private static ApplicationResponse ToResponse(ShiftApplication a) =>
        new(
            a.Id,
            a.ShiftId,
            a.CallAgentId,
            a.Status.ToString(),
            a.AppliedAtUtc,
            a.DecidedByUserId,
            a.DecidedAtUtc,
            a.DecisionNote);
}
