using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// The Application layer's view of the database. Deliberately not a repository:
/// services compose LINQ queries directly against these sets and persist with
/// <see cref="SaveChangesAsync"/> (CLAUDE.md §4). Implemented by
/// <c>AppDbContext</c> in Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Supervisor> Supervisors { get; }

    DbSet<CallAgent> CallAgents { get; }

    DbSet<Project> Projects { get; }

    DbSet<CallAgentProject> CallAgentProjects { get; }

    DbSet<Availability> Availabilities { get; }

    DbSet<Shift> Shifts { get; }

    DbSet<ShiftApplication> ShiftApplications { get; }

    DbSet<Rating> Ratings { get; }

    DbSet<Recommendation> Recommendations { get; }

    DbSet<AttendanceSession> AttendanceSessions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The change-tracking entry for <paramref name="entity"/>. Needed to set a
    /// concurrency token's <c>OriginalValue</c> to the <c>RowVersion</c> the
    /// client last read, so a stale write fails with
    /// <see cref="DbUpdateConcurrencyException"/> rather than silently
    /// overwriting a newer version (docs/01-erd-and-schema.md §3-7).
    /// </summary>
    EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        where TEntity : class;

    /// <summary>
    /// Opens an explicit database transaction. The approval flow (CLAUDE.md §5,
    /// docs/01-erd-and-schema.md §6) is the one place a single
    /// <see cref="SaveChangesAsync"/> is not enough: the approved application,
    /// the shift's move to <c>Closed</c> and the sibling rejections have to
    /// commit or roll back as a unit, and rule 5 is re-checked inside that same
    /// transaction so the decision is made against a state that cannot shift
    /// under it. Every other use case stays on the implicit
    /// <see cref="SaveChangesAsync"/> transaction.
    /// </summary>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
