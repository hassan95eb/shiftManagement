namespace ShiftFlow.Application.Features.AgentRequests;

public sealed class AgentRequestOptions
{
    public const string SectionName = "AgentRequests";

    public decimal MonthlyDowntimeHoursCap { get; init; } = 8m;
}
