using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>docs/01-erd-and-schema.md §3-3. CallAgents → Users: CASCADE (§4).</summary>
public sealed class CallAgentConfiguration : IEntityTypeConfiguration<CallAgent>
{
    public void Configure(EntityTypeBuilder<CallAgent> builder)
    {
        builder.ToTable("CallAgents");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.FullName)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(e => e.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasDatabaseName("UQ_CallAgents_UserId");

        builder.HasOne(e => e.User)
            .WithOne(u => u.CallAgent)
            .HasForeignKey<CallAgent>(e => e.UserId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Cascade);
    }
}
