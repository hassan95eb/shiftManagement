using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// The authenticated caller of the current request, projected from the JWT
/// claims. Use-case services depend on this for resource-level authorization —
/// a correct role with the wrong resource id must still fail (CLAUDE.md §7).
/// Implemented in Api from <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
/// <see cref="UserId"/> and <see cref="Role"/> throw when the request is
/// unauthenticated or the principal is malformed; they never return <c>0</c>,
/// <c>null</c> or a default role. Only read them from an endpoint that requires
/// authentication. <see cref="EmployerId"/> / <see cref="ExpertId"/> are
/// genuinely optional (one is always <c>null</c> for the other role) and also
/// return <c>null</c> when unauthenticated.
/// </remarks>
public interface ICurrentUser
{
    /// <summary><c>true</c> when the request carried a valid bearer token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The <c>Users.Id</c> of the caller. Throws when unauthenticated or malformed.</summary>
    int UserId { get; }

    /// <summary>The caller's role. Throws when unauthenticated or malformed.</summary>
    UserRole Role { get; }

    /// <summary>The caller's <c>Employers.Id</c>, or <c>null</c> for a non-Employer.</summary>
    int? EmployerId { get; }

    /// <summary>The caller's <c>Experts.Id</c>, or <c>null</c> for a non-Expert.</summary>
    int? ExpertId { get; }
}
