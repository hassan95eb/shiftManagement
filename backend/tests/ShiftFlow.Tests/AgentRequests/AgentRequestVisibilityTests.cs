using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class AgentRequestVisibilityTests
{
    [Fact]
    public async Task Request_list_is_scoped_for_call_agent_supervisor_and_manager()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        var other = ctx.Db.AddSupervisor("Other");
        var acmeProject = ctx.Db.AddProject(acme.Id, "Support");
        var otherProject = ctx.Db.AddProject(other.Id, "Sales");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var mike = ctx.Db.AddCallAgent("Mike Roe");
        var now = AgentRequestTestServices.Now;
        var janeShift = ctx.Db.AddShift(acmeProject.Id, now, now.AddHours(8));
        var mikeShift = ctx.Db.AddShift(otherProject.Id, now, now.AddHours(8));
        ctx.Db.AddAgentRequest(jane.Id, janeShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, janeShift.StartUtc, janeShift.EndUtc);
        ctx.Db.AddAgentRequest(mike.Id, mikeShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, mikeShift.StartUtc, mikeShift.EndUtc);

        var filter = new AgentRequestListFilter();
        var own = await AgentRequestTestServices.Create(ctx, StubCurrentUser.CallAgent(jane.UserId, jane.Id)).ListAsync(filter, CancellationToken.None);
        var supervised = await AgentRequestTestServices.Create(ctx, StubCurrentUser.Supervisor(acme.UserId, acme.Id)).ListAsync(filter, CancellationToken.None);
        var all = await AgentRequestTestServices.Create(ctx, StubCurrentUser.Manager(999)).ListAsync(filter, CancellationToken.None);

        Assert.Single(own);
        Assert.Equal(jane.Id, own[0].CallAgentId);
        Assert.Single(supervised);
        Assert.Equal(jane.Id, supervised[0].CallAgentId);
        Assert.Equal(2, all.Count);
    }
}
