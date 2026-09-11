namespace ShiftFlow.Application.Features.Attendance.Dtos;

public sealed record ActiveAttendanceResponse(
    int Count,
    IReadOnlyList<ActiveAgentResponse> Agents);
