namespace ShiftFlow.Application.Features.Attendance;

public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";

    public int StalenessSeconds { get; set; } = 120;

    /// <summary>
    /// The interval the future React client will use between heartbeats. The
    /// backend does not consume it until that client exists.
    /// </summary>
    public int HeartbeatSeconds { get; set; } = 60;
}
