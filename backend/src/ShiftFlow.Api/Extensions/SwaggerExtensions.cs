using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ShiftFlow.Api.Extensions;

/// <summary>
/// Swagger generation with a working <b>Authorize</b> button: a single
/// <c>bearer</c> HTTP security scheme, applied as a document-wide requirement so
/// the token is sent on every "Try it out" call once entered. Endpoints marked
/// <see cref="AllowAnonymousAttribute"/> (currently only login) have that
/// requirement removed so the UI does not show them as protected.
/// </summary>
public static class SwaggerExtensions
{
    private const string SchemeId = "bearer";

    public static IServiceCollection AddSwaggerWithBearer(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ShiftFlow API",
                Version = "v1",
                Description = "Call center shift management panel.",
            });

            options.AddSecurityDefinition(SchemeId, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description =
                    "Paste the access token from POST /api/auth/login. "
                    + "Swagger adds the \"Bearer \" prefix for you.",
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeId, document)] = new List<string>(),
            });

            options.OperationFilter<AllowAnonymousOperationFilter>();
        });

        return services;
    }

    /// <summary>Drops the document-wide bearer requirement from anonymous endpoints.</summary>
    private sealed class AllowAnonymousOperationFilter : IOperationFilter
    {
        public void Apply(OpenApiOperation operation, OperationFilterContext context)
        {
            var allowsAnonymous = context.ApiDescription.CustomAttributes().OfType<AllowAnonymousAttribute>().Any();
            if (allowsAnonymous)
            {
                // An explicit empty list overrides the document-wide requirement;
                // null would just inherit it.
                operation.Security = new List<OpenApiSecurityRequirement>();
            }
        }
    }
}
