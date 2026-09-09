using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>docs/01-erd-and-schema.md §3-4. Projects → Employers: CASCADE (§4).</summary>
public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(p => new { p.EmployerId, p.Name })
            .IsUnique()
            .HasDatabaseName("UQ_Projects_Employer_Name");

        builder.HasOne(p => p.Employer)
            .WithMany(e => e.Projects)
            .HasForeignKey(p => p.EmployerId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
