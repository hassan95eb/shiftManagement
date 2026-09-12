using System.Globalization;
using ShiftFlow.Application.Abstractions;

namespace ShiftFlow.Infrastructure;

/// <summary>
/// Jalali year boundaries based on Tehran civil time. Iran no longer observes
/// DST, so the product-defined fixed UTC+03:30 offset is used deliberately.
/// </summary>
public sealed class PersianLeaveYear : ILeaveYear
{
    private static readonly TimeSpan TehranOffset = TimeSpan.FromHours(3.5);
    private readonly PersianCalendar _calendar = new();

    public LeaveYearRange Resolve(DateTime utcInstant)
    {
        var tehran = AsUtc(utcInstant).Add(TehranOffset);
        var year = _calendar.GetYear(tehran);

        var localStart = _calendar.ToDateTime(year, 1, 1, 0, 0, 0, 0);
        var localEnd = _calendar.ToDateTime(year + 1, 1, 1, 0, 0, 0, 0);
        return new LeaveYearRange(
            DateTime.SpecifyKind(localStart - TehranOffset, DateTimeKind.Utc),
            DateTime.SpecifyKind(localEnd - TehranOffset, DateTimeKind.Utc));
    }

    public LeaveMonthRange ResolveMonth(DateTime utcInstant)
    {
        var tehran = AsUtc(utcInstant).Add(TehranOffset);
        var year = _calendar.GetYear(tehran);
        var month = _calendar.GetMonth(tehran);
        var localStart = _calendar.ToDateTime(year, month, 1, 0, 0, 0, 0);
        var localEnd = _calendar.AddMonths(localStart, 1);
        return new LeaveMonthRange(
            DateTime.SpecifyKind(localStart - TehranOffset, DateTimeKind.Utc),
            DateTime.SpecifyKind(localEnd - TehranOffset, DateTimeKind.Utc));
    }

    private static DateTime AsUtc(DateTime instant) =>
        instant.Kind == DateTimeKind.Utc
            ? instant
            : DateTime.SpecifyKind(instant, DateTimeKind.Utc);
}
