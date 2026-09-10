namespace ShiftFlow.Application.Features.Experts.Dtos;

/// <summary>
/// An expert as returned to an employer. Carries the login username (the
/// employer set it) but never the password hash.
/// </summary>
public sealed record ExpertResponse(
    int Id,
    int UserId,
    string Username,
    string FullName,
    bool IsActive,
    DateTime CreatedAtUtc);
