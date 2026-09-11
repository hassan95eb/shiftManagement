using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

/// <summary>docs/01-erd-and-schema.md §4-10. Both foreign keys use NO ACTION.</summary>
public sealed class AttendanceSessionConfiguration : IEntityTypeConfiguration<AttendanceSession>
{
    public void Configure(EntityTypeBuilder<AttendanceSession> builder)
    {
        builder.ToTable("AttendanceSessions");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.StartedAtUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(s => s.LastSeenUtc)
            .IsRequired()
            .HasColumnType("datetime2(0)");

        builder.Property(s => s.EndedAtUtc)
            .IsRequired(false)
            .HasColumnType("datetime2(0)");

        builder.HasIndex(s => new { s.CallAgentId, s.StartedAtUtc })
            .HasDatabaseName("IX_AttendanceSessions_CallAgent_StartedAtUtc");

        builder.HasIndex(s => new { s.CallAgentId, s.ShiftId })
            .HasDatabaseName("IX_AttendanceSessions_Open")
            .HasFilter("[EndedAtUtc] IS NULL");

        builder.HasOne(s => s.CallAgent)
            .WithMany(c => c.AttendanceSessions)
            .HasForeignKey(s => s.CallAgentId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(s => s.Shift)
            .WithMany(s => s.AttendanceSessions)
            .HasForeignKey(s => s.ShiftId)
            .IsRequired()
            .OnDelete(DeleteBehavior.NoAction);
    }
}
