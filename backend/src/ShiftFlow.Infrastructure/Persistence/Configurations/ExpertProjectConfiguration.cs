using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-5. Composite PK (ExpertId, ProjectId) blocks
/// duplicate assignment. Expert side CASCADE (path 1), Project side NO ACTION
/// (path 2) — §4.
/// </summary>
public sealed class ExpertProjectConfiguration : IEntityTypeConfiguration<ExpertProject>
{
    public void Configure(EntityTypeBuilder<ExpertProject> builder)
    {
        builder.ToTable("ExpertProjects");

        builder.HasKey(ep => new { ep.ExpertId, ep.ProjectId });

        builder.Property(ep => ep.AssignedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(ep => ep.ProjectId)
            .HasDatabaseName("IX_ExpertProjects_ProjectId");

        builder.HasOne(ep => ep.Expert)
            .WithMany(e => e.ExpertProjects)
            .HasForeignKey(ep => ep.ExpertId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ep => ep.Project)
            .WithMany(p => p.ExpertProjects)
            .HasForeignKey(ep => ep.ProjectId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }
}
