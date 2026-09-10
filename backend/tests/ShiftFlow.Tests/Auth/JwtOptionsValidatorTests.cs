using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShiftFlow.Infrastructure;
using ShiftFlow.Infrastructure.Identity;

namespace ShiftFlow.Tests.Auth;

/// <summary>
/// Startup must fail when <c>Jwt:Key</c> is present-but-empty (the realistic
/// case — <c>appsettings.json</c> ships <c>""</c>) or shorter than 32 bytes,
/// the HMAC-SHA256 minimum (CLAUDE.md §5 auth prompt).
/// </summary>
public class JwtOptionsValidatorTests
{
    private static JwtOptions With(string key) => new()
    {
        Issuer = "shiftflow",
        Audience = "shiftflow",
        Key = key,
        AccessTokenLifetimeMinutes = 60,
    };

    [Fact]
    public void Accepts_a_key_of_exactly_32_bytes()
    {
        var result = new JwtOptionsValidator().Validate(name: null, With(new string('k', 32)));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Rejects_a_present_but_empty_key()
    {
        var result = new JwtOptionsValidator().Validate(name: null, With(string.Empty));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("Jwt:Key") && f.Contains("empty"));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("0123456789012345678901234567890")] // 31 bytes — one short
    public void Rejects_a_key_shorter_than_32_bytes(string key)
    {
        var result = new JwtOptionsValidator().Validate(name: null, With(key));

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, f => f.Contains("32 bytes"));
    }

    [Fact]
    public void MinimumKeyBytes_is_32()
    {
        Assert.Equal(32, JwtOptionsValidator.MinimumKeyBytes);
    }

    // --- through the real DI registration: ValidateOnStart() resolves IOptions
    //     eagerly during host start, so a bad key aborts startup. -------------

    private static IServiceProvider BuildInfrastructure(string? jwtKey)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = "Server=unused;Database=unused;",
            ["Jwt:Issuer"] = "shiftflow",
            ["Jwt:Audience"] = "shiftflow",
            ["Jwt:Key"] = jwtKey,
            ["Jwt:AccessTokenLifetimeMinutes"] = "60",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Startup_fails_when_Jwt_Key_is_empty()
    {
        var provider = BuildInfrastructure(jwtKey: string.Empty);

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        Assert.Contains("Jwt:Key", ex.Message);
    }

    [Fact]
    public void Startup_fails_when_Jwt_Key_is_too_short()
    {
        var provider = BuildInfrastructure(jwtKey: "too-short-for-hmac-sha256");

        var ex = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<JwtOptions>>().Value);
        Assert.Contains("32 bytes", ex.Message);
    }

    [Fact]
    public void Startup_succeeds_with_a_valid_key()
    {
        var provider = BuildInfrastructure(jwtKey: new string('k', 48));

        var options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;

        Assert.Equal(48, options.Key.Length);
    }
}
