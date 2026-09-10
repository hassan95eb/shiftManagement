using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Applications.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// <c>GET /api/applications</c>. An expert sees only their own applications; an
/// employer sees only the applications on shifts of their own projects. Neither
/// role can see the other's rows and the <c>shiftId</c> / <c>status</c> filters
/// only narrow within that scope (CLAUDE.md §7). Newest applied first.
/// </summary>
public class ApplicationHistoryTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task An_expert_sees_only_their_own_applications_newest_first()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");

        var s1 = ctx.Db.AddShift(project.Id, At(8), At(16));
        var s2 = ctx.Db.AddShift(project.Id, At(16), At(20));
        var s3 = ctx.Db.AddShift(project.Id, At(20), At(23));

        ctx.Db.AddApplication(s1.Id, jane.Id, ApplicationStatus.Pending, On(1));
        ctx.Db.AddApplication(s2.Id, jane.Id, ApplicationStatus.Approved, On(3));
        ctx.Db.AddApplication(s3.Id, mike.Id, ApplicationStatus.Pending, On(2));

        var svc = new ApplicationService(ctx.Db, StubCurrentUser.Expert(jane.UserId, jane.Id), new TestClock(Now));
        var mine = await svc.ListAsync(new ApplicationListFilter(), CancellationToken.None);

        Assert.Equal(new[] { s2.Id, s1.Id }, mine.Select(a => a.ShiftId).ToArray()); // On(3) before On(1)
        Assert.All(mine, a => Assert.Equal(jane.Id, a.ExpertId));
    }

    [Fact]
    public async Task An_employer_sees_applications_on_their_own_shifts_only()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Acme Support");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var jane = ctx.Db.AddExpert("Jane Doe");

        var acmeShift = ctx.Db.AddShift(acmeProject.Id, At(8), At(16));
        var globexShift = ctx.Db.AddShift(globexProject.Id, At(8), At(16));
        ctx.Db.AddApplication(acmeShift.Id, jane.Id, ApplicationStatus.Pending, On(1));
        ctx.Db.AddApplication(globexShift.Id, jane.Id, ApplicationStatus.Pending, On(2));

        var svc = new ApplicationService(ctx.Db, StubCurrentUser.Employer(acme.UserId, acme.Id), new TestClock(Now));
        var seen = await svc.ListAsync(new ApplicationListFilter(), CancellationToken.None);

        Assert.Equal(new[] { acmeShift.Id }, seen.Select(a => a.ShiftId).ToArray());
    }

    [Fact]
    public async Task An_employer_can_filter_by_shift_and_status()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");

        var shiftA = ctx.Db.AddShift(project.Id, At(8), At(16));
        var shiftB = ctx.Db.AddShift(project.Id, At(16), At(23));
        ctx.Db.AddApplication(shiftA.Id, jane.Id, ApplicationStatus.Approved, On(1));
        ctx.Db.AddApplication(shiftA.Id, mike.Id, ApplicationStatus.Rejected, On(2));
        ctx.Db.AddApplication(shiftB.Id, jane.Id, ApplicationStatus.Pending, On(3));

        var svc = new ApplicationService(ctx.Db, StubCurrentUser.Employer(acme.UserId, acme.Id), new TestClock(Now));

        var forShiftA = await svc.ListAsync(new ApplicationListFilter { ShiftId = shiftA.Id }, CancellationToken.None);
        Assert.Equal(2, forShiftA.Count);
        Assert.All(forShiftA, a => Assert.Equal(shiftA.Id, a.ShiftId));

        var approvedOnShiftA = await svc.ListAsync(
            new ApplicationListFilter { ShiftId = shiftA.Id, Status = ApplicationStatus.Approved },
            CancellationToken.None);
        Assert.Single(approvedOnShiftA);
        Assert.Equal(jane.Id, approvedOnShiftA[0].ExpertId);
    }

    [Fact]
    public async Task An_employer_filtering_by_another_employers_shift_sees_nothing()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var jane = ctx.Db.AddExpert("Jane Doe");
        var globexShift = ctx.Db.AddShift(globexProject.Id, At(8), At(16));
        ctx.Db.AddApplication(globexShift.Id, jane.Id, ApplicationStatus.Pending, On(1));

        var svc = new ApplicationService(ctx.Db, StubCurrentUser.Employer(acme.UserId, acme.Id), new TestClock(Now));
        var seen = await svc.ListAsync(new ApplicationListFilter { ShiftId = globexShift.Id }, CancellationToken.None);

        Assert.Empty(seen);
    }
}
