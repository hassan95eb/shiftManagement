using System.Linq.Expressions;

namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// Applies the caller's reach over Supervisor-owned data directly to a query, in
/// place of the <c>Where(x => x.SupervisorId == _currentUser.RequireSupervisorId())</c>
/// every service used to write by hand. A Manager's "sees everything" is
/// expressed by handing the query back unrestricted — never by an <c>int?</c>
/// that could fold into <c>Where(x => x.SupervisorId == null)</c> and silently
/// match nothing, the same failure mode <see cref="ICurrentUser.RequireSupervisorId"/>
/// already guards against one layer down. A Manager never calls
/// <see cref="ICurrentUser.RequireSupervisorId"/> through this path.
/// </summary>
public interface IAccessScope
{
    /// <summary>
    /// Restricts <paramref name="query"/> to rows whose supervisor — reached via
    /// <paramref name="supervisorIdSelector"/> — is the caller's own. A Manager
    /// gets <paramref name="query"/> back unrestricted. Throws the way
    /// <see cref="ICurrentUser.RequireSupervisorId"/> does when the caller is
    /// neither a Manager nor has a SupervisorId of their own.
    /// </summary>
    IQueryable<T> RestrictToOwnSupervisor<T>(
        IQueryable<T> query,
        Expression<Func<T, int>> supervisorIdSelector);
}
