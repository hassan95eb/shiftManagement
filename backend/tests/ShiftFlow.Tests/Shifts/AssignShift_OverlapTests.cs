using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// Apply rule 5, extended by V3 (docs/04-v2-prompts.md) to also cover a
/// directly-Assigned shift, not just an Approved application. Half-open
/// comparison (docs/01 §6): a shift that only touches an existing commitment
/// does not clash.
/// </summary>
public class AssignShift_OverlapTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftAssignmentService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)));

    private static AssignShiftRequest Request(int callAgentId, string rowVersion) => new()
    {
        CallAgentId = callAgentId,
        RowVersion = rowVersion,
    };

    [Fact]
    public async Task Assigning_a_CallAgent_who_already_has_an_overlapping_Assigned_shift_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        ctx.Db.AddShift(project.Id, At(1, 10), At(1, 14), ShiftStatus.Assigned, jane.Id);

        var clashing = ctx.Db.AddShift(project.Id, At(1, 12), At(1, 16));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                clashing.Id,
                Request(jane.Id, Convert.ToBase64String(clashing.RowVersion)),
                CancellationToken.None));

        var stored = await ctx.NewContext().Shifts.SingleAsync(s => s.Id == clashing.Id);
        Assert.Null(stored.AssignedCallAgentId);
    }

    [Fact]
    public async Task Assigning_a_CallAgent_who_already_has_an_overlapping_approved_application_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);

        var approvedShift = ctx.Db.AddShift(project.Id, At(1, 10), At(1, 14));
        ctx.Db.AddApplication(approvedShift.Id, jane.Id, ApplicationStatus.Approved);

        var clashing = ctx.Db.AddShift(project.Id, At(1, 12), At(1, 16));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
                clashing.Id,
                Request(jane.Id, Convert.ToBase64String(clashing.RowVersion)),
                CancellationToken.None));
    }

    [Fact]
    public async Task A_shift_that_only_touches_an_existing_Assigned_shift_can_still_be_assigned()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        ctx.Db.AddShift(project.Id, At(1, 10), At(1, 14), ShiftStatus.Assigned, jane.Id);

        var adjacent = ctx.Db.AddShift(project.Id, At(1, 14), At(1, 18)); // starts exactly when the other ends

        var result = await ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
            adjacent.Id,
            Request(jane.Id, Convert.ToBase64String(adjacent.RowVersion)),
            CancellationToken.None);

        Assert.Equal("Assigned", result.Status);
    }

    [Fact]
    public async Task Filling_a_Released_shift_back_to_the_CallAgent_who_already_held_it_succeeds()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var shift = ctx.Db.AddShift(project.Id, At(1, 10), At(1, 14), ShiftStatus.Released, jane.Id);

        var result = await ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(
            shift.Id,
            Request(jane.Id, Convert.ToBase64String(shift.RowVersion)),
            CancellationToken.None);

        Assert.Equal("Assigned", result.Status);
        Assert.Equal(jane.Id, result.AssignedCallAgentId);
    }
}
