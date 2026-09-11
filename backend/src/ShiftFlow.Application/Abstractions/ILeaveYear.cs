namespace ShiftFlow.Application.Abstractions;

/// <summary>Resolves Jalali leave-year boundaries as UTC instants.</summary>
public interface ILeaveYear
{
    LeaveYearRange Resolve(DateTime utcInstant);

    LeaveMonthRange ResolveMonth(DateTime utcInstant);
}

public readonly record struct LeaveYearRange(DateTime StartUtc, DateTime EndUtc);

public readonly record struct LeaveMonthRange(DateTime StartUtc, DateTime EndUtc);
