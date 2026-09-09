using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-8. Shift side CASCADE (path 1); Expert side and
/// DecidedByUser side NO ACTION (§4). The filtered unique index is the last
/// line of defense against a double-approval race.
/// </summary>
public sealed class ShiftApplicationConfiguration : IEntityTypeConfiguration<ShiftApplication>
{
    public void Configure(EntityTypeBuilder<ShiftApplication> builder)
    {
        builder.ToTable("ShiftApplications", t =>
            t.HasCheckConstraint(
                "CK_ShiftApplications_Status",
                "[Status] IN ('Pending', 'Approved', 'Rejected')"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(16);

        builder.Property(a => a.AppliedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(a => a.DecidedByUserId)
            .IsRequired(false);

        builder.Property(a => a.DecidedAtUtc)
            .IsRequired(false)
            .HasColumnType("datetime2(0)");

        builder.Property(a => a.DecisionNote)
            .IsRequired(false)
            .HasMaxLength(256);

        builder.HasIndex(a => new { a.ShiftId, a.ExpertId })
            .IsUnique()
            .HasDatabaseName("UQ_ShiftApplications_Shift_Expert");

        builder.HasIndex(a => a.ShiftId)
            .IsUnique()
            .HasDatabaseName("UX_ShiftApplications_OneApproved")
            .HasFilter("[Status] = 'Approved'");

        builder.HasIndex(a => new { a.ExpertId, a.Status })
            .HasDatabaseName("IX_ShiftApplications_Expert_Status")
            .IncludeProperties(a => a.ShiftId);

        builder.HasIndex(a => new { a.ShiftId, a.Status })
            .HasDatabaseName("IX_ShiftApplications_Shift_Status");

        builder.HasOne(a => a.Shift)
            .WithMany(s => s.ShiftApplications)
            .HasForeignKey(a => a.ShiftId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Expert)
            .WithMany(e => e.ShiftApplications)
            .HasForeignKey(a => a.ExpertId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(a => a.DecidedByUser)
            .WithMany(u => u.DecidedApplications)
            .HasForeignKey(a => a.DecidedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
