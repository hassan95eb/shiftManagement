using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Availabilities.Dtos;
using ShiftFlow.Application.Features.Availabilities.Validators;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Availabilities;

/// <summary>
/// CallAgent-facing availability use cases. Every method is scoped to
/// <see cref="ICurrentUser.RequireCallAgentId"/>: another CallAgent's window is
/// treated exactly like one that does not exist (<see cref="NotFoundException"/>),
/// so ids cannot be probed (CLAUDE.md §7). There is no supervisor-facing read of
/// a CallAgent's availability in this phase — see the phase report.
/// </summary>
/// <remarks>
/// Create and update both run the whole of the CallAgent's windows through
/// <see cref="AvailabilityMerge"/> and then reconcile the stored rows to the
/// normalized result, so the database never holds two windows that overlap or
/// touch regardless of the order rows were inserted. A window whose bounds
/// change as part of a merge is replaced, not edited in place; a window the
/// merge leaves untouched keeps its row and <c>CreatedAtUtc</c>.
/// </remarks>
public sealed class AvailabilityService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public AvailabilityService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Adds a window, merging it into any it overlaps or touches. Returns the
    /// merged window that now hosts the requested interval — which may already
    /// have existed, in which case nothing is written.
    /// </summary>
    public async Task<AvailabilityResponse> CreateAsync(
        AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        var requested = AvailabilityRequestValidator.ValidateAndNormalize(request);

        var current = await LoadWindowsAsync(callAgentId, exceptId: null, cancellationToken);
        var merged = AvailabilityMerge.Merge(current.Append(requested));

        await ReconcileAsync(callAgentId, merged, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ReadHostingWindowAsync(callAgentId, requested, cancellationToken);
    }

    /// <summary>The caller's windows, earliest first.</summary>
    public async Task<IReadOnlyList<AvailabilityResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();

        return await _db.Availabilities
            .AsNoTracking()
            .Where(a => a.CallAgentId == callAgentId)
            .OrderBy(a => a.StartUtc)
            .Select(a => new AvailabilityResponse(a.Id, a.CallAgentId, a.StartUtc, a.EndUtc, a.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Gets one of the caller's windows by id.</summary>
    public async Task<AvailabilityResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var window = await FindOwnedAsync(id, cancellationToken);
        return ToResponse(window);
    }

    /// <summary>
    /// Moves or resizes a window, then re-merges. Blocked with a 409 when the
    /// new shape would leave one of the CallAgent's approved shifts without a
    /// covering window — the same guard as <see cref="DeleteAsync"/>.
    /// </summary>
    public async Task<AvailabilityResponse> UpdateAsync(
        int id,
        AvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        _ = await FindOwnedAsync(id, cancellationToken);
        var requested = AvailabilityRequestValidator.ValidateAndNormalize(request);

        var others = await LoadWindowsAsync(callAgentId, exceptId: id, cancellationToken);
        var merged = AvailabilityMerge.Merge(others.Append(requested));

        await GuardApprovedShiftsStayCoveredAsync(callAgentId, merged, cancellationToken);

        await ReconcileAsync(callAgentId, merged, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await ReadHostingWindowAsync(callAgentId, requested, cancellationToken);
    }

    /// <summary>
    /// Removes a window. Blocked with a 409 when it is the window covering one
    /// of the CallAgent's approved shifts — deleting it would contradict work the
    /// supervisor already approved (CLAUDE.md §5).
    /// </summary>
    public async Task DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();
        var window = await FindOwnedAsync(id, cancellationToken);

        var remaining = await LoadWindowsAsync(callAgentId, exceptId: id, cancellationToken);
        await GuardApprovedShiftsStayCoveredAsync(callAgentId, remaining, cancellationToken);

        _db.Availabilities.Remove(window);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The CallAgent's stored windows as <see cref="TimeRange"/> values, optionally
    /// dropping one row by id (the row being updated or deleted).
    /// </summary>
    private async Task<List<TimeRange>> LoadWindowsAsync(
        int callAgentId,
        int? exceptId,
        CancellationToken cancellationToken) =>
        await _db.Availabilities
            .AsNoTracking()
            .Where(a => a.CallAgentId == callAgentId && (exceptId == null || a.Id != exceptId))
            .Select(a => new TimeRange(a.StartUtc, a.EndUtc))
            .ToListAsync(cancellationToken);

    private async Task<Availability> FindOwnedAsync(int id, CancellationToken cancellationToken)
    {
        var callAgentId = _currentUser.RequireCallAgentId();

        return await _db.Availabilities
                   .FirstOrDefaultAsync(a => a.Id == id && a.CallAgentId == callAgentId, cancellationToken)
               ?? throw new NotFoundException("Availability window not found.");
    }

    /// <summary>
    /// Brings the stored rows in line with <paramref name="merged"/>: rows whose
    /// bounds still appear verbatim are kept, every other row is deleted, and
    /// each merged window with no matching row is inserted. Because
    /// <paramref name="merged"/> is a pure function of the window union, the
    /// resulting row set does not depend on insertion order.
    /// </summary>
    private async Task ReconcileAsync(
        int callAgentId,
        IReadOnlyList<TimeRange> merged,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Availabilities
            .Where(a => a.CallAgentId == callAgentId)
            .ToListAsync(cancellationToken);

        var wanted = merged.ToList();

        foreach (var row in rows)
        {
            var match = wanted.FindIndex(w => w.StartUtc == row.StartUtc && w.EndUtc == row.EndUtc);
            if (match >= 0)
            {
                wanted.RemoveAt(match);
            }
            else
            {
                _db.Availabilities.Remove(row);
            }
        }

        var now = _clock.UtcNow;
        foreach (var window in wanted)
        {
            _db.Availabilities.Add(new Availability
            {
                CallAgentId = callAgentId,
                StartUtc = window.StartUtc,
                EndUtc = window.EndUtc,
                CreatedAtUtc = now,
            });
        }
    }

    private async Task GuardApprovedShiftsStayCoveredAsync(
        int callAgentId,
        IReadOnlyList<TimeRange> windows,
        CancellationToken cancellationToken)
    {
        var approvedShifts = await _db.ShiftApplications
            .AsNoTracking()
            .Where(a => a.CallAgentId == callAgentId && a.Status == ApplicationStatus.Approved)
            .Select(a => new TimeRange(a.Shift.StartUtc, a.Shift.EndUtc))
            .ToListAsync(cancellationToken);

        var uncovered = approvedShifts.Any(shift => !windows.Any(w => w.Covers(shift)));
        if (uncovered)
        {
            throw new BusinessRuleViolationException(
                "This change would leave an approved shift without a covering availability window.");
        }
    }

    private async Task<AvailabilityResponse> ReadHostingWindowAsync(
        int callAgentId,
        TimeRange requested,
        CancellationToken cancellationToken)
    {
        var window = await _db.Availabilities
            .AsNoTracking()
            .Where(a => a.CallAgentId == callAgentId
                        && a.StartUtc <= requested.StartUtc
                        && a.EndUtc >= requested.EndUtc)
            .OrderBy(a => a.StartUtc)
            .FirstAsync(cancellationToken);

        return ToResponse(window);
    }

    private static AvailabilityResponse ToResponse(Availability a) =>
        new(a.Id, a.CallAgentId, a.StartUtc, a.EndUtc, a.CreatedAtUtc);
}
