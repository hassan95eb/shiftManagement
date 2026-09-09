namespace ShiftFlow.Application.Features.Auth.Dtos;

/// <summary>
/// The result of a successful login. <see cref="EmployerId"/> and
/// <see cref="ExpertId"/> mirror the token claims so the frontend does not need
/// to decode the JWT to know which profile it is dealing with.
/// </summary>
public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string TokenType,
    int UserId,
    string Role,
    int? EmployerId,
    int? ExpertId);
