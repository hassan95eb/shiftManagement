using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>docs/01-erd-and-schema.md §3-2. Supervisors → Users: CASCADE (§4).</summary>
public sealed class SupervisorConfiguration : IEntityTypeConfiguration<Supervisor>
{
    public void Configure(EntityTypeBuilder<Supervisor> builder)
    {
        builder.ToTable("Supervisors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("UQ_Supervisors_UserId");

        builder.HasOne(e => e.User)
            .WithOne(u => u.Supervisor)
            .HasForeignKey<Supervisor>(e => e.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
