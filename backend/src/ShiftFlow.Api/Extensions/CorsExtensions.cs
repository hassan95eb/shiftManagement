namespace ShiftFlow.Api.Extensions;

/// <summary>
/// CORS for the non-Docker dev setup only: the Vite dev server on
/// <c>http://localhost:5173</c> calling the API directly. In Docker the frontend
/// is served by nginx which proxies <c>/api</c>, so requests are same-origin and
/// this policy is not used (CLAUDE.md §11).
/// </summary>
public static class CorsExtensions
{
    public const string LocalDevPolicy = "local-dev";

    private const string ViteDevServerOrigin = "http://localhost:5173";

    public static IServiceCollection AddLocalDevCors(this IServiceCollection services) =>
        services.AddCors(options =>
            options.AddPolicy(LocalDevPolicy, policy =>
                policy
                    .WithOrigins(ViteDevServerOrigin)
                    .AllowAnyHeader()
                    .AllowAnyMethod()));
}
