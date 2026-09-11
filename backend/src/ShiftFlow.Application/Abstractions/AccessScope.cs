using System.Linq.Expressions;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Abstractions;

/// <summary>Default <see cref="IAccessScope"/>, backed by <see cref="ICurrentUser"/>.</summary>
public sealed class AccessScope : IAccessScope
{
    private readonly ICurrentUser _currentUser;

    public AccessScope(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }

    public IQueryable<T> RestrictToOwnSupervisor<T>(
        IQueryable<T> query,
        Expression<Func<T, int>> supervisorIdSelector)
    {
        if (_currentUser.Role == UserRole.Manager)
        {
            return query;
        }

        var supervisorId = _currentUser.RequireSupervisorId();
        var equalsOwnSupervisor = Expression.Equal(supervisorIdSelector.Body, Expression.Constant(supervisorId));
        var predicate = Expression.Lambda<Func<T, bool>>(equalsOwnSupervisor, supervisorIdSelector.Parameters);

        return query.Where(predicate);
    }
}
