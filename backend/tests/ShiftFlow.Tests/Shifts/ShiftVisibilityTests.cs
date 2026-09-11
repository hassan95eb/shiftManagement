using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts;
using ShiftFlow.Application.Features.Shifts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Shifts;

/// <summary>
/// CLAUDE.md §7: a shift is visible only through a project the caller owns
/// (supervisor) or is assigned to (CallAgent). A cross-supervisor shift, and a shift on
/// a project a CallAgent is not assigned to, are 404 / absent — never 403 — so
/// neither the id nor the project membership can be probed.
/// </summary>
public class ShiftVisibilityTests
{
    private static DateTime At(int day, int hour) => new(2026, 7, day, hour, 0, 0, DateTimeKind.Utc);

    private static ShiftService SupervisorService(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, StubCurrentUser.Supervisor(userId, supervisorId), new TestClock());

    private static OpenShiftService CallAgentService(SqliteTestContext ctx, int userId, int callAgentId) =>
        new(ctx.Db, StubCurrentUser.CallAgent(userId, callAgentId));

    // ---- supervisor side -----------------------------------------------------

    [Fact]
    public async Task Supervisor_list_returns_only_shifts_on_the_callers_projects()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Acme Support");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        ctx.Db.AddShift(acmeProject.Id, At(1, 8), At(1, 16));
        ctx.Db.AddShift(acmeProject.Id, At(2, 8), At(2, 16));
        ctx.Db.AddShift(globexProject.Id, At(1, 8), At(1, 16));

        var mine = await SupervisorService(ctx, acme.UserId, acme.Id)
            .ListAsync(new ShiftListFilter(), CancellationToken.None);

        Assert.All(mine, s => Assert.Equal(acmeProject.Id, s.ProjectId));
        Assert.Equal(2, mine.Count);
    }

    [Fact]
    public async Task Supervisor_reading_another_supervisors_shift_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var globexShift = ctx.Db.AddShift(globexProject.Id, At(1, 8), At(1, 16));

        await Assert.ThrowsAsync<NotFoundException>(() =>
            SupervisorService(ctx, acme.UserId, acme.Id).GetAsync(globexShift.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Supervisor_list_filters_by_project_status_and_date_range()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var support = ctx.Db.AddProject(acme.Id, "Support");
        var sales = ctx.Db.AddProject(acme.Id, "Sales");

        var early = ctx.Db.AddShift(support.Id, At(1, 8), At(1, 16));
        var mid = ctx.Db.AddShift(support.Id, At(10, 8), At(10, 16), ShiftStatus.Closed);
        var late = ctx.Db.AddShift(support.Id, At(20, 8), At(20, 16));
        var salesShift = ctx.Db.AddShift(sales.Id, At(12, 8), At(12, 16));

        var svc = SupervisorService(ctx, acme.UserId, acme.Id);

        var byProject = await svc.ListAsync(new ShiftListFilter { ProjectId = support.Id }, CancellationToken.None);
        Assert.Equal(3, byProject.Count);

        var open = await svc.ListAsync(new ShiftListFilter { Status = ShiftStatus.Open }, CancellationToken.None);
        Assert.Equal(new[] { late.Id, salesShift.Id, early.Id }, open.Select(s => s.Id).ToArray()); // newest start first

        var windowedOnSupport = await svc.ListAsync(
            new ShiftListFilter { ProjectId = support.Id, FromUtc = At(5, 0), ToUtc = At(15, 0) },
            CancellationToken.None);
        Assert.Equal(new[] { mid.Id }, windowedOnSupport.Select(s => s.Id).ToArray());
    }

    // ---- CallAgent side -----------------------------------------------------

    [Fact]
    public async Task CallAgent_open_list_is_restricted_to_assigned_projects()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var assigned = ctx.Db.AddProject(acme.Id, "Assigned");
        var other = ctx.Db.AddProject(acme.Id, "Other");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, assigned.Id);

        var mineShift = ctx.Db.AddShift(assigned.Id, At(1, 8), At(1, 16));
        ctx.Db.AddShift(other.Id, At(1, 8), At(1, 16));

        var open = await CallAgentService(ctx, jane.UserId, jane.Id).ListAsync(null, CancellationToken.None);

        Assert.Equal(new[] { mineShift.Id }, open.Select(s => s.Id).ToArray());
    }

    [Fact]
    public async Task A_non_assigned_call_agent_sees_no_open_shifts_and_cannot_filter_to_the_project()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));

        var svc = CallAgentService(ctx, jane.UserId, jane.Id);

        Assert.Empty(await svc.ListAsync(null, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.ListAsync(project.Id, CancellationToken.None));
    }

    [Fact]
    public async Task A_call_agent_never_sees_a_Closed_shift_in_the_open_list()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(jane.Id, project.Id);

        var openShift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));
        var closedShift = ctx.Db.AddShift(project.Id, At(2, 8), At(2, 16), ShiftStatus.Closed);

        var svc = CallAgentService(ctx, jane.UserId, jane.Id);

        var list = await svc.ListAsync(null, CancellationToken.None);
        Assert.Equal(new[] { openShift.Id }, list.Select(s => s.Id).ToArray());

        await Assert.ThrowsAsync<NotFoundException>(() =>
            svc.GetAsync(closedShift.Id, CancellationToken.None));
    }

    [Fact]
    public async Task CallAgent_reading_a_shift_on_a_non_assigned_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var shift = ctx.Db.AddShift(project.Id, At(1, 8), At(1, 16));

        // Jane is not assigned to the project — same 404 as an unknown id.
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CallAgentService(ctx, jane.UserId, jane.Id).GetAsync(shift.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            CallAgentService(ctx, jane.UserId, jane.Id).GetAsync(9999, CancellationToken.None));
    }
}
