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
/// authentication. <see cref="SupervisorId"/> / <see cref="CallAgentId"/> are
/// genuinely optional — for a Supervisor or a CallAgent, exactly one is set and
/// the other is <c>null</c>; a Manager has no profile table, so both are
/// <c>null</c> — and both also return <c>null</c> when unauthenticated.
/// </remarks>
public interface ICurrentUser
{
    /// <summary><c>true</c> when the request carried a valid bearer token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>The <c>Users.Id</c> of the caller. Throws when unauthenticated or malformed.</summary>
    int UserId { get; }

    /// <summary>The caller's role. Throws when unauthenticated or malformed.</summary>
    UserRole Role { get; }

    /// <summary>The caller's <c>Supervisors.Id</c>, or <c>null</c> for a non-Supervisor.</summary>
    int? SupervisorId { get; }

    /// <summary>The caller's <c>CallAgents.Id</c>, or <c>null</c> for a non-CallAgent.</summary>
    int? CallAgentId { get; }

    /// <summary>
    /// The caller's <c>Supervisors.Id</c>, or throws when it is absent. Ownership
    /// checks must call this instead of reading <see cref="SupervisorId"/>: a
    /// <c>null</c> silently folded into <c>Where(x =&gt; x.SupervisorId == null)</c>
    /// turns an authorization filter into a query that quietly returns nothing
    /// instead of failing (CLAUDE.md §7).
    /// </summary>
    int RequireSupervisorId();

    /// <summary>
    /// The caller's <c>CallAgents.Id</c>, or throws when it is absent. Same reason
    /// as <see cref="RequireSupervisorId"/>: an ownership filter must never fall
    /// back to a <c>null</c> comparison.
    /// </summary>
    int RequireCallAgentId();
}
