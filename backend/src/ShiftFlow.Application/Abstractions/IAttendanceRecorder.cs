namespace ShiftFlow.Application.Abstractions;

/// <summary>Best-effort attendance write performed after a CallAgent authenticates.</summary>
public interface IAttendanceRecorder
{
    Task OpenForLoginAsync(int callAgentId, CancellationToken cancellationToken);
}
