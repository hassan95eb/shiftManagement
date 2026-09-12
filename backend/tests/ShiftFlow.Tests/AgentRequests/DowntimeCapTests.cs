using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class DowntimeCapTests
{
    [Fact]
    public async Task Downtime_beyond_monthly_cap_is_a_conflict()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = AgentRequestTestServices.Now;
        var firstShift = ctx.Db.AddShift(project.Id, now.AddHours(-8), now, ShiftStatus.Assigned, agent.Id);
        var secondShift = ctx.Db.AddShift(project.Id, now.AddHours(-4), now.AddHours(4), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, firstShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, firstShift.StartUtc, firstShift.StartUtc.AddHours(7));
        var pending = ctx.Db.AddAgentRequest(agent.Id, secondShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending, secondShift.StartUtc, secondShift.StartUtc.AddHours(2));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            AgentRequestTestServices.Create(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
                .ApproveAsync(pending.Id, CancellationToken.None));
    }

    [Theory]
    [InlineData("2026-03-20T20:30:00Z")]
    [InlineData("2026-04-20T20:30:00Z")]
    public async Task Downtime_crossing_a_Jalali_month_boundary_is_split_between_months(string boundaryText)
    {
        using var ctx = new SqliteTestContext();
        var boundary = DateTime.Parse(
            boundaryText,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AdjustToUniversal);
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var priorShift = ctx.Db.AddShift(project.Id, boundary.AddHours(-8), boundary.AddHours(-1), ShiftStatus.Assigned, agent.Id);
        var crossingShift = ctx.Db.AddShift(project.Id, boundary.AddHours(-1), boundary.AddHours(1), ShiftStatus.Assigned, agent.Id);
        ctx.Db.AddAgentRequest(agent.Id, priorShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Approved, priorShift.StartUtc, priorShift.EndUtc);
        var pending = ctx.Db.AddAgentRequest(agent.Id, crossingShift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending, crossingShift.StartUtc, crossingShift.EndUtc);

        var result = await AgentRequestTestServices.Create(
                ctx,
                StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id),
                crossingShift.EndUtc)
            .ApproveAsync(pending.Id, CancellationToken.None);

        Assert.Equal("Approved", result.Status);
    }
}
