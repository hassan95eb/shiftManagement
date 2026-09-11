using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ShiftFlow.Api.Security;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Tests.Security;

/// <summary>
/// <see cref="CurrentUser"/> must fail loudly: an unauthenticated or malformed
/// principal throws when <c>UserId</c> or <c>Role</c> is read, never returns 0,
/// null or a default role (CLAUDE.md §7).
/// </summary>
public class CurrentUserTests
{
    private static CurrentUser For(HttpContext? context)
    {
        var accessor = new HttpContextAccessor { HttpContext = context };
        return new CurrentUser(accessor);
    }

    private static HttpContext WithPrincipal(ClaimsPrincipal principal) =>
        new DefaultHttpContext { User = principal };

    private static ClaimsPrincipal Authenticated(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    [Fact]
    public void No_http_context_is_unauthenticated_and_throws_on_id_and_role()
    {
        var current = For(context: null);

        Assert.False(current.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => current.UserId);
        Assert.Throws<InvalidOperationException>(() => current.Role);
        Assert.Null(current.SupervisorId);
        Assert.Null(current.CallAgentId);
    }

    [Fact]
    public void Anonymous_principal_is_unauthenticated_and_throws_on_id_and_role()
    {
        // DefaultHttpContext.User is a ClaimsPrincipal whose identity is not authenticated.
        var current = For(WithPrincipal(new ClaimsPrincipal(new ClaimsIdentity())));

        Assert.False(current.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => current.UserId);
        Assert.Throws<InvalidOperationException>(() => current.Role);
        Assert.Null(current.SupervisorId);
        Assert.Null(current.CallAgentId);
    }

    [Fact]
    public void Authenticated_but_missing_sub_throws_on_UserId()
    {
        var current = For(WithPrincipal(Authenticated(new Claim(ClaimNames.Role, "Supervisor"))));

        Assert.True(current.IsAuthenticated);
        Assert.Throws<InvalidOperationException>(() => current.UserId);
        Assert.Equal(UserRole.Supervisor, current.Role);
    }

    [Fact]
    public void Authenticated_with_non_numeric_sub_throws_on_UserId()
    {
        var current = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "not-a-number"),
            new Claim(ClaimNames.Role, "CallAgent"))));

        Assert.Throws<InvalidOperationException>(() => current.UserId);
    }

    [Fact]
    public void Authenticated_with_unknown_role_throws_on_Role()
    {
        var current = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "1"),
            new Claim(ClaimNames.Role, "Superadmin"))));

        Assert.Equal(1, current.UserId);
        Assert.Throws<InvalidOperationException>(() => current.Role);
    }

    [Fact]
    public void Authenticated_with_numeric_role_string_throws_on_Role()
    {
        // Enum.TryParse accepts "5"; Enum.IsDefined must still reject it.
        var current = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "1"),
            new Claim(ClaimNames.Role, "5"))));

        Assert.Throws<InvalidOperationException>(() => current.Role);
    }

    [Fact]
    public void Well_formed_supervisor_principal_is_projected()
    {
        var current = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "7"),
            new Claim(ClaimNames.Role, "Supervisor"),
            new Claim(ClaimNames.SupervisorId, "42"))));

        Assert.True(current.IsAuthenticated);
        Assert.Equal(7, current.UserId);
        Assert.Equal(UserRole.Supervisor, current.Role);
        Assert.Equal(42, current.SupervisorId);
        Assert.Null(current.CallAgentId);
    }

    [Fact]
    public void RequireSupervisorId_returns_the_id_when_present_and_throws_when_absent()
    {
        var supervisor = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "7"),
            new Claim(ClaimNames.Role, "Supervisor"),
            new Claim(ClaimNames.SupervisorId, "42"))));
        Assert.Equal(42, supervisor.RequireSupervisorId());

        // A Supervisor-role token without the supervisorId claim: an ownership
        // filter must fail loudly, not silently compare against null.
        var missing = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "7"),
            new Claim(ClaimNames.Role, "Supervisor"))));
        Assert.Throws<InvalidOperationException>(() => missing.RequireSupervisorId());
    }

    [Fact]
    public void RequireCallAgentId_returns_the_id_when_present_and_throws_when_absent()
    {
        var callAgent = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "3"),
            new Claim(ClaimNames.Role, "CallAgent"),
            new Claim(ClaimNames.CallAgentId, "99"))));
        Assert.Equal(99, callAgent.RequireCallAgentId());

        var supervisor = For(WithPrincipal(Authenticated(
            new Claim(ClaimNames.Sub, "7"),
            new Claim(ClaimNames.Role, "Supervisor"),
            new Claim(ClaimNames.SupervisorId, "42"))));
        Assert.Throws<InvalidOperationException>(() => supervisor.RequireCallAgentId());
    }

    [Fact]
    public void Require_accessors_throw_when_unauthenticated()
    {
        var current = For(context: null);

        Assert.Throws<InvalidOperationException>(() => current.RequireSupervisorId());
        Assert.Throws<InvalidOperationException>(() => current.RequireCallAgentId());
    }
}
