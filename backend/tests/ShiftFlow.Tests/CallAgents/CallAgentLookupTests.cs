using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.CallAgents;

/// <summary>
/// CallAgents are a shared pool — the schema has no supervisor-ownership column — so
/// list and lookup are not supervisor-scoped; they only require a supervisor
/// principal. The ownership boundary is enforced on assignment, not here.
/// </summary>
public class CallAgentLookupTests
{
    private static CallAgentService ServiceFor(SqliteTestContext ctx, int userId, int supervisorId) =>
        new(ctx.Db, StubCurrentUser.Supervisor(userId, supervisorId), new FakePasswordHasher(), new TestClock(), Options.Create(new LeaveOptions()));

    [Fact]
    public async Task List_returns_every_call_agent()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        ctx.Db.AddCallAgent("Adam Ant");
        ctx.Db.AddCallAgent("Zoe Zephyr");

        var callAgents = await ServiceFor(ctx, acme.UserId, acme.Id).ListAsync(CancellationToken.None);

        Assert.Equal(new[] { "Adam Ant", "Zoe Zephyr" }, callAgents.Select(e => e.FullName).ToArray());
    }

    [Fact]
    public async Task Get_of_an_unknown_id_is_NotFound()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        await Assert.ThrowsAsync<NotFoundException>(() =>
            ServiceFor(ctx, acme.UserId, acme.Id).GetAsync(4242, CancellationToken.None));
    }
}
