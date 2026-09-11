using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// docs/04-v2-prompts.md V3: <c>DELETE /api/shifts/{id}/assignment</c> is
/// <c>Assigned</c> → <c>Open</c> only. It does not accept a <c>Released</c>
/// shift — that status means the previously-assigned CallAgent has already been
/// excused, not that the shift is merely unfilled.
/// </summary>
public class UnassignShiftTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftAssignmentService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)));

    [Fact]
    public async Task Unassigning_an_Assigned_shift_returns_it_to_Open()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16), ShiftStatus.Assigned, jane.Id);

        var result = await ServiceFor(ctx, acme.UserId, acme.Id).UnassignAsync(shift.Id, CancellationToken.None);

        Assert.Equal("Open", result.Status);
        Assert.Null(result.AssignedCallAgentId);

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(ShiftStatus.Open, stored.Status);
        Assert.Null(stored.AssignedCallAgentId);
    }

    [Theory]
    [InlineData(ShiftStatus.Open)]
    [InlineData(ShiftStatus.Released)]
    [InlineData(ShiftStatus.Closed)]
    public async Task Unassigning_a_shift_that_is_not_Assigned_is_refused(ShiftStatus status)
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var assignedCallAgentId = status == ShiftStatus.Released ? jane.Id : (int?)null;
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16), status, assignedCallAgentId);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).UnassignAsync(shift.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Unassigning_another_supervisors_shift_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var globexShift = ctx.Db.AddShift(globexProject.Id, At(1, 8), At(1, 16), ShiftStatus.Assigned, jane.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).UnassignAsync(globexShift.Id, CancellationToken.None));
    }
}
