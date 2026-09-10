using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Application.Abstractions;

/// <summary>A signed access token and the instant it expires (UTC).</summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>
/// Issues the JWT access token returned by login. The token carries the user id,
/// the role and the related <c>EmployerId</c> / <c>ExpertId</c> so later phases
/// can authorize a request from its claims without a database round-trip.
/// There is no refresh token (CLAUDE.md §3).
/// </summary>
public interface IJwtTokenService
{
    /// <param name="user">The authenticated account.</param>
    /// <param name="employerId">The caller's <c>Employers.Id</c> when the role is Employer, otherwise <c>null</c>.</param>
    /// <param name="expertId">The caller's <c>Experts.Id</c> when the role is Expert, otherwise <c>null</c>.</param>
    AccessToken CreateAccessToken(User user, int? employerId, int? expertId);
}
