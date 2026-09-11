using ShiftFlow.Domain.Entities;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.Attendance;

/// <summary>Pure, reusable attendance calculations used by read models and ratings.</summary>
public static class AttendanceCalculator
{
    /// <summary>
    /// The attendance denominator after approved excused time is removed. A
    /// whole-shift approved leave makes the expected duration zero; approved
    /// downtime removes only the union of its covered intervals.
    /// </summary>
    public static decimal ExpectedHours(Shift shift, IEnumerable<AgentRequest> requests)
    {
        var approved = requests
            .Where(r => r.ShiftId == shift.Id && r.Status == AgentRequestStatus.Approved)
            .ToList();
        if (approved.Any(r => r.RequestType == AgentRequestType.Leave))
        {
            return 0m;
        }

        var downtime = approved
            .Where(r => r.RequestType == AgentRequestType.Downtime)
            .Select(r => (Start: Later(r.StartUtc, shift.StartUtc), End: Earlier(r.EndUtc, shift.EndUtc)))
            .Where(r => r.End > r.Start)
            .OrderBy(r => r.Start)
            .ThenBy(r => r.End)
            .ToList();
        long excusedTicks = 0;
        if (downtime.Count > 0)
        {
            var start = downtime[0].Start;
            var end = downtime[0].End;
            foreach (var interval in downtime.Skip(1))
            {
                if (interval.Start <= end)
                {
                    end = Later(end, interval.End);
                }
                else
                {
                    excusedTicks += (end - start).Ticks;
                    start = interval.Start;
                    end = interval.End;
                }
            }
            excusedTicks += (end - start).Ticks;
        }

        return ((shift.EndUtc - shift.StartUtc).Ticks - excusedTicks) / (decimal)TimeSpan.TicksPerHour;
    }

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
        IEnumerable<AgentRequest> requests,
        DateTime nowUtc,
        bool isCommitted) =>
        isCommitted
        && nowUtc >= shift.EndUtc
        && PresentHours(shift, sessions) == 0m
        && ExpectedHours(shift, requests) > 0m;

    private static DateTime Earlier(DateTime left, DateTime right) => left <= right ? left : right;

    private static DateTime Later(DateTime left, DateTime right) => left >= right ? left : right;
}
