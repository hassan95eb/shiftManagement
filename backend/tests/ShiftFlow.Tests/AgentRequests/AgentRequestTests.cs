using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.AgentRequests;
using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Application.Features.Ratings;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Infrastructure;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class AgentRequestTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static AgentRequestService Service(
        SqliteTestContext ctx,
        ICurrentUser user,
        DateTime? now = null,
        decimal cap = 8m) =>
        new(
            ctx.Db,
            user,
            new AccessScope(user),
            new TestClock(now ?? Now),
            new PersianLeaveYear(),
            Options.Create(new RatingOptions { DowntimeCapHours = cap }));

    [Fact]
    public void Tehran_0030_on_first_Farvardin_is_in_the_new_leave_year()
    {
        var leaveYear = new PersianLeaveYear();
        var instant = new DateTime(2026, 3, 20, 21, 0, 0, DateTimeKind.Utc); // 1405-01-01 00:30 Tehran

        var range = leaveYear.Resolve(instant);

        Assert.Equal(new DateTime(2026, 3, 20, 20, 30, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.True(instant >= range.StartUtc && instant < range.EndUtc);
        Assert.NotEqual(leaveYear.Resolve(range.StartUtc.AddSeconds(-1)), range);
    }

    [Fact]
    public async Task Balance_counts_leave_by_Tehran_year_even_when_the_UTC_date_is_previous_day()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var farvardin0030 = new DateTime(2026, 3, 20, 21, 0, 0, DateTimeKind.Utc);
        var first = ctx.Db.AddShift(project.Id, farvardin0030, farvardin0030.AddHours(8), ShiftStatus.Assigned, agent.Id);
        var second = ctx.Db.AddShift(project.Id, farvardin0030.AddDays(1), farvardin0030.AddDays(1).AddHours(8), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, first.Id, AgentRequestType.Leave, AgentRequestStatus.Approved, first.StartUtc, first.EndUtc, farvardin0030);

        var result = await Service(ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id), farvardin0030)
            .CreateAsync(new CreateAgentRequest
            {
                ShiftId = second.Id,
                RequestType = "Leave",
                StartUtc = second.StartUtc,
                EndUtc = second.EndUtc,
            }, CancellationToken.None);

        Assert.Equal(25, result.RemainingLeaveDays);
    }

    [Fact]
    public async Task Leave_approval_rechecks_balance_and_returns_conflict_when_exhausted()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        agent.AnnualLeaveDays = 1;
        ctx.Db.SaveChanges();
        var usedShift = ctx.Db.AddShift(project.Id, Now.AddDays(-2), Now.AddDays(-2).AddHours(8), ShiftStatus.Assigned, agent.Id);
        var pendingShift = ctx.Db.AddShift(project.Id, Now.AddDays(1), Now.AddDays(1).AddHours(8), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, usedShift.Id, AgentRequestType.Leave, AgentRequestStatus.Approved, usedShift.StartUtc, usedShift.EndUtc);
        var pending = ctx.Db.AddAgentRequest(agent.Id, pendingShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, pendingShift.StartUtc, pendingShift.EndUtc);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            Service(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
                .ApproveAsync(pending.Id, CancellationToken.None));

        Assert.Equal(AgentRequestStatus.Pending, (await ctx.NewContext().AgentRequests.SingleAsync(r => r.Id == pending.Id)).Status);
    }

    [Fact]
    public async Task Approving_leave_releases_assigned_shift_in_the_same_write()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var shift = ctx.Db.AddShift(project.Id, Now.AddDays(1), Now.AddDays(1).AddHours(8), ShiftStatus.Assigned, agent.Id);
        var request = ctx.Db.AddAgentRequest(agent.Id, shift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, shift.StartUtc, shift.EndUtc);

        var result = await Service(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
            .ApproveAsync(request.Id, CancellationToken.None);

        var stored = ctx.NewContext();
        Assert.Equal("Approved", result.Status);
        Assert.Equal(ShiftStatus.Released, (await stored.Shifts.SingleAsync(s => s.Id == shift.Id)).Status);
    }

    [Fact]
    public async Task Downtime_outside_shift_window_is_bad_request_validation()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var shift = ctx.Db.AddShift(project.Id, Now.AddHours(-4), Now.AddHours(4), ShiftStatus.Assigned, agent.Id);

        await Assert.ThrowsAsync<ValidationException>(() =>
            Service(ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id)).CreateAsync(
                new CreateAgentRequest
                {
                    ShiftId = shift.Id,
                    RequestType = "Downtime",
                    StartUtc = shift.StartUtc.AddMinutes(-1),
                    EndUtc = shift.StartUtc.AddHours(1),
                }, CancellationToken.None));
    }

    [Fact]
    public async Task Downtime_beyond_monthly_cap_is_a_conflict()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var firstShift = ctx.Db.AddShift(project.Id, Now.AddHours(-8), Now, ShiftStatus.Assigned, agent.Id);
        var secondShift = ctx.Db.AddShift(project.Id, Now.AddHours(-4), Now.AddHours(4), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, firstShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, firstShift.StartUtc, firstShift.StartUtc.AddHours(7));
        var pending = ctx.Db.AddAgentRequest(agent.Id, secondShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending, secondShift.StartUtc, secondShift.StartUtc.AddHours(2));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            Service(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
                .ApproveAsync(pending.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData("2026-03-20T20:30:00Z")] // 1405-01-01, Nowruz
    [InlineData("2026-04-20T20:30:00Z")] // 1405-02-01, ordinary Jalali month boundary
    public async Task Downtime_crossing_a_Jalali_month_boundary_is_split_between_months(string boundaryText)
    {
        using var ctx = new SqliteTestContext();
        var boundary = DateTime.Parse(
            boundaryText,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var priorShift = ctx.Db.AddShift(
            project.Id, boundary.AddHours(-8), boundary.AddHours(-1), ShiftStatus.Assigned, agent.Id);
        var crossingShift = ctx.Db.AddShift(
            project.Id, boundary.AddHours(-1), boundary.AddHours(1), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(
            agent.Id, priorShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved,
            priorShift.StartUtc, priorShift.EndUtc);
        var pending = ctx.Db.AddAgentRequest(
            agent.Id, crossingShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending,
            crossingShift.StartUtc, crossingShift.EndUtc);

        var result = await Service(
                ctx,
                StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id),
                crossingShift.EndUtc)
            .ApproveAsync(pending.Id, CancellationToken.None);

        Assert.Equal("Approved", result.Status);
    }

    [Fact]
    public async Task Database_index_rejects_two_racing_approved_leaves_on_one_shift()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var mike = ctx.Db.AddCallAgent("Mike Roe");
        var shift = ctx.Db.AddShift(project.Id, Now, Now.AddHours(8));

        using var first = ctx.NewContext();
        using var second = ctx.NewContext();
        first.AgentRequests.Add(Request(jane.Id));
        second.AgentRequests.Add(Request(mike.Id));

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        AgentRequest Request(int callAgentId) => new()
        {
            CallAgentId = callAgentId,
            ShiftId = shift.Id,
            RequestType = AgentRequestType.Leave,
            Status = AgentRequestStatus.Approved,
            RequestedAtUtc = Now,
            StartUtc = shift.StartUtc,
            EndUtc = shift.EndUtc,
        };
    }

    [Fact]
    public async Task Multiple_approved_downtime_rows_on_one_shift_are_legal()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var shift = ctx.Db.AddShift(project.Id, Now, Now.AddHours(8));

        ctx.Db.AddAgentRequest(agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, Now, Now.AddHours(1));
        ctx.Db.AddAgentRequest(agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, Now.AddHours(2), Now.AddHours(3));

        Assert.Equal(2, await ctx.Db.AgentRequests.CountAsync());
    }

    [Fact]
    public async Task Request_list_is_scoped_for_call_agent_supervisor_and_manager()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var other = ctx.Db.AddSupervisor("Other");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Support");
        var otherProject = ctx.Db.AddProject(other.Id, "Sales");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var mike = ctx.Db.AddCallAgent("Mike Roe");
        var janeShift = ctx.Db.AddShift(acmeProject.Id, Now, Now.AddHours(8));
        var mikeShift = ctx.Db.AddShift(otherProject.Id, Now, Now.AddHours(8));
        ctx.Db.AddAgentRequest(jane.Id, janeShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, janeShift.StartUtc, janeShift.EndUtc);
        ctx.Db.AddAgentRequest(mike.Id, mikeShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, mikeShift.StartUtc, mikeShift.EndUtc);

        var filter = new AgentRequestListFilter();
        var own = await Service(ctx, StubCurrentUser.CallAgent(jane.UserId, jane.Id)).ListAsync(filter, CancellationToken.None);
        var supervised = await Service(ctx, StubCurrentUser.Supervisor(acme.UserId, acme.Id)).ListAsync(filter, CancellationToken.None);
        var all = await Service(ctx, StubCurrentUser.Manager(999)).ListAsync(filter, CancellationToken.None);

        Assert.Single(own);
        Assert.Equal(jane.Id, own[0].CallAgentId);
        Assert.Single(supervised);
        Assert.Equal(jane.Id, supervised[0].CallAgentId);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Pending_leave_in_list_carries_current_balance()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        agent.AnnualLeaveDays = 2;
        ctx.Db.SaveChanges();
        var usedShift = ctx.Db.AddShift(project.Id, Now.AddDays(-2), Now.AddDays(-2).AddHours(8));
        var pendingShift = ctx.Db.AddShift(project.Id, Now.AddDays(1), Now.AddDays(1).AddHours(8));
        ctx.Db.AddAgentRequest(agent.Id, usedShift.Id, AgentRequestType.Leave, AgentRequestStatus.Approved, usedShift.StartUtc, usedShift.EndUtc);
        ctx.Db.AddAgentRequest(agent.Id, pendingShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, pendingShift.StartUtc, pendingShift.EndUtc);

        var rows = await Service(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
            .ListAsync(new AgentRequestListFilter { Status = AgentRequestStatus.Pending }, CancellationToken.None);

        var pending = Assert.Single(rows);
        Assert.Equal(1, pending.RemainingLeaveDays);
        Assert.Equal("Pending", pending.Status);
    }

    [Fact]
    public async Task Downtime_database_failure_is_not_reported_as_duplicate_leave()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var shift = ctx.Db.AddShift(
            project.Id, Now.AddHours(-4), Now.AddHours(4), ShiftStatus.Assigned, agent.Id);
        var request = ctx.Db.AddAgentRequest(
            agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending,
            shift.StartUtc, shift.StartUtc.AddHours(1));

        var invalidDecider = StubCurrentUser.Supervisor(999_999, supervisor.Id);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            Service(ctx, invalidDecider).ApproveAsync(request.Id, CancellationToken.None));
    }
}
