using ShiftFlow.Application.Common;
using ShiftFlow.Application.Features.AgentRequests.Dtos;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Application.Features.AgentRequests.Validators;

public static class AgentRequestValidator
{
    public static (int ShiftId, AgentRequestType Type, DateTime StartUtc, DateTime EndUtc, string? Reason)
        ValidateAndNormalize(CreateAgentRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ShiftId is null or <= 0)
        {
            errors["shiftId"] = ["The shiftId field is required."];
        }

        if (!Enum.TryParse<AgentRequestType>(request.RequestType, true, out var type)
            || !Enum.IsDefined(type))
        {
            errors["requestType"] = ["RequestType must be Leave or Downtime."];
        }

        if (request.StartUtc is null)
        {
            errors["startUtc"] = ["The startUtc field is required."];
        }
        if (request.EndUtc is null)
        {
            errors["endUtc"] = ["The endUtc field is required."];
        }

        var start = request.StartUtc is null ? default : WholeSecondUtc(request.StartUtc.Value);
        var end = request.EndUtc is null ? default : WholeSecondUtc(request.EndUtc.Value);
        if (request.StartUtc is not null && request.EndUtc is not null && end <= start)
        {
            errors["endUtc"] = ["The request must end after it starts."];
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (reason?.Length > 256)
        {
            errors["reason"] = ["Reason must not exceed 256 characters."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        return (request.ShiftId!.Value, type, start, end, reason);
    }

    private static DateTime WholeSecondUtc(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        return new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }
}
