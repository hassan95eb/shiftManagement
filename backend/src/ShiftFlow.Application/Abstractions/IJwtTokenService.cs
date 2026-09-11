using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Application.Abstractions;

/// <summary>A signed access token and the instant it expires (UTC).</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Issues the JWT access token returned by login. The token carries the user id,
/// the role and the related <c>SupervisorId</c> / <c>CallAgentId</c> so later phases
/// can authorize a request from its claims without a database round-trip.
/// There is no refresh token (CLAUDE.md §3).
/// </summary>
public interface IJwtTokenService
{
    /// <param name="user">The authenticated account.</param>
    /// <param name="supervisorId">The caller's <c>Supervisors.Id</c> when the role is Supervisor, otherwise <c>null</c>.</param>
    /// <param name="callAgentId">The caller's <c>CallAgents.Id</c> when the role is CallAgent, otherwise <c>null</c>.</param>
    AccessToken CreateAccessToken(User user, int? supervisorId, int? callAgentId);
}
