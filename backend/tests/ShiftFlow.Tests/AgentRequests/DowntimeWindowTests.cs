using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Domain.Exceptions;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.AgentRequests;

public class DowntimeWindowTests
{
    [Fact]
    public async Task Downtime_outside_shift_window_is_bad_request_validation()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = AgentRequestTestServices.Now;
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-4), now.AddHours(4), ShiftStatus.Assigned, agent.Id);

        await Assert.ThrowsAsync<ValidationException>(() =>
            AgentRequestTestServices.Create(ctx, StubCurrentUser.CallAgent(agent.UserId, agent.Id)).CreateAsync(
                new CreateAgentRequest
                {
                    ShiftId = shift.Id,
                    RequestType = "Downtime",
                    StartUtc = shift.StartUtc.AddMinutes(-1),
                    EndUtc = shift.StartUtc.AddHours(1),
                }, CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_window_discovered_at_approval_is_a_business_conflict()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = AgentRequestTestServices.Now;
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-4), now.AddHours(4), ShiftStatus.Assigned, agent.Id);
        var request = ctx.Db.AddAgentRequest(
            agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending,
            shift.StartUtc.AddMinutes(-1), shift.StartUtc.AddHours(1));

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() =>
            AgentRequestTestServices.Create(ctx, StubCurrentUser.Supervisor(supervisor.UserId, supervisor.Id))
                .ApproveAsync(request.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Downtime_database_failure_is_not_reported_as_duplicate_leave()
    {
        using var ctx = new SqliteTestContext();
        var supervisor = ctx.Db.AddSupervisor("Acme");
        var project = ctx.Db.AddProject(supervisor.Id, "Support");
        var agent = ctx.Db.AddCallAgent("Jane Doe");
        var now = AgentRequestTestServices.Now;
        var shift = ctx.Db.AddShift(project.Id, now.AddHours(-4), now.AddHours(4), ShiftStatus.Assigned, agent.Id);
        var request = ctx.Db.AddAgentRequest(
            agent.Id, shift.Id, AgentRequestType.Downtime, AgentRequestStatus.Pending,
            shift.StartUtc, shift.StartUtc.AddHours(1));

        var invalidDecider = StubCurrentUser.Supervisor(999_999, supervisor.Id);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AgentRequestTestServices.Create(ctx, invalidDecider)
                .ApproveAsync(request.Id, CancellationToken.None));
    }
}
