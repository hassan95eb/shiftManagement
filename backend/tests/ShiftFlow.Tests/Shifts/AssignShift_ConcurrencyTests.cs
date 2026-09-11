using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// docs/04-v2-prompts.md V3 acceptance: a RowVersion conflict on assignment is a
/// 409, exactly like <c>ShiftService.UpdateAsync</c> — the caller's
/// <c>rowVersion</c> is replayed as the concurrency token's original value, so a
/// stale one (someone else changed the shift since the caller last read it)
/// fails instead of silently overwriting the newer version.
/// </summary>
public class AssignShift_ConcurrencyTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftAssignmentService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)));

    [Fact]
    public async Task A_stale_RowVersion_is_a_ConcurrencyConflict()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));

        var stale = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                shift.Id,
                new AssignShiftRequest { CallAgentId = jane.Id, RowVersion = stale },
                CancellationToken.None));

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(ShiftStatus.Open, stored.Status);
        Assert.Null(stored.AssignedCallAgentId);
    }
}
