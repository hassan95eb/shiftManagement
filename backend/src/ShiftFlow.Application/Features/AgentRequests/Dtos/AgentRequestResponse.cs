namespace ShiftFlow.Application.Features.AgentRequests.Dtos;

public sealed record AgentRequestResponse(
    int Id,
    int CallAgentId,
    int ShiftId,
    string RequestType,
    DateTime RequestedAtUtc,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Reason,
    string Status,
    int? DecidedByUserId,
    DateTime? DecidedAtUtc,
    string? DecisionNote,
    int? RemainingLeaveDays);
