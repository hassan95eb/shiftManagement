using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-9. Seed-only table. Ratings → CallAgents:
/// CASCADE (§4).
/// </summary>
public sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings", t =>
            t.HasCheckConstraint("CK_Ratings_Score", "[Score] BETWEEN 1.0 AND 5.0"));

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

        builder.HasIndex(r => new { r.CallAgentId, r.Period })
            .IsUnique()
            .HasDatabaseName("UQ_Ratings_CallAgent_Period");

        builder.HasOne(r => r.CallAgent)
            .WithMany(e => e.Ratings)
            .HasForeignKey(r => r.CallAgentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
