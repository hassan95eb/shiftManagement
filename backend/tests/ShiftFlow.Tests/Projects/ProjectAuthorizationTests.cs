using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Projects;

/// <summary>
/// CLAUDE.md §7: a supervisor may only ever touch their own projects. A correct
/// role pointed at another supervisor's project — or at an id that does not exist
/// — must fail, and must fail the same way, so ids cannot be probed.
/// </summary>
public class ProjectAuthorizationTests
{
    private static ProjectService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, StubCurrentUser.Supervisor(userId, supervisorId), new TestClock());

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
}
