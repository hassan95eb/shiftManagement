namespace ShiftFlow.Application.Features.Attendance.Dtos;

public sealed record ActiveAgentResponse(
    int CallAgentId,
    string FullName,
    int ShiftId,
    int ProjectId,
    string ProjectName,
    DateTime StartedAtUtc,
    DateTime LastSeenUtc);
