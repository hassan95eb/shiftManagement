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
/// Proves the CLAUDE.md §5 approval is one all-or-nothing transaction: when the
/// final write fails, the shift is <b>not</b> closed and the sibling rejections
/// are <b>not</b> kept.
/// </summary>
/// <remarks>
/// The only failure the SQLite harness can stage at commit time is the filtered
/// unique index <c>UX_ShiftApplications_OneApproved</c> firing. Reaching it needs
/// an Open shift that already has an Approved application for a <i>different</i>
/// expert — a state the normal flow never produces (approval closes the shift),
/// but exactly the race that index is the backstop for (docs/01 §3-8). The
/// concurrency-token path (<c>DbUpdateConcurrencyException</c>) cannot be
/// reproduced here — SQLite has no <c>rowversion</c> and the harness holds a
/// single connection — so it is verified by hand against SQL Server.
/// </remarks>
public class ApproveApplication_TransactionRollbackTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Employer employer) =>
        new(ctx.Db, StubCurrentUser.Employer(employer.UserId, employer.Id), new TestClock(Now));

    [Fact]
    public async Task A_failed_final_write_rolls_the_shift_and_the_sibling_rejections_back()
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

        // Artificial: the shift is still Open yet already has an Approved row for
        // Nora. The service's Open / Pending / overlap guards all pass for Jane,
        // so the approve reaches SaveChanges — where the filtered unique index
        // rejects a second Approved row for this shift.
        ctx.Db.AddApplication(shift.Id, nora.Id, ApplicationStatus.Approved, On(1));
        var janeApp = ctx.Db.AddApplication(shift.Id, jane.Id, ApplicationStatus.Pending, On(2));
        var mikeApp = ctx.Db.AddApplication(shift.Id, mike.Id, ApplicationStatus.Pending, On(3));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, employer).ApproveAsync(janeApp.Id, CancellationToken.None));

        var db = ctx.NewContext();
        Assert.Equal(ShiftStatus.Open, (await db.Shifts.SingleAsync(s => s.Id == shift.Id)).Status);
        Assert.Equal(ApplicationStatus.Pending, (await db.ShiftApplications.SingleAsync(a => a.Id == janeApp.Id)).Status);
        Assert.Equal(ApplicationStatus.Pending, (await db.ShiftApplications.SingleAsync(a => a.Id == mikeApp.Id)).Status);
        Assert.Null((await db.ShiftApplications.SingleAsync(a => a.Id == mikeApp.Id)).DecisionNote);
    }
}
