using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// Apply rule 5 (the assignment's required test 2): an application is rejected
/// when the shift overlaps one the expert is already approved for, using the
/// half-open comparison <c>existing.Start &lt; new.End AND existing.End &gt;
/// new.Start</c> (docs/01 §6). Adjacent shifts (10–14 and 14–18) share only an
/// instant, so they do <b>not</b> overlap and are allowed.
/// </summary>
public class ApplyForShift_OverlapTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static ApplicationService ServiceFor(SqliteTestContext ctx, int userId, int expertId) =>
        new(ctx.Db, StubCurrentUser.Expert(userId, expertId), new TestClock(Now));

    /// <summary>
    /// Common arrangement: an expert assigned to one project, wide-open
    /// availability, and one shift they are already <see cref="ApplicationStatus.Approved"/>
    /// for running 10:00–14:00.
    /// </summary>
    private static (int UserId, int ExpertId, int ProjectId) ArrangeApprovedTenToTwo(SqliteTestContext ctx)
    {
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        ctx.Db.AddAvailability(expert.Id, At(0), At(23));

        var approvedShift = ctx.Db.AddShift(project.Id, At(10), At(14));
        ctx.Db.AddApplication(approvedShift.Id, expert.Id, ApplicationStatus.Approved);

        return (expert.UserId, expert.Id, project.Id);
    }

    [Fact]
    public async Task A_non_overlapping_shift_can_still_be_applied_to()
    {
        using var ctx = new SqliteTestContext();
        var (userId, expertId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var laterShift = ctx.Db.AddShift(projectId, At(18), At(22));

        var created = await ServiceFor(ctx, userId, expertId)
            .ApplyAsync(laterShift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task An_overlapping_shift_is_rejected()
    {
        using var ctx = new SqliteTestContext();
        var (userId, expertId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var clashingShift = ctx.Db.AddShift(projectId, At(12), At(16)); // 12–16 overlaps 10–14

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, userId, expertId).ApplyAsync(clashingShift.Id, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().ShiftApplications.CountAsync());
    }

    [Fact]
    public async Task A_shift_that_starts_exactly_when_the_approved_one_ends_is_allowed()
    {
        using var ctx = new SqliteTestContext();
        var (userId, expertId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var adjacentAfter = ctx.Db.AddShift(projectId, At(14), At(18)); // 14–18 touches 10–14 at 14:00

        var created = await ServiceFor(ctx, userId, expertId)
            .ApplyAsync(adjacentAfter.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task A_shift_that_ends_exactly_when_the_approved_one_starts_is_allowed()
    {
        using var ctx = new SqliteTestContext();
        var (userId, expertId, projectId) = ArrangeApprovedTenToTwo(ctx);
        var adjacentBefore = ctx.Db.AddShift(projectId, At(6), At(10)); // 6–10 touches 10–14 at 10:00

        var created = await ServiceFor(ctx, userId, expertId)
            .ApplyAsync(adjacentBefore.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task Only_Approved_shifts_block_a_new_application_not_Pending_ones()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        ctx.Db.AddAvailability(expert.Id, At(0), At(23));

        var pendingShift = ctx.Db.AddShift(project.Id, At(10), At(14));
        ctx.Db.AddApplication(pendingShift.Id, expert.Id, ApplicationStatus.Pending);

        var clashingShift = ctx.Db.AddShift(project.Id, At(12), At(16));

        var created = await ServiceFor(ctx, expert.UserId, expert.Id)
            .ApplyAsync(clashingShift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }
}
