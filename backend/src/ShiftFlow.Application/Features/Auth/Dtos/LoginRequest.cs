using System.ComponentModel.DataAnnotations;

namespace ShiftFlow.Application.Features.Auth.Dtos;

/// <summary>Credentials posted to <c>POST /api/auth/login</c>.</summary>
public sealed class LoginRequest
{
    [Required]
    [MaxLength(64)]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}
