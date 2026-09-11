using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.CallAgents;

public class UnassignCallAgentFromProjectTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static CallAgentProjectService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId)
    {
        var currentUser = StubCurrentUser.Supervisor(userId, supervisorId);
        return new(ctx.Db, currentUser, new AccessScope(currentUser), new TestClock(Now));
    }

    [Fact]
    public async Task Unassigning_removes_the_link()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .UnassignAsync(callAgent.Id, project.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().CallAgentProjects
            .AnyAsync(ep => ep.CallAgentId == callAgent.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Unassigning_is_blocked_while_an_approved_application_exists()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id);
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, callAgent.Id, ApplicationStatus.Approved);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UnassignAsync(callAgent.Id, project.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().CallAgentProjects
            .AnyAsync(ep => ep.CallAgentId == callAgent.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Unassigning_rejects_pending_applications_for_the_projects_shifts()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id);
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, project.Id);
        var application = ctx.Db.AddApplication(shift.Id, callAgent.Id, ApplicationStatus.Pending);

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .UnassignAsync(callAgent.Id, project.Id, CancellationToken.None);

        var decided = await ctx.NewContext().ShiftApplications.SingleAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Rejected, decided.Status);
        Assert.Equal("CallAgent unassigned from the project.", decided.DecisionNote);
        Assert.Equal(acme.UserId, decided.DecidedByUserId);
        Assert.Equal(Now, decided.DecidedAtUtc);
    }

    [Fact]
    public async Task Unassigning_from_another_supervisors_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");
        ctx.Db.Assign(callAgent.Id, globexProject.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UnassignAsync(callAgent.Id, globexProject.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().CallAgentProjects
            .AnyAsync(ep => ep.ProjectId == globexProject.Id));
    }

    [Fact]
    public async Task Unassigning_when_there_is_no_assignment_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UnassignAsync(callAgent.Id, project.Id, CancellationToken.None));
    }
}
