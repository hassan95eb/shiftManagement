using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Tests.Support;

/// <summary>
/// An in-memory <see cref="ICurrentUser"/> for use-case tests. Mirrors the real
/// <c>CurrentUser</c> contract: <see cref="RequireSupervisorId"/> /
/// <see cref="RequireCallAgentId"/> throw when the id is absent rather than
/// returning a value that would fold into an ownership filter as "match nothing".
/// </summary>
public sealed class StubCurrentUser : ICurrentUser
{
    private StubCurrentUser(int userId, UserRole role, int? supervisorId, int? callAgentId)
    {
        UserId = userId;
        Role = role;
        SupervisorId = supervisorId;
        CallAgentId = callAgentId;
    }

    public bool IsAuthenticated => true;

    public int UserId { get; }

    public UserRole Role { get; }

    public int? SupervisorId { get; }

    public int? CallAgentId { get; }

    public int RequireSupervisorId() =>
        SupervisorId ?? throw new InvalidOperationException("No supervisorId on the current principal.");

    public int RequireCallAgentId() =>
        CallAgentId ?? throw new InvalidOperationException("No callAgentId on the current principal.");

    public static StubCurrentUser Supervisor(int userId, int supervisorId) =>
        new(userId, UserRole.Supervisor, supervisorId, callAgentId: null);

    public static StubCurrentUser CallAgent(int userId, int callAgentId) =>
        new(userId, UserRole.CallAgent, supervisorId: null, callAgentId);
}
