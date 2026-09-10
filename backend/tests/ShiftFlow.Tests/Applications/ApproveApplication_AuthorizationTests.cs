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
/// CLAUDE.md §5 / §7: approve first verifies the employer owns the shift's
/// project. An application on another employer's project is a
/// <see cref="NotFoundException"/> (404, not 403), and nothing is written — the
/// same treatment the other cross-tenant paths get.
/// </summary>
public class ApproveApplication_AuthorizationTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Employer employer) =>
        new(ctx.Db, StubCurrentUser.Employer(employer.UserId, employer.Id), new TestClock(Now));

    [Fact]
    public async Task Approving_an_application_on_another_employers_project_is_NotFound_and_writes_nothing()
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
            ServiceFor(ctx, acme).ApproveAsync(app.Id, CancellationToken.None));

        var db = ctx.NewContext();
        Assert.Equal(ApplicationStatus.Pending, (await db.ShiftApplications.SingleAsync(a => a.Id == app.Id)).Status);
        Assert.Equal(ShiftStatus.Open, (await db.Shifts.SingleAsync(s => s.Id == shift.Id)).Status);
    }

    [Fact]
    public async Task An_unknown_application_id_is_the_same_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme).ApproveAsync(9999, CancellationToken.None));
    }
}
