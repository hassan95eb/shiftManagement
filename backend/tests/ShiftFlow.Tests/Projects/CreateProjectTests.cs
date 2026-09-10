using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Projects;
using ShiftFlow.Application.Features.Projects.Dtos;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Projects;

public class CreateProjectTests
{
    private static ProjectService ServiceFor(SqliteTestContext ctx, int userId, int employerId) =>
        new(ctx.Db, StubCurrentUser.Employer(userId, employerId), new TestClock());

    [Fact]
    public async Task Create_stores_the_project_under_the_calling_employer()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");

        var created = await ServiceFor(ctx, acme.UserId, acme.Id)
            .CreateAsync(new CreateProjectRequest { Name = "  Night Support  " }, CancellationToken.None);

        Assert.Equal(acme.Id, created.EmployerId);
        Assert.Equal("Night Support", created.Name); // trimmed by the validator
        Assert.True(created.IsActive);

        var persisted = await ctx.NewContext().Projects.SingleAsync();
        Assert.Equal(created.Id, persisted.Id);
        Assert.Equal(acme.Id, persisted.EmployerId);
    }

    [Fact]
    public async Task A_second_project_with_the_same_name_for_the_same_employer_is_rejected()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var service = ServiceFor(ctx, acme.UserId, acme.Id);
        await service.CreateAsync(new CreateProjectRequest { Name = "Support" }, CancellationToken.None);

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new CreateProjectRequest { Name = "Support" }, CancellationToken.None));

        Assert.Equal(1, await ctx.NewContext().Projects.CountAsync());
    }

    [Fact]
    public async Task The_same_name_is_free_for_a_different_employer()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        var globex = ctx.Db.AddEmployer("Globex");

        await ServiceFor(ctx, acme.UserId, acme.Id)
            .CreateAsync(new CreateProjectRequest { Name = "Support" }, CancellationToken.None);
        await ServiceFor(ctx, globex.UserId, globex.Id)
            .CreateAsync(new CreateProjectRequest { Name = "Support" }, CancellationToken.None);

        Assert.Equal(2, await ctx.NewContext().Projects.CountAsync());
    }

    [Fact]
    public async Task A_blank_name_is_a_validation_error()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id)
                .CreateAsync(new CreateProjectRequest { Name = "   " }, CancellationToken.None));
    }
}
