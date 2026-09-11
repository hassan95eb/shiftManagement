using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;
using ShiftFlow.Infrastructure.Identity;

namespace ShiftFlow.Tests.Auth;

/// <summary>
/// The issued token must carry the user id, the role and the related
/// Supervisor/CallAgent id, and be signed with the configured issuer/audience/key so
/// later phases can authorize straight from the claims (CLAUDE.md §5 auth prompt).
/// </summary>
public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "shiftflow-test",
        Audience = "shiftflow-test-audience",
        Key = "unit-test-signing-key-long-enough-to-be-valid-0123456789",
        AccessTokenLifetimeMinutes = 45,
    };

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private static JwtTokenService NewService(DateTime now) =>
        new(Microsoft.Extensions.Options.Options.Create(Options), new FixedClock(now));

    private static JwtSecurityToken Decode(string token) =>
        new JwtSecurityTokenHandler().ReadJwtToken(token);

    [Fact]
    public void Supervisor_token_carries_sub_role_and_supervisorId_but_no_callAgentId()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 7, Username = "acme-admin", Role = UserRole.Supervisor };

        var token = NewService(now).CreateAccessToken(user, supervisorId: 42, callAgentId: null);
        var jwt = Decode(token.Token);

        Assert.Equal("7", jwt.Subject);
        Assert.Equal("Supervisor", jwt.Claims.Single(c => c.Type == ClaimNames.Role).Value);
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == ClaimNames.SupervisorId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimNames.CallAgentId);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void CallAgent_token_carries_callAgentId_but_no_supervisorId()
    {
        var now = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 3, Username = "temp-jane", Role = UserRole.CallAgent };

        var token = NewService(now).CreateAccessToken(user, supervisorId: null, callAgentId: 99);
        var jwt = Decode(token.Token);

        Assert.Equal("3", jwt.Subject);
        Assert.Equal("CallAgent", jwt.Claims.Single(c => c.Type == ClaimNames.Role).Value);
        Assert.Equal("99", jwt.Claims.Single(c => c.Type == ClaimNames.CallAgentId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimNames.SupervisorId);
    }

    [Fact]
    public void Expiry_is_the_clock_plus_the_configured_lifetime()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 1, Username = "u", Role = UserRole.Supervisor };

        var token = NewService(now).CreateAccessToken(user, supervisorId: 1, callAgentId: null);

        Assert.Equal(now.AddMinutes(45), token.ExpiresAtUtc);
        Assert.Equal(now.AddMinutes(45), Decode(token.Token).ValidTo);
    }

    [Fact]
    public void Token_is_signed_with_the_configured_issuer_audience_and_key()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 5, Username = "u5", Role = UserRole.Supervisor };
        var token = NewService(now).CreateAccessToken(user, supervisorId: 5, callAgentId: null);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Options.Issuer,
            ValidateAudience = true,
            ValidAudience = Options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.Key)),
            ValidateLifetime = false,
        };

        var principal = new JwtSecurityTokenHandler().ValidateToken(token.Token, parameters, out var validated);

        Assert.True(principal.Identity?.IsAuthenticated);
        Assert.Equal(Options.Issuer, ((JwtSecurityToken)validated).Issuer);
    }

    [Fact]
    public void Wrong_signing_key_fails_validation()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 5, Username = "u5", Role = UserRole.Supervisor };
        var token = NewService(now).CreateAccessToken(user, supervisorId: 5, callAgentId: null);

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("a-different-key-that-is-also-long-enough-9876543210")),
        };

        Assert.ThrowsAny<SecurityTokenException>(
            () => new JwtSecurityTokenHandler().ValidateToken(token.Token, parameters, out _));
    }
}
