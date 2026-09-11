using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Identity;

/// <summary>
/// Issues HMAC-SHA256 signed JWTs. Expiry is derived from <see cref="IClock"/>
/// so token lifetime is deterministic under test. The token carries
/// <c>sub</c> (user id), <c>role</c>, and <c>supervisorId</c> or <c>callAgentId</c>
/// (<see cref="ClaimNames"/>).
/// </summary>
public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenService(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public AccessToken CreateAccessToken(User user, int? supervisorId, int? callAgentId)
    {
        var issuedAt = _clock.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(ClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimNames.Role, user.Role.ToString()),
        };

        if (supervisorId is int resolvedSupervisorId)
        {
            claims.Add(new Claim(ClaimNames.SupervisorId, resolvedSupervisorId.ToString(CultureInfo.InvariantCulture)));
        }

        if (callAgentId is int resolvedCallAgentId)
        {
            claims.Add(new Claim(ClaimNames.CallAgentId, resolvedCallAgentId.ToString(CultureInfo.InvariantCulture)));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt,
            expires: expiresAt,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(encoded, expiresAt);
    }
}
