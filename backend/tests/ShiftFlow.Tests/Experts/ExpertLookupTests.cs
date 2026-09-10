using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Experts;

/// <summary>
/// Experts are a shared pool — the schema has no employer-ownership column — so
/// list and lookup are not employer-scoped; they only require an employer
/// principal. The ownership boundary is enforced on assignment, not here.
/// </summary>
public class ExpertLookupTests
{
    private static ExpertService ServiceFor(SqliteTestContext ctx, int userId, int employerId) =>
        new(ctx.Db, StubCurrentUser.Employer(userId, employerId), new FakePasswordHasher(), new TestClock());

    [Fact]
    public async Task List_returns_every_expert()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        ctx.Db.AddExpert("Adam Ant");
        ctx.Db.AddExpert("Zoe Zephyr");

        var experts = await ServiceFor(ctx, acme.UserId, acme.Id).ListAsync(CancellationToken.None);

        Assert.Equal(new[] { "Adam Ant", "Zoe Zephyr" }, experts.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public async Task Get_of_an_unknown_id_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).GetAsync(4242, CancellationToken.None));
    }
}
