using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-10. Written only by the Python script via
/// MERGE. Shift side CASCADE (path 1), Expert side NO ACTION (path 2) — §4.
/// </summary>
public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("Recommendations", t =>
            t.HasCheckConstraint("CK_Recommendations_Score", "[Score] BETWEEN 0 AND 100"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Score)
            .IsRequired()
            .HasColumnType("decimal(5,2)");

        builder.Property(r => r.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(r => r.ComputedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(r => new { r.ShiftId, r.ExpertId })
            .IsUnique()
            .HasDatabaseName("UQ_Recommendations_Shift_Expert");

        // (ShiftId ASC, Score DESC) — read a shift's ranking best-first.
        builder.HasIndex(r => new { r.ShiftId, r.Score })
            .HasDatabaseName("IX_Recommendations_Shift_Score")
            .IsDescending(false, true);

        builder.HasOne(r => r.Shift)
            .WithMany(s => s.Recommendations)
            .HasForeignKey(r => r.ShiftId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Expert)
            .WithMany(e => e.Recommendations)
            .HasForeignKey(r => r.ExpertId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }
}
