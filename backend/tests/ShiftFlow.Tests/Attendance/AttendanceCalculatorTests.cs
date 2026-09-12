using ShiftFlow.Application.Features.Attendance;
using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Tests.Attendance;

public class AttendanceCalculatorTests
{
    private static readonly DateTime Start = new(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);

    private static Shift Shift() => new() { Id = 7, StartUtc = Start, EndUtc = Start.AddHours(8) };

    private static AttendanceSession Session(double startHour, double endHour) => new()
    {
        ShiftId = 7,
        StartedAtUtc = Start.AddHours(startHour),
        LastSeenUtc = Start.AddHours(endHour),
        EndedAtUtc = Start.AddHours(endHour),
    };

    [Fact]
    public void Overlapping_sessions_do_not_double_count_present_hours()
    {
        var hours = AttendanceCalculator.PresentHours(
            Shift(),
            [Session(0, 4), Session(2, 6)]);

        Assert.Equal(6m, hours);
    }

    [Fact]
    public void Session_time_is_clipped_to_the_shift_window()
    {
        var hours = AttendanceCalculator.PresentHours(
            Shift(),
            [Session(-2, 10), Session(0, 8)]);

        Assert.Equal(8m, hours);
    }

    [Fact]
    public void Ended_committed_shift_with_no_session_and_no_excuse_is_absent()
    {
        Assert.True(AttendanceCalculator.IsAbsent(Shift(), [], [], Start.AddHours(9), true));
    }

    [Fact]
    public void Partial_session_is_not_an_absence()
    {
        Assert.False(AttendanceCalculator.IsAbsent(
            Shift(), [Session(1, 2)], [], Start.AddHours(9), true));
    }

    [Fact]
    public void Partial_downtime_with_zero_presence_is_still_an_absence()
    {
        var shift = Shift();
        var downtime = new AgentRequest
        {
            ShiftId = shift.Id,
            RequestType = AgentRequestType.Downtime,
            Status = AgentRequestStatus.Approved,
            StartUtc = shift.StartUtc,
            EndUtc = shift.StartUtc.AddMinutes(15),
        };

        Assert.True(AttendanceCalculator.IsAbsent(shift, [], [downtime], Start.AddHours(9), true));
    }

    [Fact]
    public void Approved_leave_removes_the_whole_shift_from_expected_hours()
    {
        var shift = Shift();
        var leave = new AgentRequest
        {
            ShiftId = shift.Id,
            RequestType = AgentRequestType.Leave,
            Status = AgentRequestStatus.Approved,
            StartUtc = shift.StartUtc,
            EndUtc = shift.EndUtc,
        };

        Assert.Equal(0m, AttendanceCalculator.ExpectedHours(shift, [leave]));
        Assert.False(AttendanceCalculator.IsAbsent(shift, [], [leave], shift.EndUtc, true));
    }
}
