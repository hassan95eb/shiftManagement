namespace ShiftFlow.Application.Features.Auth.Dtos;

/// <summary>
/// The result of a successful login. <see cref="SupervisorId"/> and
/// <see cref="CallAgentId"/> mirror the token claims so the frontend does not need
/// to decode the JWT to know which profile it is dealing with. A Manager has no
/// profile table, so both come back <c>null</c> for one — for a Supervisor or a
/// CallAgent, exactly one of the two is set.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenType,
    int UserId,
    string Role,
    int? SupervisorId,
    int? CallAgentId);
