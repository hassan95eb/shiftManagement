using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Availabilities.Dtos;

/// <summary>
/// Body of both <c>POST /api/availability</c> and <c>PUT /api/availability/{id}</c>
/// — the two payloads are identical, so one DTO serves both. The owning CallAgent
/// comes from the token, never the body. Both bounds are nullable so a request
/// that omits one is rejected with the uniform 400 shape rather than being read
/// as <c>0001-01-01</c>.
/// </summary>
public sealed class AvailabilityRequest
{
    [Required]
    public DateTime? StartUtc { get; init; }

    [Required]
    public DateTime? EndUtc { get; init; }
}
