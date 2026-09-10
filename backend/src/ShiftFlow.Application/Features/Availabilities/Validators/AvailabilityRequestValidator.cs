using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Availabilities.Dtos;

namespace ShiftFlow.Application.Features.Availabilities.Validators;

/// <summary>
/// Semantic validation for <see cref="AvailabilityRequest"/>, on top of the
/// model binder's shape checks. Confirms both bounds are present, normalizes
/// them to whole-second UTC (the storage column is <c>datetime2(0)</c>, so
/// merge arithmetic runs on the same precision the database keeps), and rejects
/// a window that does not end strictly after it starts — the same rule as the
/// <c>CK_Availabilities_Range</c> check constraint.
/// </summary>
public static class AvailabilityRequestValidator
{
    /// <returns>The normalized, validated window.</returns>
    public static TimeRange ValidateAndNormalize(AvailabilityRequest request)
    {
        var missing = new Dictionary<string, string[]>();
        if (request.StartUtc is null)
        {
            missing["startUtc"] = ["The startUtc field is required."];
        }

        if (request.EndUtc is null)
        {
            missing["endUtc"] = ["The endUtc field is required."];
        }

        if (missing.Count > 0)
        {
            throw new ValidationException(missing);
        }

        var start = ToWholeSecondsUtc(request.StartUtc!.Value);
        var end = ToWholeSecondsUtc(request.EndUtc!.Value);

        if (end <= start)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                ["endUtc"] = ["The window must end after it starts."],
            });
        }

        return new TimeRange(start, end);
    }

    /// <summary>
    /// Coerces an incoming value to UTC and drops any sub-second part. An
    /// offset-bearing timestamp is converted; a bare local one is converted from
    /// the server's zone; a kind-less one is taken to already be UTC, matching
    /// the "everything is UTC, everywhere" rule (CLAUDE.md §4).
    /// </summary>
    private static DateTime ToWholeSecondsUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

        return new DateTime(
            utc.Ticks - (utc.Ticks % TimeSpan.TicksPerSecond),
            DateTimeKind.Utc);
    }
}
