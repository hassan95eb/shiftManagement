using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.Shifts.Dtos;

namespace ShiftFlow.Application.Features.Shifts.Validators;

/// <summary>
/// Semantic validation for <see cref="AssignShiftRequest"/>, on top of the model
/// binder's shape checks. Confirms <c>callAgentId</c> is present and decodes the
/// supplied base64 <c>RowVersion</c> — the same two steps
/// <see cref="ShiftRequestValidator"/> runs for an update, applied here to an
/// assignment instead.
/// </summary>
public static class ShiftAssignmentRequestValidator
{
    /// <returns>The CallAgent id and the decoded row-version token.</returns>
    public static (int CallAgentId, byte[] RowVersion) ValidateAndNormalize(AssignShiftRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (request.CallAgentId is null or <= 0)
        {
            errors["callAgentId"] = ["The callAgentId field is required."];
        }

        var rowVersion = DecodeRowVersion(request.RowVersion, errors);

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (request.CallAgentId!.Value, rowVersion);
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
}
