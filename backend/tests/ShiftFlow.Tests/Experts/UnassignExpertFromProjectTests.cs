using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Experts;

public class UnassignExpertFromProjectTests
{
    private static readonly DateTime Now = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private static ExpertProjectService ServiceFor(SqliteTestContext ctx, int userId, int employerId) =>
        new(ctx.Db, StubCurrentUser.Employer(userId, employerId), new TestClock(Now));

    [Fact]
    public async Task Unassigning_removes_the_link()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .UnassignAsync(expert.Id, project.Id, CancellationToken.None);

        Assert.False(await ctx.NewContext().ExpertProjects
            .AnyAsync(ep => ep.ExpertId == expert.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Unassigning_is_blocked_while_an_approved_application_exists()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id);
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        ctx.Db.AddApplication(shift.Id, expert.Id, ApplicationStatus.Approved);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UnassignAsync(expert.Id, project.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().ExpertProjects
            .AnyAsync(ep => ep.ExpertId == expert.Id && ep.ProjectId == project.Id));
    }

    [Fact]
    public async Task Unassigning_rejects_pending_applications_for_the_projects_shifts()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var shift = ctx.Db.AddShift(project.Id);
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, project.Id);
        var application = ctx.Db.AddApplication(shift.Id, expert.Id, ApplicationStatus.Pending);

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .UnassignAsync(expert.Id, project.Id, CancellationToken.None);

        var decided = await ctx.NewContext().ShiftApplications.SingleAsync(a => a.Id == application.Id);
        Assert.Equal(ApplicationStatus.Rejected, decided.Status);
        Assert.Equal("Expert unassigned from the project.", decided.DecisionNote);
        Assert.Equal(acme.UserId, decided.DecidedByUserId);
        Assert.Equal(Now, decided.DecidedAtUtc);
    }

    [Fact]
    public async Task Unassigning_from_another_employers_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");
        var globexProject = ctx.Db.AddProject(globex.Id, "Globex Support");
        var expert = ctx.Db.AddExpert("Jane Doe");
        ctx.Db.Assign(expert.Id, globexProject.Id);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UnassignAsync(expert.Id, globexProject.Id, CancellationToken.None));

        Assert.True(await ctx.NewContext().ExpertProjects
            .AnyAsync(ep => ep.ProjectId == globexProject.Id));
    }

    [Fact]
    public async Task Unassigning_when_there_is_no_assignment_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        var expert = ctx.Db.AddExpert("Jane Doe");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .UnassignAsync(expert.Id, project.Id, CancellationToken.None));
    }
}
