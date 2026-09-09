using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// docs/01-erd-and-schema.md §3-6. Availabilities → Experts: CASCADE (§4).
/// The "no overlapping/adjacent windows" rule is enforced in the service layer
/// by merge-on-insert, not in the database.
/// </summary>
public sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> builder)
    {
        builder.ToTable("Availabilities", t =>
            t.HasCheckConstraint("CK_Availabilities_Range", "[EndUtc] > [StartUtc]"));

        builder.HasKey(a => a.Id);

        builder.Property(a => a.StartUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(a => a.EndUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(a => a.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(a => new { a.ExpertId, a.StartUtc, a.EndUtc })
            .HasDatabaseName("IX_Availabilities_Expert_Range");

        builder.HasOne(a => a.Expert)
            .WithMany(e => e.Availabilities)
            .HasForeignKey(a => a.ExpertId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
