using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Projects;

/// <summary>
/// The spec keeps <c>DELETE /api/projects/{id}</c>, but the schema cascades
/// Project → Shifts → ShiftApplications. So the delete is guarded: it works only
/// while the project has no shifts; otherwise the caller is told to deactivate.
/// </summary>
public class DeleteProjectTests
{
    private static ProjectService ServiceFor(SqliteTestContext ctx, int userId, int employerId) =>
        new(ctx.Db, StubCurrentUser.Employer(userId, employerId), new TestClock());

    [Fact]
    public async Task An_empty_project_is_deleted()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        await ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(project.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().Projects.AnyAsync(p => p.Id == project.Id));
    }

    [Fact]
    public async Task A_project_with_a_shift_cannot_be_deleted()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        ctx.Db.AddShift(project.Id);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(project.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().Projects.AnyAsync(p => p.Id == project.Id));
    }

    [Fact]
    public async Task Deleting_a_project_also_removes_its_expert_assignments()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);

        await ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(project.Id, CancellationToken.None);

        var fresh = ctx.NewContext();
        Assert.False(await fresh.Projects.AnyAsync(p => p.Id == project.Id));
        Assert.False(await fresh.ExpertProjects.AnyAsync(ep => ep.ProjectId == project.Id));
        Assert.True(await fresh.Experts.AnyAsync(e => e.Id == expert.Id)); // the expert itself stays
    }
}
