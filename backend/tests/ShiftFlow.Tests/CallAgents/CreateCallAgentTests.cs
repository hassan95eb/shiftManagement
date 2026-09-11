using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Experts;
using ShiftFlow.Application.Features.Experts.Dtos;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Tests.Support;

namespace ShiftFlow.Tests.Experts;

public class CreateExpertTests
{
    private static ExpertService ServiceFor(SqliteTestContext ctx, StubCurrentUser caller) =>
        new(ctx.Db, caller, new FakePasswordHasher(), new TestClock());

    private static CreateExpertRequest Request(string username = "temp-jane") => new()
    {
        Username = username,
        Password = "correct horse battery staple",
        FullName = "Jane Doe",
    };

    [Fact]
    public async Task Create_writes_the_user_and_the_expert_together()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");

        var created = await ServiceFor(ctx, StubCurrentUser.Employer(acme.UserId, acme.Id))
            .CreateAsync(Request(), CancellationToken.None);

        var fresh = ctx.NewContext();
        var user = await fresh.Users.SingleAsync(u => u.Id == created.UserId);
        var expert = await fresh.Experts.SingleAsync(e => e.Id == created.Id);

        Assert.Equal(UserRole.Expert, user.Role);
        Assert.True(user.IsActive);
        Assert.Equal("temp-jane", user.Username);
        Assert.Equal(FakePasswordHasher.Prefix + "correct horse battery staple", user.PasswordHash);
        Assert.NotEqual("correct horse battery staple", user.PasswordHash);
        Assert.Equal(user.Id, expert.UserId);
        Assert.Equal("Jane Doe", expert.FullName);
    }

    [Fact]
    public async Task A_duplicate_username_is_rejected_and_nothing_is_written()
    {
        using var ctx = new SqliteTestContext();
        var acme = ctx.Db.AddEmployer("Acme");
        ctx.Db.AddExpert("Existing Expert"); // username -> "existing-expert"

        var usersBefore = await ctx.NewContext().Users.CountAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            ServiceFor(ctx, StubCurrentUser.Employer(acme.UserId, acme.Id))
                .CreateAsync(Request(username: "existing-expert"), CancellationToken.None));

        Assert.Equal(usersBefore, await ctx.NewContext().Users.CountAsync());
    }

    [Fact]
    public async Task An_expert_principal_cannot_create_experts()
    {
        using var ctx = new SqliteTestContext();
        var expert = ctx.Db.AddExpert("Jane Doe");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ServiceFor(ctx, StubCurrentUser.Expert(expert.UserId, expert.Id))
                .CreateAsync(Request(), CancellationToken.None));
    }
}
