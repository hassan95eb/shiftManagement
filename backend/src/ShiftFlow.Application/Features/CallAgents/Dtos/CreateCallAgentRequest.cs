using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.CallAgents.Dtos;

/// <summary>
/// Body of <c>POST /api/call-agents</c>. The supervisor supplies the new specialist's
/// initial login. A real system would email an invitation instead of setting the
/// password directly; that trade-off is noted in the README.
/// </summary>
public sealed class CreateCallAgentRequest
{
    [Required]
    [MaxLength(64)]
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// No strength policy is enforced — the brief does not define one — only
    /// presence and a maximum length so the hash input is bounded.
    /// </summary>
    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string FullName { get; init; } = string.Empty;
}
