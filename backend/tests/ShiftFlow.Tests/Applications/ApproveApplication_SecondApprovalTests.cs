using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// One expert per shift (CLAUDE.md §5). Once an application is approved the shift
/// is Closed, so a second approval — on any application of that shift — is
/// refused, and the first approval stands untouched.
/// </summary>
public class ApproveApplication_SecondApprovalTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Employer employer) =>
        new(ctx.Db, StubCurrentUser.Employer(employer.UserId, employer.Id), new TestClock(Now));

    [Fact]
    public async Task A_second_approval_on_the_same_shift_fails_and_the_first_stands()
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
        var mikeApp = ctx.Db.AddApplication(shift.Id, mike.Id, ApplicationStatus.Pending, On(2));

        await ServiceFor(ctx, employer).ApproveAsync(janeApp.Id, CancellationToken.None);

        // Mike's row was auto-rejected by Jane's approval; approving it now is
        // refused because the shift is Closed.
        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, employer).ApproveAsync(mikeApp.Id, CancellationToken.None));

        var db = ctx.NewContext();
        Assert.Equal(ApplicationStatus.Approved, (await db.ShiftApplications.SingleAsync(a => a.Id == janeApp.Id)).Status);
        Assert.Equal(ApplicationStatus.Rejected, (await db.ShiftApplications.SingleAsync(a => a.Id == mikeApp.Id)).Status);
        Assert.Equal(1, await db.ShiftApplications.CountAsync(a => a.Status == ApplicationStatus.Approved));
    }
}
