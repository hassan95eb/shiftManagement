using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// Apply rule 5 (the assignment's required test 2): an application is rejected
/// when the shift overlaps one the CallAgent is already approved for, using the
/// half-open comparison <c>existing.Start &lt; new.End AND existing.End &gt;
/// new.Start</c> (docs/01 §6). Adjacent shifts (10–14 and 14–18) share only an
/// instant, so they do <b>not</b> overlap and are allowed.
/// </summary>
public class ApplyForShift_OverlapTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static ApplicationService ServiceFor(SqliteTestContext ctx, int userId, int callAgentId)
    {
        var currentUser = StubCurrentUser.CallAgent(userId, callAgentId);
        return new(ctx.Db, currentUser, new AccessScope(currentUser), new TestClock(Now));
    }

    /// <summary>
    /// Common arrangement: a CallAgent assigned to one project, wide-open
    /// availability, and one shift they are already <see cref="ApplicationStatus.Approved"/>
    /// for running 10:00–14:00.
    /// </summary>
    private static (int UserId, int CallAgentId, int ProjectId) ArrangeApprovedTenToTwo(SqliteTestContext ctx)
    {
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(0), At(23));

        var approvedShift = ctx.Db.AddShift(project.Id, At(10), At(14));
        ctx.Db.AddApplication(approvedShift.Id, callAgent.Id, ApplicationStatus.Approved);

        return (callAgent.UserId, callAgent.Id, project.Id);
    }

    [Fact]
    public async Task A_non_overlapping_shift_can_still_be_applied_to()
    {
        using var ctx = new SqliteTestContext();
        var (userId, callAgentId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var laterShift = ctx.Db.AddShift(projectId, At(18), At(22));

        var created = await ServiceFor(ctx, userId, callAgentId)
            .ApplyAsync(laterShift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task An_overlapping_shift_is_rejected()
    {
        using var ctx = new SqliteTestContext();
        var (userId, callAgentId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var clashingShift = ctx.Db.AddShift(projectId, At(12), At(16)); // 12–16 overlaps 10–14

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, userId, callAgentId).ApplyAsync(clashingShift.Id, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().ShiftApplications.CountAsync());
    }

    [Fact]
    public async Task A_shift_that_starts_exactly_when_the_approved_one_ends_is_allowed()
    {
        using var ctx = new SqliteTestContext();
        var (userId, callAgentId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var adjacentAfter = ctx.Db.AddShift(projectId, At(14), At(18)); // 14–18 touches 10–14 at 14:00

        var created = await ServiceFor(ctx, userId, callAgentId)
            .ApplyAsync(adjacentAfter.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task A_shift_that_ends_exactly_when_the_approved_one_starts_is_allowed()
    {
        using var ctx = new SqliteTestContext();
        var (userId, callAgentId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var adjacentBefore = ctx.Db.AddShift(projectId, At(6), At(10)); // 6–10 touches 10–14 at 10:00

        var created = await ServiceFor(ctx, userId, callAgentId)
            .ApplyAsync(adjacentBefore.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task An_approved_shift_on_another_project_still_blocks_an_overlapping_application()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var support = ctx.Db.AddProject(supervisor.Id, "Support");
        var billing = ctx.Db.AddProject(supervisor.Id, "Billing");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, support.Id);
        ctx.Db.Assign(callAgent.Id, billing.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(0), At(23));

        // Approved on Support for 10–14; the overlap check is not scoped to one
        // project, so a clashing 12–16 shift on Billing is still rejected.
        var approvedOnSupport = ctx.Db.AddShift(support.Id, At(10), At(14));
        ctx.Db.AddApplication(approvedOnSupport.Id, callAgent.Id, ApplicationStatus.Approved);
        var clashingOnBilling = ctx.Db.AddShift(billing.Id, At(12), At(16));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, callAgent.UserId, callAgent.Id).ApplyAsync(clashingOnBilling.Id, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().ShiftApplications.CountAsync());
    }

    [Fact]
    public async Task Only_Approved_shifts_block_a_new_application_not_Pending_ones()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(0), At(23));

        var pendingShift = ctx.Db.AddShift(project.Id, At(10), At(14));
        ctx.Db.AddApplication(pendingShift.Id, callAgent.Id, ApplicationStatus.Pending);

        var clashingShift = ctx.Db.AddShift(project.Id, At(12), At(16));

        var created = await ServiceFor(ctx, callAgent.UserId, callAgent.Id)
            .ApplyAsync(clashingShift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }
}
