using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Projects;

/// <summary>
/// <c>PUT /api/projects/{id}/supervisor</c> — Manager-only. Moving a project can
/// collide with <c>UQ_Projects_Supervisor_Name</c> on the target supervisor, so
/// that guard is re-run against the target before saving (see the phase report).
/// </summary>
public class ProjectReassignmentTests
{
    private static ProjectService ServiceForManager(SqliteTestContext ctx, int managerUserId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Manager(managerUserId)), new TestClock());

    [Fact]
    public async Task Reassigning_to_a_different_supervisor_moves_the_project()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        var updated = await ServiceForManager(ctx, managerUserId: 1)
            .ReassignSupervisorAsync(project.Id, new ReassignProjectSupervisorRequest { SupervisorId = globex.Id }, CancellationToken.None);

        Assert.Equal(globex.Id, updated.SupervisorId);
        var persisted = await ctx.NewContext().Projects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(globex.Id, persisted.SupervisorId);
    }

    [Fact]
    public async Task Reassigning_to_the_current_supervisor_is_a_no_op()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        var updated = await ServiceForManager(ctx, managerUserId: 1)
            .ReassignSupervisorAsync(project.Id, new ReassignProjectSupervisorRequest { SupervisorId = acme.Id }, CancellationToken.None);

        Assert.Equal(acme.Id, updated.SupervisorId);
    }

    [Fact]
    public async Task Reassigning_to_a_supervisor_with_a_same_named_project_is_a_conflict_and_changes_nothing()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var project = ctx.Db.AddProject(acme.Id, "Support");
        ctx.Db.AddProject(globex.Id, "Support");

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            ServiceForManager(ctx, managerUserId: 1)
                .ReassignSupervisorAsync(project.Id, new ReassignProjectSupervisorRequest { SupervisorId = globex.Id }, CancellationToken.None));

        var untouched = await ctx.NewContext().Projects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(acme.Id, untouched.SupervisorId);
    }

    [Fact]
    public async Task Reassigning_to_an_unknown_supervisor_is_a_validation_error()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(acme.Id, "Support");

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceForManager(ctx, managerUserId: 1)
                .ReassignSupervisorAsync(project.Id, new ReassignProjectSupervisorRequest { SupervisorId = 9999 }, CancellationToken.None));

        var untouched = await ctx.NewContext().Projects.SingleAsync(p => p.Id == project.Id);
        Assert.Equal(acme.Id, untouched.SupervisorId);
    }

    [Fact]
    public async Task Reassigning_an_unknown_project_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceForManager(ctx, managerUserId: 1)
                .ReassignSupervisorAsync(9999, new ReassignProjectSupervisorRequest { SupervisorId = acme.Id }, CancellationToken.None));
    }
}
