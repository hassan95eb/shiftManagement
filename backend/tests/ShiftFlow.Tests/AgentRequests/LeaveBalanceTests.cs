using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class LeaveBalanceTests
{
    [Fact]
    public async Task Leave_approval_rechecks_balance_and_returns_conflict_when_exhausted()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        agent.AnnualLeaveDays = 1;
        ctx.Db.SaveChanges();
        var now = AgentRequestTestServices.Now;
        var usedShift = ctx.Db.AddShift(project.Id, now.AddDays(-2), now.AddDays(-2).AddHours(8), ShiftStatus.Assigned, agent.Id);
        var pendingShift = ctx.Db.AddShift(project.Id, now.AddDays(1), now.AddDays(1).AddHours(8), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, usedShift.Id, AgentRequestType.Leave, AgentRequestStatus.Approved, usedShift.StartUtc, usedShift.EndUtc);
        var pending = ctx.Db.AddAgentRequest(agent.Id, pendingShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, pendingShift.StartUtc, pendingShift.EndUtc);

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            AgentRequestTestServices.Create(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
                .ApproveAsync(pending.Id, CancellationToken.None));

        Assert.Equal(AgentRequestStatus.Pending, (await ctx.NewContext().AgentRequests.SingleAsync(r => r.Id == pending.Id)).Status);
    }

    [Fact]
    public async Task Pending_leave_in_list_carries_current_balance()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        agent.AnnualLeaveDays = 2;
        ctx.Db.SaveChanges();
        var now = AgentRequestTestServices.Now;
        var usedShift = ctx.Db.AddShift(project.Id, now.AddDays(-2), now.AddDays(-2).AddHours(8));
        var pendingShift = ctx.Db.AddShift(project.Id, now.AddDays(1), now.AddDays(1).AddHours(8));
        ctx.Db.AddAgentRequest(agent.Id, usedShift.Id, AgentRequestType.Leave, AgentRequestStatus.Approved, usedShift.StartUtc, usedShift.EndUtc);
        ctx.Db.AddAgentRequest(agent.Id, pendingShift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, pendingShift.StartUtc, pendingShift.EndUtc);

        var rows = await AgentRequestTestServices.Create(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
            .ListAsync(new AgentRequestListFilter { Status = AgentRequestStatus.Pending }, CancellationToken.None);

        var pending = Assert.Single(rows);
        Assert.Equal(1, pending.RemainingLeaveDays);
        Assert.Equal("Pending", pending.Status);
    }
}
