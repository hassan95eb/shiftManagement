using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.Availabilities.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Availabilities;

/// <summary>
/// Update-time merge, and the approved-shift coverage guard that makes a
/// resize refuse the same way a delete does (CLAUDE.md §5).
/// </summary>
public class UpdateAvailabilityTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static AvailabilityService ServiceFor(SqliteTestContext ctx, int userId, int callAgentId) =>
        new(ctx.Db, StubCurrentUser.CallAgent(userId, callAgentId), new TestClock(Now));

    private static AvailabilityRequest Request(int startHour, int endHour) =>
        new() { StartUtc = At(startHour), EndUtc = At(endHour) };

    private static (DateTime StartUtc, DateTime EndUtc)[] StoredWindows(SqliteTestContext ctx, int callAgentId) =>
        ctx.NewContext().Availabilities
            .Where(a => a.CallAgentId == callAgentId)
            .OrderBy(a => a.StartUtc)
            .Select(a => new { a.StartUtc, a.EndUtc })
            .AsEnumerable()
            .Select(a => (a.StartUtc, a.EndUtc))
            .ToArray();

    [Fact]
    public async Task Extending_a_window_until_it_touches_a_neighbour_merges_the_two()
    {
        using var ctx = new SqliteTestContext();
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        var morning = ctx.Db.AddAvailability(callAgent.Id, At(8), At(10));
        ctx.Db.AddAvailability(callAgent.Id, At(14), At(16));

        var result = await ServiceFor(ctx, callAgent.UserId, callAgent.Id)
            .UpdateAsync(morning.Id, Request(8, 14), CancellationToken.None);

        Assert.Equal([(At(8), At(16))], StoredWindows(ctx, callAgent.Id));
        Assert.Equal(At(8), result.StartUtc);
        Assert.Equal(At(16), result.EndUtc);
    }

    [Fact]
    public async Task Shrinking_a_window_that_still_covers_its_approved_shift_is_allowed()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(9), At(12));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, callAgent.Id, ApplicationStatus.Approved);
        var window = ctx.Db.AddAvailability(callAgent.Id, At(6), At(20));

        await ServiceFor(ctx, callAgent.UserId, callAgent.Id)
            .UpdateAsync(window.Id, Request(8, 13), CancellationToken.None);

        Assert.Equal([(At(8), At(13))], StoredWindows(ctx, callAgent.Id));
    }

    [Fact]
    public async Task Resizing_a_window_so_it_no_longer_covers_an_approved_shift_is_blocked()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, callAgent.Id, ApplicationStatus.Approved);
        var window = ctx.Db.AddAvailability(callAgent.Id, At(6), At(20));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, callAgent.UserId, callAgent.Id)
                .UpdateAsync(window.Id, Request(6, 12), CancellationToken.None));

        Assert.Equal([(At(6), At(20))], StoredWindows(ctx, callAgent.Id));
    }

    [Fact]
    public async Task Updating_another_call_agents_window_is_NotFound_and_changes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var mike = ctx.Db.AddCallAgent("Mike Roe");
        var janesWindow = ctx.Db.AddAvailability(jane.Id, At(8), At(12));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, mike.UserId, mike.Id)
                .UpdateAsync(janesWindow.Id, Request(8, 18), CancellationToken.None));

        Assert.Equal([(At(8), At(12))], StoredWindows(ctx, jane.Id));
    }
}
