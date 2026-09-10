using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// Apply rule 2: the expert must be a member of the shift's project. A shift on
/// a project the caller is not assigned to — like an unknown shift id — is a
/// <see cref="NotFoundException"/> (404), never a 403, so project membership
/// cannot be probed (CLAUDE.md §7).
/// </summary>
public class ApplyForShift_ProjectMembershipTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static ApplicationService ServiceFor(SqliteTestContext ctx, int userId, int expertId) =>
        new(ctx.Db, StubCurrentUser.Expert(userId, expertId), new TestClock(Now));

    [Fact]
    public async Task An_assigned_expert_can_apply()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        ctx.Db.AddAvailability(expert.Id, At(6), At(20));

        var created = await ServiceFor(ctx, expert.UserId, expert.Id)
            .ApplyAsync(shift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
        Assert.Equal(shift.Id, created.ShiftId);
        Assert.Equal(expert.Id, created.ExpertId);
        Assert.Equal(Now, created.AppliedAtUtc);

        var stored = await ctx.NewContext().ShiftApplications.SingleAsync();
        Assert.Equal(ApplicationStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task A_non_member_expert_gets_NotFound_and_nothing_is_written()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var expert = ctx.Db.AddExpert("Jane Doe");
        // Not assigned to the project.
        ctx.Db.AddAvailability(expert.Id, At(6), At(20));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, expert.UserId, expert.Id).ApplyAsync(shift.Id, CancellationToken.None));

        Assert.False(await ctx.NewContext().ShiftApplications.AnyAsync());
    }

    [Fact]
    public async Task An_unknown_shift_id_is_the_same_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, expert.UserId, expert.Id).ApplyAsync(9999, CancellationToken.None));
    }
}
