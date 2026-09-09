using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// The authenticated caller of the current request, projected from the JWT
/// claims. Use-case services depend on this for resource-level authorization —
/// a correct role with the wrong resource id must still fail (CLAUDE.md §7).
/// Implemented in Api from <c>HttpContext.User</c>.
/// </summary>
public interface ICurrentUser
{
    /// <summary><c>true</c> when the request carried a valid bearer token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The <c>Users.Id</c> of the caller. Throws when unauthenticated.</summary>
    int UserId { get; }

    /// <summary>The caller's role. Throws when unauthenticated.</summary>
    UserRole Role { get; }

    /// <summary>The caller's <c>Employers.Id</c>, or <c>null</c> for a non-Employer.</summary>
    int? EmployerId { get; }

    /// <summary>The caller's <c>Experts.Id</c>, or <c>null</c> for a non-Expert.</summary>
    int? ExpertId { get; }
}
