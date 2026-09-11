using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Projects;

/// <summary>
/// CLAUDE.md §7: a supervisor may only ever touch their own projects, and a
/// correct role pointed at another supervisor's project — or at an id that does
/// not exist — must fail the same way, so ids cannot be probed. A Manager sees
/// every project, so the 404-not-403 rule never triggers for one.
/// </summary>
public class ProjectAuthorizationTests
{
    private static ProjectService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)), new TestClock());

    private static ProjectService ServiceForManager(SqliteTestContext ctx, int managerUserId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Manager(managerUserId)), new TestClock());

    private static readonly UpdateProjectRequest AnyUpdate = new() { Name = "Renamed", IsActive = false };

    [Fact]
    public async Task Reading_another_supervisors_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).GetAsync(globexProject.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Reading_an_unknown_id_with_a_valid_supervisor_is_also_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).GetAsync(9999, CancellationToken.None));
    }

    [Fact]
    public async Task Updating_another_supervisors_project_is_NotFound_and_changes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UpdateAsync(globexProject.Id, AnyUpdate, CancellationToken.None));

        var untouched = await ctx.NewContext().Projects.SingleAsync(p => p.Id == globexProject.Id);
        Assert.Equal("Globex Support", untouched.Name);
        Assert.True(untouched.IsActive);
    }

    [Fact]
    public async Task Deleting_another_supervisors_project_is_NotFound_and_the_project_survives()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(globexProject.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().Projects.AnyAsync(p => p.Id == globexProject.Id));
    }

    [Fact]
    public async Task List_returns_only_the_callers_projects()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        ctx.Db.AddProject(acme.Id, "Acme A");
        ctx.Db.AddProject(acme.Id, "Acme B");
        ctx.Db.AddProject(globex.Id, "Globex A");

        var mine = await ServiceFor(ctx, acme.UserId, acme.Id).ListAsync(CancellationToken.None);

        Assert.Equal(new[] { "Acme A", "Acme B" }, mine.Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task A_manager_can_read_any_supervisors_project()
    {
        using var ctx = new SqliteTestContext();
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        var read = await ServiceForManager(ctx, managerUserId: 1).GetAsync(globexProject.Id, CancellationToken.None);

        Assert.Equal(globexProject.Id, read.Id);
    }

    [Fact]
    public async Task A_manager_lists_every_supervisors_projects()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        ctx.Db.AddProject(acme.Id, "Acme A");
        ctx.Db.AddProject(globex.Id, "Globex A");

        var all = await ServiceForManager(ctx, managerUserId: 1).ListAsync(CancellationToken.None);

        Assert.Equal(new[] { "Acme A", "Globex A" }, all.Select(p => p.Name).ToArray());
    }
}
