using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-9. Seed-only table. ExpertRatings → Experts:
/// CASCADE (§4).
/// </summary>
public sealed class ExpertRatingConfiguration : IEntityTypeConfiguration<ExpertRating>
{
    public void Configure(EntityTypeBuilder<ExpertRating> builder)
    {
        builder.ToTable("ExpertRatings", t =>
            t.HasCheckConstraint("CK_ExpertRatings_Score", "[Score] BETWEEN 1.0 AND 5.0"));

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Period)
            .IsRequired()
            .HasColumnType("char(7)")
            .HasMaxLength(7)
            .IsFixedLength();

        builder.Property(r => r.Score)
            .IsRequired()
            .HasColumnType("decimal(2,1)");

        builder.Property(r => r.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(r => new { r.ExpertId, r.Period })
            .IsUnique()
            .HasDatabaseName("UQ_ExpertRatings_Expert_Period");

        builder.HasOne(r => r.Expert)
            .WithMany(e => e.ExpertRatings)
            .HasForeignKey(r => r.ExpertId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
