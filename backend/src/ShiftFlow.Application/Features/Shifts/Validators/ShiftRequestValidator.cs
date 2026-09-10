using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts.Dtos;

namespace ShiftFlow.Application.Features.Shifts.Validators;

/// <summary>
/// Semantic validation for the shift write DTOs, on top of the model binder's
/// shape checks. Confirms the required fields are present, normalizes the bounds
/// to whole-second UTC (the storage column is <c>datetime2(0)</c>), and rejects
/// a window that does not end strictly after it starts — the same rule as the
/// <c>CK_Shifts_Range</c> check constraint. On update it also decodes the
/// supplied base64 <c>RowVersion</c>.
/// </summary>
public static class ShiftRequestValidator
{
    /// <returns>The project id and the normalized, validated window.</returns>
    public static (int ProjectId, DateTime StartUtc, DateTime EndUtc) ValidateAndNormalize(
        CreateShiftRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.ProjectId is null or <= 0)
        {
            errors["projectId"] = ["The projectId field is required."];
        }

        var (start, end) = ValidateWindow(request.StartUtc, request.EndUtc, errors);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (request.ProjectId!.Value, start, end);
    }

    /// <returns>The normalized window and the decoded row-version token.</returns>
    public static (DateTime StartUtc, DateTime EndUtc, byte[] RowVersion) ValidateAndNormalize(
        UpdateShiftRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        var (start, end) = ValidateWindow(request.StartUtc, request.EndUtc, errors);
        var rowVersion = DecodeRowVersion(request.RowVersion, errors);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (start, end, rowVersion);
    }

    private static (DateTime Start, DateTime End) ValidateWindow(
        DateTime? rawStart,
        DateTime? rawEnd,
        Dictionary<string, string[]> errors)
    {
        if (rawStart is null)
        {
            errors["startUtc"] = ["The startUtc field is required."];
        }

        if (rawEnd is null)
        {
            errors["endUtc"] = ["The endUtc field is required."];
        }

        if (rawStart is null || rawEnd is null)
        {
            return default;
        }

        var start = ToWholeSecondsUtc(rawStart.Value);
        var end = ToWholeSecondsUtc(rawEnd.Value);

        if (end <= start)
        {
            errors["endUtc"] = ["The shift must end after it starts."];
        }

        return (start, end);
    }

    private static byte[] DecodeRowVersion(string? raw, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            errors["rowVersion"] = ["The rowVersion field is required."];
            return [];
        }

        try
        {
            var bytes = Convert.FromBase64String(raw);
            if (bytes.Length == 0)
            {
                errors["rowVersion"] = ["The rowVersion field is not a valid token."];
            }

            return bytes;
        }
        catch (FormatException)
        {
            errors["rowVersion"] = ["The rowVersion field is not a valid token."];
            return [];
        }
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
