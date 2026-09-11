using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Projects;

public class CreateProjectTests
{
    private static ProjectService ServiceForManager(SqliteTestContext ctx, int managerUserId) =>
        new(ctx.Db, new AccessScope(StubCurrentUser.Manager(managerUserId)), new TestClock());

    [Fact]
    public async Task Create_stores_the_project_under_the_named_supervisor()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        var created = await ServiceForManager(ctx, managerUserId: 1)
            .CreateAsync(
                new CreateProjectRequest { Name = "  Night Support  ", SupervisorId = acme.Id },
                CancellationToken.None);

        Assert.Equal(acme.Id, created.SupervisorId);
        Assert.Equal("Night Support", created.Name); // trimmed by the validator
        Assert.True(created.IsActive);

        var persisted = await ctx.NewContext().Projects.SingleAsync();
        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(acme.Id, persisted.SupervisorId);
    }

    [Fact]
    public async Task A_second_project_with_the_same_name_for_the_same_supervisor_is_rejected()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var service = ServiceForManager(ctx, managerUserId: 1);
        await service.CreateAsync(new CreateProjectRequest { Name = "Support", SupervisorId = acme.Id }, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new CreateProjectRequest { Name = "Support", SupervisorId = acme.Id }, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().Projects.CountAsync());
    }

    [Fact]
    public async Task The_same_name_is_free_for_a_different_supervisor()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var globex = ctx.Db.AddSupervisor("Globex");
        var service = ServiceForManager(ctx, managerUserId: 1);

        await service.CreateAsync(new CreateProjectRequest { Name = "Support", SupervisorId = acme.Id }, CancellationToken.None);
        await service.CreateAsync(new CreateProjectRequest { Name = "Support", SupervisorId = globex.Id }, CancellationToken.None);

        Assert.Equal(2, await ctx.NewContext().Projects.CountAsync());
    }

    [Fact]
    public async Task A_blank_name_is_a_validation_error()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceForManager(ctx, managerUserId: 1)
                .CreateAsync(new CreateProjectRequest { Name = "   ", SupervisorId = acme.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task An_unknown_supervisor_id_is_a_validation_error()
    {
        using var ctx = new SqliteTestContext();

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceForManager(ctx, managerUserId: 1)
                .CreateAsync(new CreateProjectRequest { Name = "Support", SupervisorId = 9999 }, CancellationToken.None));

        Assert.Equal(0, await ctx.NewContext().Projects.CountAsync());
    }
}
