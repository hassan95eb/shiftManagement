using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
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
    private static ProjectService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Supervisor(userId, supervisorId)), new TestClock());

    [Fact]
    public async Task An_empty_project_is_deleted()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        await ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(project.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().Projects.AnyAsync(p => p.Id == project.Id));
    }

    [Fact]
    public async Task A_project_with_a_shift_cannot_be_deleted()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        ctx.Db.AddShift(project.Id);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(project.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().Projects.AnyAsync(p => p.Id == project.Id));
    }

    [Fact]
    public async Task Deleting_a_project_also_removes_its_call_agent_assignments()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);

        await ServiceFor(ctx, acme.UserId, acme.Id).DeleteAsync(project.Id, CancellationToken.None);

        var fresh = ctx.NewContext();
        Assert.False(await fresh.Projects.AnyAsync(p => p.Id == project.Id));
        Assert.False(await fresh.CallAgentProjects.AnyAsync(ep => ep.ProjectId == project.Id));
        Assert.True(await fresh.CallAgents.AnyAsync(e => e.Id == callAgent.Id)); // the callAgent itself stays
    }
}
