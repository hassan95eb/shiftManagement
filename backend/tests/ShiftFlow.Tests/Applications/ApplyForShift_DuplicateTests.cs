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
/// Apply rule 4: no duplicate application. Enforced in the service by an
/// existence check, not by waiting for <c>UNIQUE(ShiftId, CallAgentId)</c> to throw.
/// A prior row blocks a re-apply whatever its status — the unique index would
/// reject the insert regardless, so a Rejected applicant cannot silently
/// re-apply.
/// </summary>
public class ApplyForShift_DuplicateTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static ApplicationService ServiceFor(SqliteTestContext ctx, int userId, int callAgentId) =>
        new(ctx.Db, StubCurrentUser.CallAgent(userId, callAgentId), new TestClock(Now));

    [Fact]
    public async Task A_first_application_succeeds()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(6), At(20));

        var created = await ServiceFor(ctx, callAgent.UserId, callAgent.Id)
            .ApplyAsync(shift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task A_second_application_to_the_same_shift_is_blocked()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(6), At(20));

        var svc = ServiceFor(ctx, callAgent.UserId, callAgent.Id);
        await svc.ApplyAsync(shift.Id, CancellationToken.None);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            svc.ApplyAsync(shift.Id, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().ShiftApplications.CountAsync());
    }

    [Fact]
    public async Task A_previously_rejected_applicant_cannot_re_apply()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(6), At(20));
        ctx.Db.AddApplication(shift.Id, callAgent.Id, ApplicationStatus.Rejected);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, callAgent.UserId, callAgent.Id).ApplyAsync(shift.Id, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().ShiftApplications.CountAsync());
    }
}
