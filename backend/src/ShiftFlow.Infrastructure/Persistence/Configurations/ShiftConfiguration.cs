using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>docs/01-erd-and-schema.md §3-7. Shifts → Projects: CASCADE (§4).</summary>
public sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("Shifts", t =>
        {
            t.HasCheckConstraint("CK_Shifts_Range", "[EndUtc] > [StartUtc]");
            t.HasCheckConstraint("CK_Shifts_Status", "[Status] IN ('Open', 'Closed')");
        });

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StartUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(s => s.EndUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(s => s.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16)
            .HasDefaultValue(ShiftStatus.Open);

        builder.Property(s => s.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(s => s.RowVersion)
            .IsRowVersion();

        builder.HasIndex(s => new { s.Status, s.StartUtc })
            .HasDatabaseName("IX_Shifts_Status_StartUtc");

        builder.HasIndex(s => new { s.ProjectId, s.Status })
            .HasDatabaseName("IX_Shifts_ProjectId_Status");

        builder.HasOne(s => s.Project)
            .WithMany(p => p.Shifts)
            .HasForeignKey(s => s.ProjectId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
