using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Tests.Support;

/// <summary>
/// An in-memory <see cref="ICurrentUser"/> for use-case tests. Mirrors the real
/// <c>CurrentUser</c> contract: <see cref="RequireEmployerId"/> /
/// <see cref="RequireExpertId"/> throw when the id is absent rather than
/// returning a value that would fold into an ownership filter as "match nothing".
/// </summary>
public sealed class StubCurrentUser : ICurrentUser
{
    private StubCurrentUser(int userId, UserRole role, int? employerId, int? expertId)
    {
        UserId = userId;
        Role = role;
        EmployerId = employerId;
        ExpertId = expertId;
    }

    public bool IsAuthenticated => true;

    public int UserId { get; }

    public UserRole Role { get; }

    public int? EmployerId { get; }

    public int? ExpertId { get; }

    public int RequireEmployerId() =>
        EmployerId ?? throw new InvalidOperationException("No employerId on the current principal.");

    public int RequireExpertId() =>
        ExpertId ?? throw new InvalidOperationException("No expertId on the current principal.");

    public static StubCurrentUser Employer(int userId, int employerId) =>
        new(userId, UserRole.Employer, employerId, expertId: null);

    public static StubCurrentUser Expert(int userId, int expertId) =>
        new(userId, UserRole.Expert, employerId: null, expertId);
}
