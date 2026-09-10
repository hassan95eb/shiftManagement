using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Infrastructure.Persistence;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Persistence;

/// <summary>
/// The development seed is a manual-testing convenience, but the two things it
/// promises are worth pinning: the rows it writes form a consistent login graph,
/// and running it twice does not duplicate anything.
/// </summary>
public sealed class DevelopmentSeederTests
{
    private static DevelopmentSeeder SeederFor(SqliteTestContext ctx) =>
        new(ctx.Db, new FakePasswordHasher(), new TestClock(), NullLogger<DevelopmentSeeder>.Instance);

    [Fact]
    public async Task Seed_writes_a_consistent_login_graph()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();

        var assert = ctx.NewContext();

        var employerUser = await assert.Users.SingleAsync(u => u.Role == UserRole.Employer);
        var expertUser = await assert.Users.SingleAsync(u => u.Role == UserRole.Expert);
        Assert.Equal(DevelopmentSeeder.EmployerUsername, employerUser.Username);
        Assert.Equal(DevelopmentSeeder.ExpertUsername, expertUser.Username);
        Assert.NotEqual(DevelopmentSeeder.DemoPassword, employerUser.PasswordHash);
        Assert.True(employerUser.IsActive);
        Assert.True(expertUser.IsActive);

        var employer = await assert.Employers.SingleAsync();
        var expert = await assert.Experts.SingleAsync();
        Assert.Equal(employerUser.Id, employer.UserId);
        Assert.Equal(expertUser.Id, expert.UserId);

        var project = await assert.Projects.SingleAsync();
        Assert.Equal(employer.Id, project.EmployerId);
        Assert.True(await assert.ExpertProjects.AnyAsync(
            ep => ep.ExpertId == expert.Id && ep.ProjectId == project.Id));

        var shift = await assert.Shifts.SingleAsync();
        Assert.Equal(project.Id, shift.ProjectId);
        Assert.Equal(ShiftStatus.Open, shift.Status);

        // The window fully covers the shift, so apply-rule 3 is satisfiable.
        var window = await assert.Availabilities.SingleAsync(a => a.ExpertId == expert.Id);
        Assert.True(window.StartUtc <= shift.StartUtc && window.EndUtc >= shift.EndUtc);
    }

    [Fact]
    public async Task Seed_run_twice_does_not_duplicate()
    {
        using var ctx = new SqliteTestContext();

        await SeederFor(ctx).SeedAsync();
        await SeederFor(ctx).SeedAsync();

        var assert = ctx.NewContext();

        Assert.Equal(2, await assert.Users.CountAsync());
        Assert.Equal(1, await assert.Employers.CountAsync());
        Assert.Equal(1, await assert.Experts.CountAsync());
        Assert.Equal(1, await assert.Projects.CountAsync());
        Assert.Equal(1, await assert.ExpertProjects.CountAsync());
        Assert.Equal(1, await assert.Shifts.CountAsync());
        Assert.Equal(1, await assert.Availabilities.CountAsync());
    }
}
