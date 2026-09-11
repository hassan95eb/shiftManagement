using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Features.Attendance;
using ShiftFlow.Application.Features.Auth;
using ShiftFlow.Application.Features.Auth.Dtos;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Attendance;

public class AttendanceSessionLifecycleTests
{
    private sealed class TokenService : IJwtTokenService
    {
        public AccessToken CreateAccessToken(User user, int? supervisorId, int? callAgentId) =>
            new("token", new DateTime(2026, 7, 1, 11, 0, 0, DateTimeKind.Utc));
    }

    private sealed class ThrowingAttendanceRecorder : IAttendanceRecorder
    {
        public Task OpenForLoginAsync(int callAgentId, CancellationToken cancellationToken) =>
            throw new DbUpdateException("Transient attendance write failure.");
    }

    private static AttendanceService ServiceFor(
        SqliteTestContext ctx,
        StubCurrentUser currentUser,
        TestClock clock) =>
        new(
            ctx.Db,
            currentUser,
            new AccessScope(currentUser),
            clock,
            Options.Create(new AttendanceOptions()));

    [Fact]
    public async Task Login_opens_then_reuses_the_current_committed_shift_session()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Assigned, agent.Id);
        var clock = new TestClock(now);
        var service = ServiceFor(ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id), clock);

        await service.OpenForLoginAsync(agent.Id, CancellationToken.None);
        clock.UtcNow = now.AddMinutes(1);
        await service.OpenForLoginAsync(agent.Id, CancellationToken.None);

        var stored = await ctx.NewContext().AttendanceSessions.SingleAsync();
        Assert.Equal(shift.Id, stored.ShiftId);
        Assert.Equal(now, stored.StartedAtUtc);
        Assert.Equal(now.AddMinutes(1), stored.LastSeenUtc);
    }

    [Fact]
    public async Task Successful_auth_login_opens_the_current_committed_shift_session()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        agent.User.PasswordHash = FakePasswordHasher.Prefix + "secret";
        ctx.Db.SaveChanges();
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Assigned, agent.Id);
        var currentUser = StubCurrentUser.CallAgent(agent.UserId, agent.Id);
        var attendance = ServiceFor(ctx, currentUser, new TestClock(now));
        var auth = new AuthService(ctx.Db, new FakePasswordHasher(), new TokenService(), attendance);

        var result = await auth.LoginAsync(
            new LoginRequest { Username = agent.User.Username, Password = "secret" },
            CancellationToken.None);

        Assert.Equal("token", result.AccessToken);
        var stored = await ctx.NewContext().AttendanceSessions.SingleAsync();
        Assert.Equal(shift.Id, stored.ShiftId);
        Assert.Equal(now, stored.StartedAtUtc);
    }

    [Fact]
    public async Task Attendance_write_failure_does_not_fail_a_valid_login()
    {
        using var ctx = new SqliteTestContext();
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        agent.User.PasswordHash = FakePasswordHasher.Prefix + "secret";
        ctx.Db.SaveChanges();
        var auth = new AuthService(
            ctx.Db,
            new FakePasswordHasher(),
            new TokenService(),
            new ThrowingAttendanceRecorder());

        var result = await auth.LoginAsync(
            new LoginRequest { Username = agent.User.Username, Password = "secret" },
            CancellationToken.None);

        Assert.Equal("token", result.AccessToken);
        Assert.Equal(agent.Id, result.CallAgentId);
    }

    [Fact]
    public async Task Login_opens_a_session_for_an_approved_application_commitment()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Closed);
        ctx.Db.AddApplication(shift.Id, agent.Id, ApplicationStatus.Approved);
        var service = ServiceFor(
            ctx,
            StubCurrentUser.CallAgent(agent.UserId, agent.Id),
            new TestClock(now));

        await service.OpenForLoginAsync(agent.Id, CancellationToken.None);

        Assert.Equal(shift.Id, (await ctx.NewContext().AttendanceSessions.SingleAsync()).ShiftId);
    }

    [Fact]
    public async Task Heartbeat_refreshes_and_logout_closes_the_open_session()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAttendanceSession(agent.Id, shift.Id, now.AddMinutes(-10), now.AddMinutes(-5));
        var clock = new TestClock(now);
        var service = ServiceFor(ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id), clock);

        await service.HeartbeatAsync(CancellationToken.None);
        clock.UtcNow = now.AddMinutes(1);
        await service.LogoutAsync(CancellationToken.None);

        var stored = await ctx.NewContext().AttendanceSessions.SingleAsync();
        Assert.Equal(now, stored.LastSeenUtc);
        Assert.Equal(now.AddMinutes(1), stored.EndedAtUtc);
    }

    [Fact]
    public async Task Heartbeat_opens_the_current_shift_without_touching_an_old_open_session()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 2, 10, 0, 0, DateTimeKind.Utc);
        var oldShift = ctx.Db.AddShift(
            project.Id, now.AddDays(-1).AddHours(-1), now.AddDays(-1).AddHours(1),
            ShiftStatus.Assigned, agent.Id);
        var currentShift = ctx.Db.AddShift(
            project.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Assigned, agent.Id);
        var oldLastSeen = now.AddDays(-1);
        ctx.Db.AddAttendanceSession(
            agent.Id, oldShift.Id, oldShift.StartUtc, oldLastSeen);
        var service = ServiceFor(
            ctx,
            StubCurrentUser.CallAgent(agent.UserId, agent.Id),
            new TestClock(now));

        await service.HeartbeatAsync(CancellationToken.None);

        var sessions = await ctx.NewContext().AttendanceSessions.OrderBy(s => s.ShiftId).ToListAsync();
        Assert.Equal(2, sessions.Count);
        Assert.Equal(oldLastSeen, sessions.Single(s => s.ShiftId == oldShift.Id).LastSeenUtc);
        Assert.Equal(now, sessions.Single(s => s.ShiftId == currentShift.Id).LastSeenUtc);
    }

    [Fact]
    public async Task Heartbeat_without_a_current_committed_shift_writes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var service = ServiceFor(
            ctx,
            StubCurrentUser.CallAgent(agent.UserId, agent.Id),
            new TestClock(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc)));

        await service.HeartbeatAsync(CancellationToken.None);

        Assert.False(await ctx.NewContext().AttendanceSessions.AnyAsync());
    }

    [Fact]
    public async Task Login_outside_a_committed_shift_does_not_open_a_session()
    {
        using var ctx = new SqliteTestContext();
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var clock = new TestClock(new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc));
        var service = ServiceFor(ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id), clock);

        await service.OpenForLoginAsync(agent.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().AttendanceSessions.AnyAsync());
    }

    [Fact]
    public async Task Released_shift_does_not_open_a_session_for_its_former_assignee()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = new DateTime(2026, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        ctx.Db.AddShift(
            project.Id, now.AddHours(-1), now.AddHours(1), ShiftStatus.Released, agent.Id);
        var service = ServiceFor(
            ctx,
            StubCurrentUser.CallAgent(agent.UserId, agent.Id),
            new TestClock(now));

        await service.OpenForLoginAsync(agent.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().AttendanceSessions.AnyAsync());
    }
}
