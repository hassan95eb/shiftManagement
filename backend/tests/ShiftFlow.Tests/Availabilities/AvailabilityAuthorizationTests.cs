using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.Availabilities.Dtos;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Availabilities;

/// <summary>
/// CLAUDE.md §7: an expert may only ever read or modify their own availability.
/// A valid Expert principal pointed at another expert's window — or at an id
/// that does not exist — fails the same way, so ids cannot be probed.
/// </summary>
public class AvailabilityAuthorizationTests
{
    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static AvailabilityService ServiceFor(SqliteTestContext ctx, int userId, int expertId) =>
        new(ctx.Db, StubCurrentUser.Expert(userId, expertId), new TestClock());

    [Fact]
    public async Task Reading_another_experts_window_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");
        var janesWindow = ctx.Db.AddAvailability(jane.Id, At(8), At(12));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, mike.UserId, mike.Id).GetAsync(janesWindow.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Reading_an_unknown_id_with_a_valid_expert_is_also_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var jane = ctx.Db.AddExpert("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, jane.UserId, jane.Id).GetAsync(9999, CancellationToken.None));
    }

    [Fact]
    public async Task List_returns_only_the_callers_windows()
    {
        using var ctx = new SqliteTestContext();
        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");
        ctx.Db.AddAvailability(jane.Id, At(8), At(10));
        ctx.Db.AddAvailability(jane.Id, At(12), At(14));
        ctx.Db.AddAvailability(mike.Id, At(9), At(11));

        var mine = await ServiceFor(ctx, jane.UserId, jane.Id).ListAsync(CancellationToken.None);

        Assert.Equal(new[] { At(8), At(12) }, mine.Select(w => w.StartUtc).ToArray());
    }

    [Fact]
    public async Task Creating_a_window_never_touches_another_experts_rows()
    {
        using var ctx = new SqliteTestContext();
        var jane = ctx.Db.AddExpert("Jane Doe");
        var mike = ctx.Db.AddExpert("Mike Roe");
        ctx.Db.AddAvailability(mike.Id, At(8), At(12));

        await ServiceFor(ctx, jane.UserId, jane.Id)
            .CreateAsync(new AvailabilityRequest { StartUtc = At(8), EndUtc = At(12) }, CancellationToken.None);

        var mikesWindows = ctx.NewContext().Availabilities.Where(a => a.ExpertId == mike.Id).ToList();
        Assert.Single(mikesWindows);
        Assert.Equal(At(8), mikesWindows[0].StartUtc);
    }
}
