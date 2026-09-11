namespace ShiftFlow.Application.Features.Experts.Dtos;

/// <summary>
/// An expert as returned to an employer. The login username is deliberately
/// omitted: the experts list is a shared pool, not employer-scoped, so echoing
/// usernames would hand every employer half of every other expert's credentials.
/// The password hash is never exposed either.
/// </summary>
public sealed record ExpertResponse(
    int Id,
    int UserId,
    string FullName,
    bool IsActive,
    DateTime CreatedAtUtc);
