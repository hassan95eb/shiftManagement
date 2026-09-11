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

        // A bare Expression.Constant renders as a SQL literal, so SQL Server
        // would compile and cache a separate plan per supervisor id. Routing
        // the value through a closure — same trick EF.Parameter() codifies —
        // makes it a query parameter instead.
        Expression<Func<int>> boxedSupervisorId = () => supervisorId;
        var equalsOwnSupervisor = Expression.Equal(supervisorIdSelector.Body, boxedSupervisorId.Body);
        var predicate = Expression.Lambda<Func<T, bool>>(equalsOwnSupervisor, supervisorIdSelector.Parameters);

        return query.Where(predicate);
    }
}
