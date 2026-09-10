using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// The happy path of the CLAUDE.md §5 approval transaction: the chosen
/// application is approved with the decision recorded, its shift is closed, and
/// every other <see cref="ApplicationStatus.Pending"/> application on that shift
/// is rejected — each with a decision note and the deciding user / time set. A
/// sibling that was already decided is left exactly as it was.
/// </summary>
public class ApproveApplication_SuccessTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Employer employer) =>
        new(ctx.Db, StubCurrentUser.Employer(employer.UserId, employer.Id), new TestClock(Now));

    [Fact]
    public async Task Approving_closes_the_shift_and_rejects_the_other_pending_applications()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));

        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");
        var nora = ctx.Db.AddExpert("Nora Fox");
        ctx.Db.Assign(jane.Id, project.Id);
        ctx.Db.Assign(mike.Id, project.Id);
        ctx.Db.Assign(nora.Id, project.Id);

        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(1));
        var mikeApp = ctx.Db.AddApplication(shift.Id, mike.Id, ApplicationStatus.Pending, On(2));
        var noraApp = ctx.Db.AddApplication(shift.Id, nora.Id, ApplicationStatus.Pending, On(3));

        var result = await ServiceFor(ctx, employer).ApproveAsync(janeApp.Id, CancellationToken.None);

        Assert.Equal("Approved", result.Status);
        Assert.Equal(employer.UserId, result.DecidedByUserId);
        Assert.Equal(Now, result.DecidedAtUtc);
        Assert.Null(result.DecisionNote); // the approved row carries no "filled" note

        var db = ctx.NewContext();

        var storedShift = await db.Shifts.SingleAsync(s => s.Id == shift.Id);
        Assert.Equal(ShiftStatus.Closed, storedShift.Status);

        foreach (var siblingId in new[] { mikeApp.Id, noraApp.Id })
        {
            var sibling = await db.ShiftApplications.SingleAsync(a => a.Id == siblingId);
            Assert.Equal(ApplicationStatus.Rejected, sibling.Status);
            Assert.Equal(employer.UserId, sibling.DecidedByUserId);
            Assert.Equal(Now, sibling.DecidedAtUtc);
            Assert.False(string.IsNullOrWhiteSpace(sibling.DecisionNote));
        }
    }

    [Fact]
    public async Task An_already_rejected_sibling_is_not_touched_by_the_approval()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));

        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");
        ctx.Db.Assign(jane.Id, project.Id);
        ctx.Db.Assign(mike.Id, project.Id);

        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(1));
        var mikeApp = ctx.Db.AddApplication(shift.Id, mike.Id, ApplicationStatus.Rejected, On(2));

        await ServiceFor(ctx, employer).ApproveAsync(janeApp.Id, CancellationToken.None);

        var stored = await ctx.NewContext().ShiftApplications.SingleAsync(a => a.Id == mikeApp.Id);
        Assert.Equal(ApplicationStatus.Rejected, stored.Status);
        Assert.Null(stored.DecidedByUserId);
        Assert.Null(stored.DecidedAtUtc);
        Assert.Null(stored.DecisionNote);
    }

    [Fact]
    public async Task A_lone_applicant_is_approved_and_the_shift_closes_with_no_rejections()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var jane = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);
        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(1));

        await ServiceFor(ctx, employer).ApproveAsync(janeApp.Id, CancellationToken.None);

        var db = ctx.NewContext();
        Assert.Equal(ShiftStatus.Closed, (await db.Shifts.SingleAsync()).Status);
        Assert.Equal(1, await db.ShiftApplications.CountAsync(a => a.Status == ApplicationStatus.Approved));
        Assert.Equal(0, await db.ShiftApplications.CountAsync(a => a.Status == ApplicationStatus.Rejected));
    }
}
