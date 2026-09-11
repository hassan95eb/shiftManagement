using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Common;

/// <summary>
/// Apply rule 5 (CLAUDE.md §5): a CallAgent may not hold two overlapping
/// commitments, using the half-open comparison <c>existing.Start &lt; new.End
/// AND existing.End &gt; new.Start</c> (docs/01-erd-and-schema.md §6), so shifts
/// that only touch (10–14 and 14–18) do not clash. V3 extends "commitment" from
/// just an <see cref="Domain.Enums.ApplicationStatus.Approved"/> application to
/// also include a directly-<see cref="ShiftStatus.Assigned"/> shift
/// (docs/04-v2-prompts.md V3) — a <see cref="ShiftStatus.Released"/> shift does
/// not count, since its assigned CallAgent has been excused from it.
/// </summary>
/// <remarks>
/// Shared by <c>ApplicationService.ApplyAsync</c>, <c>ApprovalService.ApproveAsync</c>
/// and <c>ShiftAssignmentService.AssignAsync</c> so the three call sites — apply,
/// re-check at approval, and direct assignment — cannot drift out of sync.
/// </remarks>
public static class ShiftOverlapPolicy
{
    public static async Task<bool> HasOverlapAsync(
        IAppDbContext db,
        int callAgentId,
        int excludeShiftId,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken cancellationToken)
    {
        var overlapsApprovedApplication = await db.ShiftApplications
            .AnyAsync(
                a => a.CallAgentId == callAgentId
                     && a.Status == ApplicationStatus.Approved
                     && a.Shift.StartUtc < endUtc
                     && a.Shift.EndUtc > startUtc,
                cancellationToken);
        if (overlapsApprovedApplication)
        {
            return true;
        }

        return await db.Shifts
            .AnyAsync(
                s => s.Id != excludeShiftId
                     && s.AssignedCallAgentId == callAgentId
                     && s.Status == ShiftStatus.Assigned
                     && s.StartUtc < endUtc
                     && s.EndUtc > startUtc,
                cancellationToken);
    }
}
