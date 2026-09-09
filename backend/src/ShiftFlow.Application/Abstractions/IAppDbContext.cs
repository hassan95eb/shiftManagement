using Microsoft.EntityFrameworkCore;
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
}
