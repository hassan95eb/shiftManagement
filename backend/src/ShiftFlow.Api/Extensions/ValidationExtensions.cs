using Microsoft.AspNetCore.Mvc;
using ShiftFlow.Api.Contracts;

namespace ShiftFlow.Api.Extensions;

/// <summary>
/// Rewrites the automatic <c>[ApiController]</c> model-binding 400 into the same
/// <see cref="ApiError"/> shape the exception middleware produces, so a client
/// only ever has to parse one error format.
/// </summary>
public static class ValidationExtensions
{
    public static IServiceCollection AddUniformModelValidationErrors(this IServiceCollection services) =>
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var details = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .ToDictionary(
                        entry => entry.Key,
                        entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                var error = new ApiError
                {
                    Status = StatusCodes.Status400BadRequest,
                    Error = "ValidationFailed",
                    Message = "One or more validation errors occurred.",
                    Details = details,
                };

                return new ObjectResult(error) { StatusCode = StatusCodes.Status400BadRequest };
            };
        });
}
