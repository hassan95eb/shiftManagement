using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.CallAgents;
using ShiftFlow.Application.Features.CallAgents.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.CallAgents;

public class CreateCallAgentTests
{
    private static CallAgentService ServiceFor(SqliteTestContext ctx, StubCurrentUser caller) =>
        new(ctx.Db, caller, new FakePasswordHasher(), new TestClock(), Options.Create(new LeaveOptions()));

    private static CreateCallAgentRequest Request(string username = "temp-jane") => new()
    {
        Username = username,
        Password = "correct horse battery staple",
        FullName = "Jane Doe",
    };

    [Fact]
    public async Task Create_writes_the_user_and_the_call_agent_together()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");

        var created = await ServiceFor(ctx, StubCurrentUser.Supervisor(acme.UserId, acme.Id))
            .CreateAsync(Request(), CancellationToken.None);

        var fresh = ctx.NewContext();
        var user = await fresh.Users.SingleAsync(u => u.Id == created.UserId);
        var callAgent = await fresh.CallAgents.SingleAsync(e => e.Id == created.Id);

        Assert.Equal(UserRole.CallAgent, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("temp-jane", user.Username);
        Assert.Equal(FakePasswordHasher.Prefix + "correct horse battery staple", user.PasswordHash);
        Assert.NotEqual("correct horse battery staple", user.PasswordHash);
        Assert.Equal(user.Id, callAgent.UserId);
        Assert.Equal("Jane Doe", callAgent.FullName);
        Assert.Equal(26, callAgent.AnnualLeaveDays);
    }

    [Fact]
    public async Task A_duplicate_username_is_rejected_and_nothing_is_written()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddSupervisor("Acme");
        ctx.Db.AddCallAgent("Existing Call Agent"); // username -> "existing-call-agent"

        var usersBefore = await ctx.NewContext().Users.CountAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(ctx, StubCurrentUser.Supervisor(acme.UserId, acme.Id))
                .CreateAsync(Request(username: "existing-call-agent"), CancellationToken.None));

        Assert.Equal(usersBefore, await ctx.NewContext().Users.CountAsync());
    }

    [Fact]
    public async Task A_call_agent_principal_cannot_create_call_agents()
    {
        using var ctx = new SqliteTestContext();
        var callAgent = ctx.Db.AddCallAgent("Jane Doe");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServiceFor(ctx, StubCurrentUser.CallAgent(callAgent.UserId, callAgent.Id))
                .CreateAsync(Request(), CancellationToken.None));
    }
}
