using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Domain.Entities;

/// <summary>A leave or downtime request tied to one committed shift.</summary>
public class AgentRequest
{
    public int Id { get; set; }
    public int CallAgentId { get; set; }
    public int ShiftId { get; set; }
    public AgentRequestType RequestType { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? Reason { get; set; }
    public AgentRequestStatus Status { get; set; }
    public int? DecidedByUserId { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public string? DecisionNote { get; set; }

    public CallAgent CallAgent { get; set; } = null!;
    public Shift Shift { get; set; } = null!;
    public User? DecidedByUser { get; set; }
}
