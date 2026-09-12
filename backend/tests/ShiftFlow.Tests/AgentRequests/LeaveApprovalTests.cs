using Microsoft.EntityFrameworkCore;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class LeaveApprovalTests
{
    [Fact]
    public async Task Approving_leave_releases_assigned_shift_in_the_same_write()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = AgentRequestTestServices.Now;
        var shift = ctx.Db.AddShift(project.Id, now.AddDays(1), now.AddDays(1).AddHours(8), ShiftStatus.Assigned, agent.Id);
        var request = ctx.Db.AddAgentRequest(agent.Id, shift.Id, AgentRequestType.Leave, AgentRequestStatus.Pending, shift.StartUtc, shift.EndUtc);

        var result = await AgentRequestTestServices.Create(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
            .ApproveAsync(request.Id, CancellationToken.None);

        var stored = ctx.NewContext();
        Assert.Equal("Approved", result.Status);
        Assert.Equal(ShiftStatus.Released, (await stored.Shifts.SingleAsync(s => s.Id == shift.Id)).Status);
    }
}
