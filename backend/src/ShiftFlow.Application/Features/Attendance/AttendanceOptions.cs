namespace ShiftFlow.Application.Features.Attendance;

public sealed class AttendanceOptions
{
    public const string SectionName = "Attendance";

    public int StalenessThresholdSeconds { get; set; } = 120;
}
