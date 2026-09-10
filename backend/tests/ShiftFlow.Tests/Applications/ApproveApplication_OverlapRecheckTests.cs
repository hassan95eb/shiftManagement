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
/// CLAUDE.md §5: apply rule 5 is re-checked at approve time, not trusted from
/// apply time. If the expert has been approved for an overlapping shift since
/// they applied, the approval fails and nothing is written — the application
/// stays Pending and the shift stays Open. Half-open comparison (docs/01 §6), so
/// a shift that only touches the approved one still approves.
/// </summary>
public class ApproveApplication_OverlapRecheckTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static DateTime On(int day) => new(2026, 6, day, 12, 0, 0, DateTimeKind.Utc);

    private static ApprovalService ServiceFor(SqliteTestContext ctx, Employer employer) =>
        new(ctx.Db, StubCurrentUser.Employer(employer.UserId, employer.Id), new TestClock(Now));

    [Fact]
    public async Task Approval_fails_when_the_expert_gained_an_overlapping_approved_shift_since_applying()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var support = ctx.Db.AddProject(employer.Id, "Support");
        var billing = ctx.Db.AddProject(employer.Id, "Billing");
        var jane = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(jane.Id, support.Id);
        ctx.Db.Assign(jane.Id, billing.Id);

        var target = ctx.Db.AddShift(support.Id, At(10), At(14));
        var targetApp = ctx.Db.AddApplication(target.Id, jane.Id, ApplicationStatus.Pending, On(1));

        // Since applying, Jane was approved for a clashing 12–16 shift elsewhere.
        var clashing = ctx.Db.AddShift(billing.Id, At(12), At(16));
        ctx.Db.AddApplication(clashing.Id, jane.Id, ApplicationStatus.Approved, On(2));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, employer).ApproveAsync(targetApp.Id, CancellationToken.None));

        var db = ctx.NewContext();
        Assert.Equal(ApplicationStatus.Pending, (await db.ShiftApplications.SingleAsync(a => a.Id == targetApp.Id)).Status);
        Assert.Equal(ShiftStatus.Open, (await db.Shifts.SingleAsync(s => s.Id == target.Id)).Status);
    }

    [Fact]
    public async Task Approval_succeeds_when_the_expert_is_only_approved_for_a_touching_shift()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var jane = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);

        var target = ctx.Db.AddShift(project.Id, At(14), At(18));
        var targetApp = ctx.Db.AddApplication(target.Id, jane.Id, ApplicationStatus.Pending, On(1));

        var adjacent = ctx.Db.AddShift(project.Id, At(10), At(14)); // ends exactly when target starts
        ctx.Db.AddApplication(adjacent.Id, jane.Id, ApplicationStatus.Approved, On(2));

        var result = await ServiceFor(ctx, employer).ApproveAsync(targetApp.Id, CancellationToken.None);

        Assert.Equal("Approved", result.Status);
    }
}
