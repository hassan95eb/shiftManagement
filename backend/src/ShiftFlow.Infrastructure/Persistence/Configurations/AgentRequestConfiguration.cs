using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Infrastructure.Persistence.Configurations;

public sealed class AgentRequestConfiguration : IEntityTypeConfiguration<AgentRequest>
{
    public void Configure(EntityTypeBuilder<AgentRequest> builder)
    {
        builder.ToTable("AgentRequests", table =>
        {
            table.HasCheckConstraint("CK_AgentRequests_RequestType", "[RequestType] IN ('Leave', 'Downtime')");
            table.HasCheckConstraint("CK_AgentRequests_Status", "[Status] IN ('Pending', 'Approved', 'Rejected')");
            table.HasCheckConstraint("CK_AgentRequests_Range", "[EndUtc] > [StartUtc]");
        });

        builder.HasKey(r => r.Id);
        builder.Property(r => r.RequestType).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.RequestedAtUtc).IsRequired().HasColumnType("datetime2(0)");
        builder.Property(r => r.StartUtc).IsRequired().HasColumnType("datetime2(0)");
        builder.Property(r => r.EndUtc).IsRequired().HasColumnType("datetime2(0)");
        builder.Property(r => r.Reason).IsRequired(false).HasMaxLength(256);
        builder.Property(r => r.Status).IsRequired().HasConversion<string>().HasMaxLength(16);
        builder.Property(r => r.DecidedAtUtc).IsRequired(false).HasColumnType("datetime2(0)");
        builder.Property(r => r.DecisionNote).IsRequired(false).HasMaxLength(256);

        builder.HasIndex(r => new { r.CallAgentId, r.RequestType, r.Status })
            .HasDatabaseName("IX_AgentRequests_CallAgent_Type_Status");
        builder.HasIndex(r => r.ShiftId)
            .IsUnique()
            .HasDatabaseName("UX_AgentRequests_OneApprovedLeave")
            .HasFilter("[Status] = 'Approved' AND [RequestType] = 'Leave'");

        builder.HasOne(r => r.CallAgent).WithMany(a => a.AgentRequests)
            .HasForeignKey(r => r.CallAgentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(r => r.Shift).WithMany(s => s.AgentRequests)
            .HasForeignKey(r => r.ShiftId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(r => r.DecidedByUser).WithMany(u => u.DecidedAgentRequests)
            .HasForeignKey(r => r.DecidedByUserId).OnDelete(DeleteBehavior.NoAction);
    }
}
