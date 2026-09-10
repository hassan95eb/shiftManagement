using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// CLAUDE.md §5: reject affects only the one application. The shift stays Open
/// and the other applications are left as they were. Ownership is enforced the
/// same way as approve — another employer's application is a 404.
/// </summary>
public class RejectApplicationTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Employer employer) =>
        new(ctx.Db, StubCurrentUser.Employer(employer.UserId, employer.Id), new TestClock(Now));

    [Fact]
    public async Task Rejecting_one_application_leaves_the_shift_open_and_the_others_untouched()
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

        var result = await ServiceFor(ctx, employer).RejectAsync(janeApp.Id, CancellationToken.None);

        Assert.Equal("Rejected", result.Status);
        Assert.Equal(employer.UserId, result.DecidedByUserId);
        Assert.Equal(Now, result.DecidedAtUtc);

        var db = ctx.NewContext();
        Assert.Equal(ShiftStatus.Open, (await db.Shifts.SingleAsync()).Status);

        var mikeStored = await db.ShiftApplications.SingleAsync(a => a.Id == mikeApp.Id);
        Assert.Equal(ApplicationStatus.Pending, mikeStored.Status);
        Assert.Null(mikeStored.DecidedByUserId);
        Assert.Null(mikeStored.DecidedAtUtc);
    }

    [Fact]
    public async Task Rejecting_an_application_on_another_employers_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var jane = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(jane.Id, globexProject.Id);
        var shift = ctx.Db.AddShift(globexProject.Id, At(8), At(16));
        var app = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme).RejectAsync(app.Id, CancellationToken.None));

        Assert.Equal(
            ApplicationStatus.Pending,
            (await ctx.NewContext().ShiftApplications.SingleAsync(a => a.Id == app.Id)).Status);
    }
}
