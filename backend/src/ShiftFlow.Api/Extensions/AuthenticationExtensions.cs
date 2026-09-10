using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Infrastructure.Identity;

namespace ShiftFlow.Api.Extensions;

/// <summary>
/// JWT bearer authentication + authorization. Validation parameters are built
/// from the same <c>Jwt</c> section that <c>AddInfrastructure</c> binds and
/// validates, so the issuer/audience/key the API checks are exactly the ones the
/// token was signed with.
/// </summary>
public static class AuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        // Same rules as the ValidateOnStart() check in AddInfrastructure, run here
        // too so misconfiguration fails at registration, before the host is built.
        var validation = new JwtOptionsValidator().Validate(name: null, jwt);
        if (validation.Failed)
        {
            throw new InvalidOperationException(
                "Invalid JWT configuration: " + string.Join(" ", validation.Failures));
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = ClaimNames.Sub,
                    RoleClaimType = ClaimNames.Role,
                };
            });

        services.AddAuthorization();

        return services;
    }
}
