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
/// Employer/Expert id, and be signed with the configured issuer/audience/key so
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
    public void Employer_token_carries_sub_role_and_employerId_but_no_expertId()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 7, Username = "acme-admin", Role = UserRole.Employer };

        var token = NewService(now).CreateAccessToken(user, employerId: 42, expertId: null);
        var jwt = Decode(token.Token);

        Assert.Equal("7", jwt.Subject);
        Assert.Equal("Employer", jwt.Claims.Single(c => c.Type == ClaimNames.Role).Value);
        Assert.Equal("42", jwt.Claims.Single(c => c.Type == ClaimNames.EmployerId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimNames.ExpertId);
        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void Expert_token_carries_expertId_but_no_employerId()
    {
        var now = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 3, Username = "temp-jane", Role = UserRole.Expert };

        var token = NewService(now).CreateAccessToken(user, employerId: null, expertId: 99);
        var jwt = Decode(token.Token);

        Assert.Equal("3", jwt.Subject);
        Assert.Equal("Expert", jwt.Claims.Single(c => c.Type == ClaimNames.Role).Value);
        Assert.Equal("99", jwt.Claims.Single(c => c.Type == ClaimNames.ExpertId).Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type == ClaimNames.EmployerId);
    }

    [Fact]
    public void Expiry_is_the_clock_plus_the_configured_lifetime()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 1, Username = "u", Role = UserRole.Employer };

        var token = NewService(now).CreateAccessToken(user, employerId: 1, expertId: null);

        Assert.Equal(now.AddMinutes(45), token.ExpiresAtUtc);
        Assert.Equal(now.AddMinutes(45), Decode(token.Token).ValidTo);
    }

    [Fact]
    public void Token_is_signed_with_the_configured_issuer_audience_and_key()
    {
        var now = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = new User { Id = 5, Username = "u5", Role = UserRole.Employer };
        var token = NewService(now).CreateAccessToken(user, employerId: 5, expertId: null);

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
        var user = new User { Id = 5, Username = "u5", Role = UserRole.Employer };
        var token = NewService(now).CreateAccessToken(user, employerId: 5, expertId: null);

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
