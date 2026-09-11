namespace ShiftFlow.Application.Abstractions;

/// <summary>Resolves Jalali leave-year boundaries as UTC instants.</summary>
public interface ILeaveYear
{
    LeaveYearRange Resolve(DateTime utcInstant);
}

public readonly record struct LeaveYearRange(DateTime StartUtc, DateTime EndUtc);
