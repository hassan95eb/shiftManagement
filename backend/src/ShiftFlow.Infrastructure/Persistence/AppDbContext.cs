using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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

    public DbSet<Supervisor> Supervisors => Set<Supervisor>();

    public DbSet<CallAgent> CallAgents => Set<CallAgent>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<CallAgentProject> CallAgentProjects => Set<CallAgentProject>();

    public DbSet<Availability> Availabilities => Set<Availability>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<ShiftApplication> ShiftApplications => Set<ShiftApplication>();

    public DbSet<Rating> Ratings => Set<Rating>();

    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    public DbSet<AttendanceSession> AttendanceSessions => Set<AttendanceSession>();

    public DbSet<AgentRequest> AgentRequests => Set<AgentRequest>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
