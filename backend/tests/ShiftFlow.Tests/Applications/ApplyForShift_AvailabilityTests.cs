using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.Applications;
using ShiftFlow.Application.Features.Availabilities;
using ShiftFlow.Application.Features.Availabilities.Dtos;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Applications;

/// <summary>
/// Apply rule 3 (the assignment's required test 1): the whole shift must fall
/// inside a single availability window. Includes the case the phase-6 merge
/// exists for — a shift spanning what used to be two adjacent windows is
/// accepted, because those windows are now stored as one.
/// </summary>
public class ApplyForShift_AvailabilityTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static DateTime At(int hour) => new(2026, 7, 1, hour, 0, 0, DateTimeKind.Utc);

    private static ApplicationService ApplyService(SqliteTestContext ctx, int userId, int callAgentId) =>
        new(ctx.Db, StubCurrentUser.CallAgent(userId, callAgentId), new TestClock(Now));

    private static AvailabilityService AvailabilityServiceFor(SqliteTestContext ctx, int userId, int callAgentId) =>
        new(ctx.Db, StubCurrentUser.CallAgent(userId, callAgentId), new TestClock(Now));

    [Fact]
    public async Task A_window_that_covers_the_whole_shift_lets_the_application_through()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(8), At(16)); // exact cover, endpoints included

        var created = await ApplyService(ctx, callAgent.UserId, callAgent.Id)
            .ApplyAsync(shift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }

    [Fact]
    public async Task A_window_that_only_partly_covers_the_shift_is_rejected()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddAvailability(callAgent.Id, At(8), At(14)); // ends two hours short

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ApplyService(ctx, callAgent.UserId, callAgent.Id).ApplyAsync(shift.Id, CancellationToken.None));

        Assert.False(await ctx.NewContext().ShiftApplications.AnyAsync());
    }

    [Fact]
    public async Task No_availability_at_all_is_rejected()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ApplyService(ctx, callAgent.UserId, callAgent.Id).ApplyAsync(shift.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Two_windows_that_only_together_span_the_shift_but_do_not_touch_are_rejected()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(8), At(16));
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        // A real gap between them (12:00–13:00), so no single window covers the shift.
        ctx.Db.AddAvailability(callAgent.Id, At(8), At(12));
        ctx.Db.AddAvailability(callAgent.Id, At(13), At(16));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ApplyService(ctx, callAgent.UserId, callAgent.Id).ApplyAsync(shift.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_shift_spanning_a_merged_availability_boundary_is_accepted()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id, At(10), At(14)); // straddles the 12:00 boundary
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);

        // Declared as two adjacent windows; phase 6 merges them on write.
        var availability = AvailabilityServiceFor(ctx, callAgent.UserId, callAgent.Id);
        await availability.CreateAsync(new AvailabilityRequest { StartUtc = At(8), EndUtc = At(12) }, CancellationToken.None);
        await availability.CreateAsync(new AvailabilityRequest { StartUtc = At(12), EndUtc = At(16) }, CancellationToken.None);

        var stored = await ctx.NewContext().Availabilities
            .Where(a => a.CallAgentId == callAgent.Id)
            .ToListAsync();
        Assert.Single(stored); // one 08:00–16:00 window, not two
        Assert.Equal(At(8), stored[0].StartUtc);
        Assert.Equal(At(16), stored[0].EndUtc);

        var created = await ApplyService(ctx, callAgent.UserId, callAgent.Id)
            .ApplyAsync(shift.Id, CancellationToken.None);

        Assert.Equal("Pending", created.Status);
    }
}
