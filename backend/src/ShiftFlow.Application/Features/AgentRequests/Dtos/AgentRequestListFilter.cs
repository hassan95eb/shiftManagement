using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.AgentRequests.Dtos;

public sealed class AgentRequestListFilter
{
    public AgentRequestStatus? Status { get; init; }
}
