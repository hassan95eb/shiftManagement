using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.Attendance;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Attendance;

public class ActiveAttendanceTests
{
    private static AttendanceService ServiceFor(
        SqliteTestContext ctx,
        StubCurrentUser currentUser,
        TestClock clock,
        int thresholdSeconds = 120) =>
        new(
            ctx.Db,
            currentUser,
            new AccessScope(currentUser),
            clock,
            Options.Create(new AttendanceOptions { StalenessThresholdSeconds = thresholdSeconds }));

    [Fact]
    public async Task Session_older_than_fixed_clock_threshold_is_excluded()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-2), now.AddHours(2), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAttendanceSession(agent.Id, shift.Id, now.AddHours(-2), now.AddSeconds(-121));

        var result = await ServiceFor(
            ctx,
            StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id),
            new TestClock(now)).GetActiveAsync(CancellationToken.None);

        Assert.Equal(0, result.Count);
        Assert.Empty(result.Agents);
        Assert.Null(ctx.Db.AttendanceSessions.Single().EndedAtUtc);
    }

    [Fact]
    public async Task Supervisor_is_scoped_while_manager_sees_all_fresh_active_agents()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Support");
        var globexProject = ctx.Db.AddProject(globex.Id, "Care");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var mike = ctx.Db.AddCallAgent("Mike Roe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var janeShift = ctx.Db.AddShift(acmeProject.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Assigned, jane.Id);
        var mikeShift = ctx.Db.AddShift(globexProject.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Assigned, mike.Id);
        ctx.Db.AddAttendanceSession(jane.Id, janeShift.Id, now.AddMinutes(-30), now.AddSeconds(-30));
        ctx.Db.AddAttendanceSession(mike.Id, mikeShift.Id, now.AddMinutes(-20), now.AddSeconds(-20));

        var clock = new TestClock(now);
        var scoped = await ServiceFor(
            ctx, StubCurrentUser.Supervisor(acme.UserId, acme.Id), clock)
            .GetActiveAsync(CancellationToken.None);
        var global = await ServiceFor(ctx, StubCurrentUser.Manager(999), clock)
            .GetActiveAsync(CancellationToken.None);

        Assert.Equal([jane.Id], scoped.Agents.Select(a => a.CallAgentId));
        Assert.Equal(2, global.Count);
    }

    [Fact]
    public async Task Fresh_session_outside_its_shift_window_is_excluded()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-2), now, ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAttendanceSession(agent.Id, shift.Id, now.AddHours(-2), now);

        var result = await ServiceFor(
            ctx,
            StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id),
            new TestClock(now)).GetActiveAsync(CancellationToken.None);

        Assert.Empty(result.Agents);
    }
}
