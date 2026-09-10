namespace ShiftFlow.Infrastructure.Identity;

/// <summary>
/// JWT settings, bound from the <c>Jwt</c> configuration section. Issuer,
/// audience and lifetime have safe defaults in <c>appsettings.json</c>;
/// <see cref="Key"/> is a secret and must come from the environment
/// (<c>Jwt__Key</c>). Validated on startup in
/// <see cref="DependencyInjection.AddInfrastructure"/>. No refresh token
/// (CLAUDE.md §3).
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key. At least 32 bytes.</summary>
    public string Key { get; init; } = string.Empty;

    public int AccessTokenLifetimeMinutes { get; init; } = 60;
}
