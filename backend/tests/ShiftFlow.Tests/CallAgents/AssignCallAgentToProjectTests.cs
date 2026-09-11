using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.CallAgents;

/// <summary>
/// CLAUDE.md §7: the assignment routes start with the CallAgent, but the check that
/// matters is on the <em>project</em> — it must belong to the calling supervisor.
/// A cross-supervisor attempt must fail as NotFound, not leak the project.
/// </summary>
public class AssignCallAgentToProjectTests
{
    private static CallAgentProjectService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, StubCurrentUser.Supervisor(userId, supervisorId), new TestClock());

    [Fact]
    public async Task Assigning_to_your_own_project_creates_the_link()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .AssignAsync(callAgent.Id, project.Id, CancellationToken.None);

        Assert.True(await ctx.NewContext().CallAgentProjects
            .AnyAsync(ep => ep.CallAgentId == callAgent.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Assigning_to_another_supervisors_project_is_NotFound_and_writes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .AssignAsync(callAgent.Id, globexProject.Id, CancellationToken.None));

        Assert.False(await ctx.NewContext().CallAgentProjects.AnyAsync(ep => ep.ProjectId == globexProject.Id));
    }

    [Fact]
    public async Task Assigning_to_an_unknown_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(callAgent.Id, 9999, CancellationToken.None));
    }

    [Fact]
    public async Task Assigning_an_unknown_call_agent_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(9999, project.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Assigning_twice_is_idempotent()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        var service = ServiceFor(ctx, acme.UserId, acme.Id);

        await service.AssignAsync(callAgent.Id, project.Id, CancellationToken.None);
        await service.AssignAsync(callAgent.Id, project.Id, CancellationToken.None);

        Assert.Equal(1, await ctx.NewContext().CallAgentProjects
            .CountAsync(ep => ep.CallAgentId == callAgent.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Listing_call_agents_of_another_supervisors_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .ListCallAgentsForProjectAsync(globexProject.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Listing_an_call_agents_projects_shows_only_the_callers_own()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Acme Support");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, acmeProject.Id);
        ctx.Db.Assign(callAgent.Id, globexProject.Id);

        var visible = await ServiceFor(ctx, acme.UserId, acme.Id)
            .ListProjectsForCallAgentAsync(callAgent.Id, CancellationToken.None);

        Assert.Equal(new[] { "Acme Support" }, visible.Select(p => p.Name).ToArray());
    }
}
