using Microsoft.EntityFrameworkCore;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence;

/// <summary>
/// The one EF Core <see cref="DbContext"/> for ShiftFlow. It owns no business
/// logic; every table shape, constraint, index and delete rule comes from an
/// <see cref="IEntityTypeConfiguration{TEntity}"/> in
/// <c>Persistence/Configurations</c>, one file per entity, matched line by line
/// against docs/01-erd-and-schema.md.
/// </summary>
public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Employer> Employers => Set<Employer>();

    public DbSet<Expert> Experts => Set<Expert>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ExpertProject> ExpertProjects => Set<ExpertProject>();

    public DbSet<Availability> Availabilities => Set<Availability>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<ShiftApplication> ShiftApplications => Set<ShiftApplication>();

    public DbSet<ExpertRating> ExpertRatings => Set<ExpertRating>();

    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
