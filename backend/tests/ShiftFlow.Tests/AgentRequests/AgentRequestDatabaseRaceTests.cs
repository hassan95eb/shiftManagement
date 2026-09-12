using Microsoft.EntityFrameworkCore;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class AgentRequestDatabaseRaceTests
{
    [Fact]
    public async Task Database_index_rejects_two_racing_approved_leaves_on_one_shift()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var jane = ctx.Db.AddCallAgent("Jane Doe");
        var mike = ctx.Db.AddCallAgent("Mike Roe");
        var now = AgentRequestTestServices.Now;
        var shift = ctx.Db.AddShift(project.Id, now, now.AddHours(8));

        using var first = ctx.NewContext();
        using var second = ctx.NewContext();
        first.AgentRequests.Add(Request(jane.Id));
        second.AgentRequests.Add(Request(mike.Id));

        await first.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        AgentRequest Request(int callAgentId) => new()
        {
            CallAgentId = callAgentId,
            ShiftId = shift.Id,
            RequestType = AgentRequestType.Leave,
            Status = AgentRequestStatus.Approved,
            RequestedAtUtc = now,
            StartUtc = shift.StartUtc,
            EndUtc = shift.EndUtc,
        };
    }

    [Fact]
    public async Task Multiple_approved_downtime_rows_on_one_shift_are_legal()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = AgentRequestTestServices.Now;
        var shift = ctx.Db.AddShift(project.Id, now, now.AddHours(8));

        ctx.Db.AddAgentRequest(agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, now, now.AddHours(1));
        ctx.Db.AddAgentRequest(agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, now.AddHours(2), now.AddHours(3));

        Assert.Equal(2, await ctx.Db.AgentRequests.CountAsync());
    }
}
