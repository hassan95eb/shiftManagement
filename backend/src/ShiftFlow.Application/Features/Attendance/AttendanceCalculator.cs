using ShiftFlow.Domain.Entities;

namespace ShiftFlow.Application.Features.Attendance;

/// <summary>Pure, reusable attendance calculations used by read models and ratings.</summary>
public static class AttendanceCalculator
{
    public static decimal PresentHours(Shift shift, IEnumerable<AttendanceSession> sessions)
    {
        var intervals = sessions
            .Where(s => s.ShiftId == shift.Id)
            .Select(s => (
                Start: Later(s.StartedAtUtc, shift.StartUtc),
                End: Earlier(s.EndedAtUtc ?? s.LastSeenUtc, shift.EndUtc)))
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();

        if (intervals.Count == 0)
        {
            return 0m;
        }

        long presentTicks = 0;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;

        foreach (var interval in intervals.Skip(1))
        {
            if (interval.Start <= currentEnd)
            {
                currentEnd = Later(currentEnd, interval.End);
                continue;
            }

            presentTicks += (currentEnd - currentStart).Ticks;
            currentStart = interval.Start;
            currentEnd = interval.End;
        }

        presentTicks += (currentEnd - currentStart).Ticks;
        var cappedTicks = Math.Min(presentTicks, (shift.EndUtc - shift.StartUtc).Ticks);
        return cappedTicks / (decimal)TimeSpan.TicksPerHour;
    }

    public static bool IsAbsent(
        Shift shift,
        IEnumerable<AttendanceSession> sessions,
        DateTime nowUtc,
        bool isCommitted,
        bool hasApprovedLeaveOrDowntime) =>
        isCommitted
        && nowUtc >= shift.EndUtc
        && PresentHours(shift, sessions) == 0m
        && !hasApprovedLeaveOrDowntime;

    private static DateTime Earlier(DateTime left, DateTime right) => left <= right ? left : right;

    private static DateTime Later(DateTime left, DateTime right) => left >= right ? left : right;
}
