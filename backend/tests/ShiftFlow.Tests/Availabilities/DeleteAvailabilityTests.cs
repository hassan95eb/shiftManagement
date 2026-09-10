using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Availabilities;

/// <summary>
/// Delete, and the guard that blocks removing the window a still-approved shift
/// depends on (CLAUDE.md §5).
/// </summary>
public class DeleteAvailabilityTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static AvailabilityService ServiceFor(SqliteTestContext ctx, int userId, int expertId) =>
        new(ctx.Db, StubCurrentUser.Expert(userId, expertId), new TestClock(Now));

    [Fact]
    public async Task Deleting_a_window_with_no_approved_shift_behind_it_removes_it()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        var window = ctx.Db.AddAvailability(expert.Id, At(8), At(12));

        await ServiceFor(ctx, expert.UserId, expert.Id)
            .DeleteAsync(window.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().Availabilities.AnyAsync(a => a.Id == window.Id));
    }

    [Fact]
    public async Task Deleting_the_window_that_covers_an_approved_shift_is_blocked()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, expert.Id, ApplicationStatus.Approved);
        var window = ctx.Db.AddAvailability(expert.Id, At(6), At(20));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, expert.UserId, expert.Id)
                .DeleteAsync(window.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().Availabilities.AnyAsync(a => a.Id == window.Id));
    }

    [Fact]
    public async Task Deleting_a_window_while_another_still_covers_the_approved_shift_is_allowed()
    {
        using var ctx = new SqliteTestContext();
        var employer = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(employer.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, expert.Id, ApplicationStatus.Approved);
        var covering = ctx.Db.AddAvailability(expert.Id, At(6), At(20));
        var unrelated = ctx.Db.AddAvailability(expert.Id, At(22), At(23));

        await ServiceFor(ctx, expert.UserId, expert.Id)
            .DeleteAsync(unrelated.Id, CancellationToken.None);

        Assert.True(await ctx.NewContext().Availabilities.AnyAsync(a => a.Id == covering.Id));
        Assert.False(await ctx.NewContext().Availabilities.AnyAsync(a => a.Id == unrelated.Id));
    }

    [Fact]
    public async Task Deleting_another_experts_window_is_NotFound_and_it_survives()
    {
        using var ctx = new SqliteTestContext();
        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");
        var janesWindow = ctx.Db.AddAvailability(jane.Id, At(8), At(12));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, mike.UserId, mike.Id)
                .DeleteAsync(janesWindow.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().Availabilities.AnyAsync(a => a.Id == janesWindow.Id));
    }
}
