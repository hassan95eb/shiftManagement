using System.Text;
using Microsoft.Extensions.Options;

namespace ShiftFlow.Infrastructure.Identity;

/// <summary>
/// Startup validation for <see cref="JwtOptions"/>. Registered with
/// <c>ValidateOnStart()</c> so the host refuses to start on bad JWT config, and
/// reused by the API's authentication wiring for the same check at registration
/// time. The empty-key case is the realistic one: <c>appsettings.json</c> ships
/// <c>"Jwt:Key": ""</c> and expects the real value from <c>Jwt__Key</c>.
/// </summary>
public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    /// <summary>Minimum signing-key size for HMAC-SHA256.</summary>
    public const int MinimumKeyBytes = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            failures.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            failures.Add("Jwt:Audience is required.");
        }

        if (string.IsNullOrEmpty(options.Key))
        {
            failures.Add("Jwt:Key is empty. Supply it from the environment as Jwt__Key.");
        }
        else if (Encoding.UTF8.GetByteCount(options.Key) < MinimumKeyBytes)
        {
            failures.Add($"Jwt:Key must be at least {MinimumKeyBytes} bytes for HMAC-SHA256.");
        }

        if (options.AccessTokenLifetimeMinutes <= 0)
        {
            failures.Add("Jwt:AccessTokenLifetimeMinutes must be positive.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
