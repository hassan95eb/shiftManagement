namespace ShiftFlow.Application.Features.CallAgents.Dtos;

/// <summary>
/// A CallAgent as returned to a supervisor. The login username is deliberately
/// omitted: the CallAgents list is a shared pool, not supervisor-scoped, so echoing
/// usernames would hand every supervisor half of every other CallAgent's credentials.
/// The password hash is never exposed either.
/// </summary>
public sealed record CallAgentResponse(
    int Id,
    int UserId,
    string FullName,
    bool IsActive,
    DateTime CreatedAtUtc);
