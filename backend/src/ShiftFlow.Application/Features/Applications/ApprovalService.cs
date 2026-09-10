using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Applications.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Application.Features.Applications;

/// <summary>
/// The employer's decision on a pending application. <see cref="ApproveAsync"/>
/// runs the exact CLAUDE.md §5 sequence inside one explicit transaction;
/// <see cref="RejectAsync"/> touches only the one row and leaves the shift
/// <see cref="ShiftStatus.Open"/> for the remaining applicants.
/// </summary>
/// <remarks>
/// <para><b>Ownership.</b> Both methods load the application filtered through
/// <c>Shift.Project.EmployerId</c>, so an application on another employer's
/// project — like an unknown id — is a <see cref="NotFoundException"/> (404),
/// never a 403 (CLAUDE.md §7).</para>
/// <para><b>Why an explicit transaction for approve.</b> The approved row, the
/// shift's move to <see cref="ShiftStatus.Closed"/> and the sibling rejections
/// are one atomic unit, and apply rule 5 is re-checked <i>inside</i> the
/// transaction — the expert may have been approved for a clashing shift since
/// they applied. A lone <see cref="IAppDbContext.SaveChangesAsync"/> would still
/// be atomic, but it could not hold the re-check and the writes in the same
/// isolation scope, and the filtered unique index
/// <c>UX_ShiftApplications_OneApproved</c> is only a last-ditch race backstop
/// (docs/01-erd-and-schema.md §3-8).</para>
/// </remarks>
public sealed class ApprovalService
{
    /// <summary>
    /// Stamped on the shift's other pending applications when it is filled.
    /// docs/01-erd-and-schema.md §3-8 gives this exact wording as the
    /// <see cref="ShiftApplication.DecisionNote"/> example.
    /// </summary>
    private const string ShiftFilledNote = "Shift filled by another expert.";

    private readonly IAppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ApprovalService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Approves <paramref name="applicationId"/>: the application becomes
    /// <see cref="ApplicationStatus.Approved"/> with the decision recorded, the
    /// shift becomes <see cref="ShiftStatus.Closed"/>, and every other pending
    /// application on that shift becomes <see cref="ApplicationStatus.Rejected"/>
    /// with <see cref="ShiftFilledNote"/> — all in one transaction, or none of it.
    /// </summary>
    public async Task<ApplicationResponse> ApproveAsync(int applicationId, CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();

        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);

        // Tracked, with the shift, scoped to the caller's own projects.
        var application = await _db.ShiftApplications
            .Include(a => a.Shift)
            .FirstOrDefaultAsync(
                a => a.Id == applicationId && a.Shift.Project.EmployerId == employerId,
                cancellationToken)
            ?? throw new NotFoundException("Application not found.");

        var shift = application.Shift;

        // Shift still Open. This is also what makes a second approval on the same
        // shift fail: the first approval closed it.
        if (shift.Status != ShiftStatus.Open)
        {
            throw new BusinessRuleViolationException("This shift is no longer open.");
        }

        // Only a pending application can be approved — approving one already
        // decided would silently resurrect a rejected row.
        if (application.Status != ApplicationStatus.Pending)
        {
            throw new BusinessRuleViolationException("This application has already been decided.");
        }

        // Re-check apply rule 5 against the current state, not the state at apply
        // time. Half-open comparison (docs/01-erd-and-schema.md §6), so a shift
        // that merely touches an approved one does not clash.
        var overlapsApproved = await _db.ShiftApplications
            .AnyAsync(
                a => a.ExpertId == application.ExpertId
                     && a.Status == ApplicationStatus.Approved
                     && a.Shift.StartUtc < shift.EndUtc
                     && a.Shift.EndUtc > shift.StartUtc,
                cancellationToken);
        if (overlapsApproved)
        {
            throw new BusinessRuleViolationException(
                "The expert has since been approved for a shift that overlaps this one.");
        }

        var decidedAtUtc = _clock.UtcNow;
        var decidedByUserId = _currentUser.UserId;

        application.Status = ApplicationStatus.Approved;
        application.DecidedByUserId = decidedByUserId;
        application.DecidedAtUtc = decidedAtUtc;

        shift.Status = ShiftStatus.Closed;

        var siblings = await _db.ShiftApplications
            .Where(a => a.ShiftId == shift.Id
                        && a.Id != application.Id
                        && a.Status == ApplicationStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var sibling in siblings)
        {
            sibling.Status = ApplicationStatus.Rejected;
            sibling.DecidedByUserId = decidedByUserId;
            sibling.DecidedAtUtc = decidedAtUtc;
            sibling.DecisionNote = ShiftFilledNote;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // Another writer moved the shift row between our load and our save.
            await transaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyConflictException(
                "This shift was decided by someone else. Reload it and try again.",
                exception);
        }
        catch (DbUpdateException exception)
        {
            // UX_ShiftApplications_OneApproved rejected a second approved row —
            // two approvals raced past the Open check at once.
            await transaction.RollbackAsync(cancellationToken);
            throw new BusinessRuleViolationException(
                "Another expert has just been approved for this shift.",
                exception);
        }

        return ToResponse(application);
    }

    /// <summary>
    /// Rejects <paramref name="applicationId"/> and nothing else. The shift stays
    /// <see cref="ShiftStatus.Open"/> and the other applications are untouched
    /// (CLAUDE.md §5). One row changes, so a single
    /// <see cref="IAppDbContext.SaveChangesAsync"/> is the whole write.
    /// </summary>
    public async Task<ApplicationResponse> RejectAsync(int applicationId, CancellationToken cancellationToken)
    {
        var employerId = _currentUser.RequireEmployerId();

        var application = await _db.ShiftApplications
            .FirstOrDefaultAsync(
                a => a.Id == applicationId && a.Shift.Project.EmployerId == employerId,
                cancellationToken)
            ?? throw new NotFoundException("Application not found.");

        if (application.Status != ApplicationStatus.Pending)
        {
            throw new BusinessRuleViolationException("This application has already been decided.");
        }

        application.Status = ApplicationStatus.Rejected;
        application.DecidedByUserId = _currentUser.UserId;
        application.DecidedAtUtc = _clock.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        return ToResponse(application);
    }

    private static ApplicationResponse ToResponse(ShiftApplication a) =>
        new(
            a.Id,
            a.ShiftId,
            a.ExpertId,
            a.Status.ToString(),
            a.AppliedAtUtc,
            a.DecidedByUserId,
            a.DecidedAtUtc,
            a.DecisionNote);
}
