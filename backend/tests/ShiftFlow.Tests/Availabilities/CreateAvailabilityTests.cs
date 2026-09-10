using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.Availabilities.Dtos;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Availabilities;

/// <summary>
/// Create-time merge (CLAUDE.md §5, docs/01 §3-6): the database must never end
/// up holding two windows that overlap or touch, and the stored state must be
/// the same whichever order the windows were posted in.
/// </summary>
public class CreateAvailabilityTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static AvailabilityService ServiceFor(SqliteTestContext ctx, int userId, int expertId) =>
        new(ctx.Db, StubCurrentUser.Expert(userId, expertId), new TestClock(Now));

    private static AvailabilityRequest Request(int startHour, int endHour) =>
        new() { StartUtc = At(startHour), EndUtc = At(endHour) };

    private static (DateTime StartUtc, DateTime EndUtc)[] StoredWindows(SqliteTestContext ctx, int expertId) =>
        ctx.NewContext().Availabilities
            .Where(a => a.ExpertId == expertId)
            .OrderBy(a => a.StartUtc)
            .Select(a => new { a.StartUtc, a.EndUtc })
            .AsEnumerable()
            .Select(a => (a.StartUtc, a.EndUtc))
            .ToArray();

    [Fact]
    public async Task Creating_a_first_window_stores_and_returns_it()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");

        var created = await ServiceFor(ctx, expert.UserId, expert.Id)
            .CreateAsync(Request(8, 12), CancellationToken.None);

        Assert.Equal(At(8), created.StartUtc);
        Assert.Equal(At(12), created.EndUtc);
        Assert.Equal(Now, created.CreatedAtUtc);
        Assert.Equal([(At(8), At(12))], StoredWindows(ctx, expert.Id));
    }

    [Fact]
    public async Task Posting_adjacent_windows_forward_order_stores_one_merged_window()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        var service = ServiceFor(ctx, expert.UserId, expert.Id);

        await service.CreateAsync(Request(8, 12), CancellationToken.None);
        var second = await service.CreateAsync(Request(12, 16), CancellationToken.None);

        Assert.Equal([(At(8), At(16))], StoredWindows(ctx, expert.Id));
        Assert.Equal(At(8), second.StartUtc);
        Assert.Equal(At(16), second.EndUtc);
    }

    [Fact]
    public async Task Posting_adjacent_windows_reverse_order_stores_the_same_single_window()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        var service = ServiceFor(ctx, expert.UserId, expert.Id);

        await service.CreateAsync(Request(12, 16), CancellationToken.None);
        await service.CreateAsync(Request(8, 12), CancellationToken.None);

        Assert.Equal([(At(8), At(16))], StoredWindows(ctx, expert.Id));
    }

    [Fact]
    public async Task Posting_an_overlapping_window_merges_it_into_the_span()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        var service = ServiceFor(ctx, expert.UserId, expert.Id);

        await service.CreateAsync(Request(8, 13), CancellationToken.None);
        await service.CreateAsync(Request(11, 16), CancellationToken.None);

        Assert.Equal([(At(8), At(16))], StoredWindows(ctx, expert.Id));
    }

    [Fact]
    public async Task A_window_spanning_several_existing_ones_collapses_them_to_one()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.AddAvailability(expert.Id, At(9), At(10));
        ctx.Db.AddAvailability(expert.Id, At(11), At(12));
        ctx.Db.AddAvailability(expert.Id, At(13), At(14));

        await ServiceFor(ctx, expert.UserId, expert.Id)
            .CreateAsync(Request(7, 18), CancellationToken.None);

        Assert.Equal([(At(7), At(18))], StoredWindows(ctx, expert.Id));
    }

    [Fact]
    public async Task A_non_adjacent_window_is_stored_alongside_the_existing_one()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        var service = ServiceFor(ctx, expert.UserId, expert.Id);

        await service.CreateAsync(Request(8, 10), CancellationToken.None);
        await service.CreateAsync(Request(14, 16), CancellationToken.None);

        Assert.Equal([(At(8), At(10)), (At(14), At(16))], StoredWindows(ctx, expert.Id));
    }

    [Fact]
    public async Task A_window_already_inside_an_existing_one_writes_nothing_and_returns_the_host()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");
        var host = ctx.Db.AddAvailability(expert.Id, At(8), At(18));

        var returned = await ServiceFor(ctx, expert.UserId, expert.Id)
            .CreateAsync(Request(10, 12), CancellationToken.None);

        Assert.Equal(host.Id, returned.Id);
        Assert.Equal([(At(8), At(18))], StoredWindows(ctx, expert.Id));
    }
}
