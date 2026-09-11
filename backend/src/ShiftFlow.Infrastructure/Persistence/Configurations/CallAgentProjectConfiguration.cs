using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-5. Composite PK (CallAgentId, ProjectId) blocks
/// duplicate assignment. CallAgent side CASCADE (path 1), Project side NO ACTION
/// (path 2) — §4.
/// </summary>
public sealed class CallAgentProjectConfiguration : IEntityTypeConfiguration<CallAgentProject>
{
    public void Configure(EntityTypeBuilder<CallAgentProject> builder)
    {
        builder.ToTable("CallAgentProjects");

        builder.HasKey(ep => new { ep.CallAgentId, ep.ProjectId });

        builder.Property(ep => ep.AssignedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(ep => ep.ProjectId)
            .HasDatabaseName("IX_CallAgentProjects_ProjectId");

        builder.HasOne(ep => ep.CallAgent)
            .WithMany(e => e.CallAgentProjects)
            .HasForeignKey(ep => ep.CallAgentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ep => ep.Project)
            .WithMany(p => p.CallAgentProjects)
            .HasForeignKey(ep => ep.ProjectId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }
}
