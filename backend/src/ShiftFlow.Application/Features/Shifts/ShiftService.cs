using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Application.Features.Shifts.Validators;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Shifts;

/// <summary>
/// Supervisor-facing shift use cases. Every method is scoped through the shift's
/// project to <see cref="ICurrentUser.RequireSupervisorId"/>: a shift on another
/// supervisor's project is treated exactly like one that does not exist
/// (<see cref="NotFoundException"/>), so ids cannot be probed (CLAUDE.md §7).
/// </summary>
/// <remarks>
/// A supervisor may set a shift's schedule only at creation, and may correct it
/// afterwards through <see cref="UpdateAsync"/> <i>only while the shift is still
/// <see cref="ShiftStatus.Open"/> and no CallAgent has applied to it</i>. Once an
/// application references the shift, its window is frozen — applicants were
/// evaluated against that exact interval (apply rules 3 and 5, and the scoring
/// math, all bind to <c>Shift.StartUtc</c>/<c>EndUtc</c>). Status is never set
/// here: <c>Open → Closed</c> is owned by the approval transaction (phase 11),
/// which also rejects the other pending applications atomically; re-opening a
/// closed shift is in no design doc. So this service exposes no status write and
/// no delete (a shift is removed only by deleting its project, which cascades).
/// </remarks>
public sealed class ShiftService
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ShiftService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>Creates an <see cref="ShiftStatus.Open"/> shift on one of the caller's projects.</summary>
    public async Task<ShiftResponse> CreateAsync(CreateShiftRequest request, CancellationToken cancellationToken)
    {
        var supervisorId = _currentUser.RequireSupervisorId();
        var (projectId, startUtc, endUtc) = ShiftRequestValidator.ValidateAndNormalize(request);

        var projectExists = await _db.Projects
            .AnyAsync(p => p.Id == projectId && p.SupervisorId == supervisorId, cancellationToken);
        if (!projectExists)
        {
            throw new NotFoundException("Project not found.");
        }

        var shift = new Shift
        {
            ProjectId = projectId,
            StartUtc = startUtc,
            EndUtc = endUtc,
            Status = ShiftStatus.Open,
            CreatedAtUtc = _clock.UtcNow,
        };

        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(shift);
    }

    /// <summary>The caller's shifts, most recent start first, narrowed by the optional filters.</summary>
    public async Task<IReadOnlyList<ShiftResponse>> ListAsync(
        ShiftListFilter filter,
        CancellationToken cancellationToken)
    {
        var supervisorId = _currentUser.RequireSupervisorId();

        var query = _db.Shifts
            .AsNoTracking()
            .Where(s => s.Project.SupervisorId == supervisorId);

        if (filter.ProjectId is { } projectId)
        {
            query = query.Where(s => s.ProjectId == projectId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(s => s.Status == status);
        }

        if (filter.FromUtc is { } fromUtc)
        {
            query = query.Where(s => s.StartUtc >= fromUtc);
        }

        if (filter.ToUtc is { } toUtc)
        {
            query = query.Where(s => s.StartUtc <= toUtc);
        }

        var shifts = await query
            .OrderByDescending(s => s.StartUtc)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return shifts.Select(ToResponse).ToList();
    }

    /// <summary>Gets one of the caller's shifts by id.</summary>
    public async Task<ShiftResponse> GetAsync(int id, CancellationToken cancellationToken)
    {
        var shift = await FindOwnedAsync(id, tracked: false, cancellationToken);
        return ToResponse(shift);
    }

    /// <summary>
    /// Corrects a shift's window. Refused with 409 when the shift is no longer
    /// <see cref="ShiftStatus.Open"/> or a CallAgent has already applied to it, and
    /// with 409 when <paramref name="request"/> carries a stale
    /// <c>RowVersion</c>.
    /// </summary>
    public async Task<ShiftResponse> UpdateAsync(
        int id,
        UpdateShiftRequest request,
        CancellationToken cancellationToken)
    {
        var (startUtc, endUtc, rowVersion) = ShiftRequestValidator.ValidateAndNormalize(request);
        var shift = await FindOwnedAsync(id, tracked: true, cancellationToken);

        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleViolationException(
                "A closed shift cannot be edited.");
        }

        var hasApplications = await _db.ShiftApplications
            .AnyAsync(a => a.ShiftId == shift.Id, cancellationToken);
        if (hasApplications)
        {
            throw new BusinessRuleViolationException(
                "This shift already has applications and its time can no longer be changed.");
        }

        shift.StartUtc = startUtc;
        shift.EndUtc = endUtc;

        // Replay the token the caller last saw as the concurrency check's
        // baseline: an edit built on a stale read updates zero rows.
        _db.Entry(shift).Property(s => s.RowVersion).OriginalValue = rowVersion;

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new ConcurrencyConflictException(
                "This shift was changed by someone else. Reload it and try again.",
                exception);
        }

        return ToResponse(shift);
    }

    /// <summary>
    /// Loads a shift by id whose project belongs to the caller. A miss — unknown
    /// id or another supervisor's shift — is a <see cref="NotFoundException"/>, so
    /// the two are indistinguishable to the caller.
    /// </summary>
    private async Task<Shift> FindOwnedAsync(int id, bool tracked, CancellationToken cancellationToken)
    {
        var supervisorId = _currentUser.RequireSupervisorId();

        var query = _db.Shifts.Where(s => s.Id == id && s.Project.SupervisorId == supervisorId);
        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("Shift not found.");
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
