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
/// The Supervisor's direct-assignment path for a shift, alongside the existing
/// application-based one. Owns exactly two of the shift state machine's
/// transitions (docs/01-erd-and-schema.md §7, docs/04-v2-prompts.md V3):
/// <c>Open</c>/<c>Released</c> → <c>Assigned</c> on assign, and
/// <c>Assigned</c> → <c>Open</c> on unassign. Neither method writes a
/// <see cref="ShiftApplication"/> row.
/// </summary>
/// <remarks>
/// Every read is scoped through <see cref="IAccessScope"/> exactly like
/// <see cref="ShiftService"/>: a shift on another supervisor's project is a
/// <see cref="NotFoundException"/>, never a 403 (CLAUDE.md §7).
/// </remarks>
public sealed class ShiftAssignmentService
{
    private readonly IAppDbContext _db;
    private readonly IAccessScope _accessScope;

    public ShiftAssignmentService(IAppDbContext db, IAccessScope accessScope)
    {
        _db = db;
        _accessScope = accessScope;
    }

    /// <summary>
    /// Assigns <paramref name="request"/>'s CallAgent to <paramref name="shiftId"/>
    /// directly. Accepts the shift in <see cref="ShiftStatus.Open"/> (→
    /// <see cref="ShiftStatus.Assigned"/>) or <see cref="ShiftStatus.Released"/>
    /// (the direct-fill path back to <see cref="ShiftStatus.Assigned"/>, once V5
    /// can produce a Released shift); any other status is a 409. The CallAgent
    /// must be a member of the shift's project and must not already hold an
    /// overlapping commitment.
    /// </summary>
    public async Task<ShiftResponse> AssignAsync(
        int shiftId,
        AssignShiftRequest request,
        CancellationToken cancellationToken)
    {
        var (callAgentId, rowVersion) = ShiftAssignmentRequestValidator.ValidateAndNormalize(request);

        var shift = await FindOwnedAsync(shiftId, cancellationToken);

        if (shift.Status != ShiftStatus.Open && shift.Status != ShiftStatus.Released)
        {
            throw new BusinessRuleViolationException(
                "Only an Open or Released shift can be assigned.");
        }

        var callAgentExists = await _db.CallAgents.AnyAsync(c => c.Id == callAgentId, cancellationToken);
        if (!callAgentExists)
        {
            throw new NotFoundException("CallAgent not found.");
        }

        var isProjectMember = await _db.CallAgentProjects
            .AnyAsync(cp => cp.CallAgentId == callAgentId && cp.ProjectId == shift.ProjectId, cancellationToken);
        if (!isProjectMember)
        {
            throw new BusinessRuleViolationException(
                "This CallAgent is not a member of the shift's project.");
        }

        var hasOverlap = await ShiftOverlapPolicy.HasOverlapAsync(
            _db, callAgentId, shift.Id, shift.StartUtc, shift.EndUtc, cancellationToken);
        if (hasOverlap)
        {
            throw new BusinessRuleViolationException(
                "This CallAgent already has an overlapping shift.");
        }

        shift.Status = ShiftStatus.Assigned;
        shift.AssignedCallAgentId = callAgentId;

        // Replay the token the caller last saw as the concurrency check's
        // baseline, exactly like ShiftService.UpdateAsync — a concurrent
        // assignment built on a stale read updates zero rows.
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
    /// Clears the assignment on <paramref name="shiftId"/>, returning it to
    /// <see cref="ShiftStatus.Open"/>. Refused with 409 when the shift is not
    /// <see cref="ShiftStatus.Assigned"/>. docs/04-v2-prompts.md V3 also blocks
    /// this while an attendance session exists for the shift; there is no such
    /// thing before V4 introduces <c>AttendanceSessions</c>, so that guard has
    /// nothing to check yet and is added in that phase.
    /// </summary>
    public async Task<ShiftResponse> UnassignAsync(int shiftId, CancellationToken cancellationToken)
    {
        var shift = await FindOwnedAsync(shiftId, cancellationToken);

        if (shift.Status != ShiftStatus.Assigned)
        {
            throw new BusinessRuleViolationException(
                "Only an Assigned shift's assignment can be removed.");
        }

        var hasAttendance = await _db.AttendanceSessions
            .AnyAsync(a => a.ShiftId == shift.Id, cancellationToken);
        if (hasAttendance)
        {
            throw new BusinessRuleViolationException(
                "An assignment with attendance history cannot be removed.");
        }

        shift.Status = ShiftStatus.Open;
        shift.AssignedCallAgentId = null;

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
    /// Loads a shift by id whose project belongs to the caller, tracked. A miss —
    /// unknown id or another supervisor's shift — is a <see cref="NotFoundException"/>.
    /// </summary>
    private async Task<Shift> FindOwnedAsync(int shiftId, CancellationToken cancellationToken)
    {
        return await _accessScope
                   .RestrictToOwnSupervisor(_db.Shifts.Where(s => s.Id == shiftId), s => s.Project.SupervisorId)
                   .FirstOrDefaultAsync(cancellationToken)
               ?? throw new NotFoundException("Shift not found.");
    }

    private static ShiftResponse ToResponse(Shift s) =>
        new(
            s.Id,
            s.ProjectId,
            s.StartUtc,
            s.EndUtc,
            s.Status.ToString(),
            s.AssignedCallAgentId,
            s.CreatedAtUtc,
            Convert.ToBase64String(s.RowVersion));
}
