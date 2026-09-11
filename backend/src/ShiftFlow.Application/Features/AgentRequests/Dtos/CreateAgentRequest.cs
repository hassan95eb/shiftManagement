using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.AgentRequests.Dtos;

public sealed class CreateAgentRequest
{
    [Required]
    public int? ShiftId { get; init; }

    [Required]
    public string? RequestType { get; init; }

    [Required]
    public DateTime? StartUtc { get; init; }

    [Required]
    public DateTime? EndUtc { get; init; }

    [MaxLength(256)]
    public string? Reason { get; init; }
}
