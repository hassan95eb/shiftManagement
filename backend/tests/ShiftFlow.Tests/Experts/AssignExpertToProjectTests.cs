using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Experts;

/// <summary>
/// CLAUDE.md §7: the assignment routes start with the expert, but the check that
/// matters is on the <em>project</em> — it must belong to the calling employer.
/// A cross-employer attempt must fail as NotFound, not leak the project.
/// </summary>
public class AssignExpertToProjectTests
{
    private static ExpertProjectService ServiceFor(SqliteTestContext ctx, int userId, int employerId) =>
        new(ctx.Db, StubCurrentUser.Employer(userId, employerId), new TestClock());

    [Fact]
    public async Task Assigning_to_your_own_project_creates_the_link()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .AssignAsync(expert.Id, project.Id, CancellationToken.None);

        Assert.True(await ctx.NewContext().ExpertProjects
            .AnyAsync(ep => ep.ExpertId == expert.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Assigning_to_another_employers_project_is_NotFound_and_writes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var expert = ctx.Db.AddExpert("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .AssignAsync(expert.Id, globexProject.Id, CancellationToken.None));

        Assert.False(await ctx.NewContext().ExpertProjects.AnyAsync(ep => ep.ProjectId == globexProject.Id));
    }

    [Fact]
    public async Task Assigning_to_an_unknown_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var expert = ctx.Db.AddExpert("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(expert.Id, 9999, CancellationToken.None));
    }

    [Fact]
    public async Task Assigning_an_unknown_expert_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).AssignAsync(9999, project.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Assigning_twice_is_idempotent()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        var service = ServiceFor(ctx, acme.UserId, acme.Id);

        await service.AssignAsync(expert.Id, project.Id, CancellationToken.None);
        await service.AssignAsync(expert.Id, project.Id, CancellationToken.None);

        Assert.Equal(1, await ctx.NewContext().ExpertProjects
            .CountAsync(ep => ep.ExpertId == expert.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Listing_experts_of_another_employers_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .ListExpertsForProjectAsync(globexProject.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Listing_an_experts_projects_shows_only_the_callers_own()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Acme Support");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, acmeProject.Id);
        ctx.Db.Assign(expert.Id, globexProject.Id);

        var visible = await ServiceFor(ctx, acme.UserId, acme.Id)
            .ListProjectsForExpertAsync(expert.Id, CancellationToken.None);

        Assert.Equal(new[] { "Acme Support" }, visible.Select(p => p.Name).ToArray());
    }
}
