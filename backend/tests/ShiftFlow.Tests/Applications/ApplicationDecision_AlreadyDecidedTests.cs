using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// CLAUDE.md §5: a decision is one-directional. Neither
/// <see cref="ApprovalService.ApproveAsync"/> nor
/// <see cref="ApprovalService.RejectAsync"/> may act on an application that has
/// already been decided — the shift-still-Open guard does not catch every case,
/// because <see cref="ApprovalService.RejectAsync"/> leaves the shift Open.
/// An unknown id is a <see cref="NotFoundException"/>, exactly as it is for approve.
/// </summary>
public class ApplicationDecision_AlreadyDecidedTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Supervisor supervisor) =>
        new(ctx.Db, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id), new TestClock(Now));

    [Fact]
    public async Task Approving_a_rejected_application_while_the_shift_is_still_open_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));

        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(1));

        var service = ServiceFor(ctx, supervisor);

        // Reject leaves the shift Open, so the shift-status guard cannot stand in
        // for the already-decided guard here.
        await service.RejectAsync(janeApp.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.ApproveAsync(janeApp.Id, CancellationToken.None));

        var db = ctx.NewContext();
        Assert.Equal(ApplicationStatus.Rejected, (await db.ShiftApplications.SingleAsync(a => a.Id == janeApp.Id)).Status);
        Assert.Equal(ShiftStatus.Open, (await db.Shifts.SingleAsync(s => s.Id == shift.Id)).Status);
    }

    [Fact]
    public async Task Rejecting_an_already_approved_application_is_refused_and_the_approval_stands()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));

        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(1));

        var service = ServiceFor(ctx, supervisor);
        await service.ApproveAsync(janeApp.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.RejectAsync(janeApp.Id, CancellationToken.None));

        var db = ctx.NewContext();
        Assert.Equal(ApplicationStatus.Approved, (await db.ShiftApplications.SingleAsync(a => a.Id == janeApp.Id)).Status);
        Assert.Equal(ShiftStatus.Closed, (await db.Shifts.SingleAsync(s => s.Id == shift.Id)).Status);
    }

    [Fact]
    public async Task Rejecting_an_already_rejected_application_is_refused()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));

        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(1));

        var service = ServiceFor(ctx, supervisor);
        await service.RejectAsync(janeApp.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            service.RejectAsync(janeApp.Id, CancellationToken.None));

        Assert.Equal(
            ApplicationStatus.Rejected,
            (await ctx.NewContext().ShiftApplications.SingleAsync(a => a.Id == janeApp.Id)).Status);
    }

    [Fact]
    public async Task Rejecting_an_unknown_application_id_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, supervisor).RejectAsync(9999, CancellationToken.None));
    }
}
