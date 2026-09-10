using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

    DbSet<Employer> Employers { get; }

    DbSet<Expert> Experts { get; }

    DbSet<Project> Projects { get; }

    DbSet<ExpertProject> ExpertProjects { get; }

    DbSet<Availability> Availabilities { get; }

    DbSet<Shift> Shifts { get; }

    DbSet<ShiftApplication> ShiftApplications { get; }

    DbSet<ExpertRating> ExpertRatings { get; }

    DbSet<Recommendation> Recommendations { get; }

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
}
