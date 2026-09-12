using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Infrastructure;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class LeaveYearBoundaryTests
{
    [Fact]
    public void Tehran_0030_on_first_Farvardin_is_in_the_new_leave_year()
    {
        var leaveYear = new PersianLeaveYear();
        var instant = new DateTime(2026, 3, 20, 21, 0, 0, DateTimeKind.Utc);

        var range = leaveYear.Resolve(instant);

        Assert.Equal(new DateTime(2026, 3, 20, 20, 30, 0, DateTimeKind.Utc), range.StartUtc);
        Assert.True(instant >= range.StartUtc && instant < range.EndUtc);
        Assert.NotEqual(leaveYear.Resolve(range.StartUtc.AddSeconds(-1)), range);
    }

    [Fact]
    public async Task Balance_counts_leave_by_Tehran_year_even_when_the_UTC_date_is_previous_day()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var farvardin0030 = new DateTime(2026, 3, 20, 21, 0, 0, DateTimeKind.Utc);
        var first = ctx.Db.AddShift(project.Id, farvardin0030, farvardin0030.AddHours(8), ShiftStatus.Assigned, agent.Id);
        var second = ctx.Db.AddShift(project.Id, farvardin0030.AddDays(1), farvardin0030.AddDays(1).AddHours(8), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, first.Id, AgentRequestType.Leave, AgentRequestStatus.Approved, first.StartUtc, first.EndUtc, farvardin0030);

        var result = await AgentRequestTestServices.Create(
                ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id), farvardin0030)
            .CreateAsync(new CreateAgentRequest
            {
                ShiftId = second.Id,
                RequestType = "Leave",
                StartUtc = second.StartUtc,
                EndUtc = second.EndUtc,
            }, CancellationToken.None);

        Assert.Equal(25, result.RemainingLeaveDays);
    }
}
