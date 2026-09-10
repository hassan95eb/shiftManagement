using ShiftFlow.Api.Contracts;
using ShiftFlow.Application.Common;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Api.Middleware;

/// <summary>
/// Turns every unhandled exception into one <see cref="ApiError"/> JSON body:
/// <list type="bullet">
///   <item><see cref="ValidationException"/> → 400</item>
///   <item><see cref="InvalidCredentialsException"/> → 401</item>
///   <item><see cref="ForbiddenAccessException"/> → 403</item>
///   <item><see cref="NotFoundException"/> → 404</item>
///   <item><see cref="BusinessRuleViolationException"/> → 409</item>
///   <item><see cref="ConcurrencyConflictException"/> → 409</item>
///   <item>anything else → 500, with no detail leaked to the client</item>
/// </list>
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            var error = Map(exception);

            if (error.Status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogInformation(
                    "{Method} {Path} failed: {Error} ({Status})",
                    context.Request.Method,
                    context.Request.Path,
                    error.Error,
                    error.Status);
            }

            if (context.Response.HasStarted)
            {
                // The body is already on the wire; nothing to do but let it surface.
                throw;
            }

            context.Response.StatusCode = error.Status;
            await context.Response.WriteAsJsonAsync(error);
        }
    }

    /// <summary>Pure exception → error mapping, exposed for unit testing.</summary>
    internal static ApiError Map(Exception exception) => exception switch
    {
        ValidationException validation => new ApiError
        {
            Status = StatusCodes.Status400BadRequest,
            Error = "ValidationFailed",
            Message = validation.Message,
            Details = validation.Errors.Count > 0 ? validation.Errors : null,
        },
        InvalidCredentialsException invalidCredentials => new ApiError
        {
            Status = StatusCodes.Status401Unauthorized,
            Error = "InvalidCredentials",
            Message = invalidCredentials.Message,
        },
        ForbiddenAccessException forbidden => new ApiError
        {
            Status = StatusCodes.Status403Forbidden,
            Error = "Forbidden",
            Message = forbidden.Message,
        },
        NotFoundException notFound => new ApiError
        {
            Status = StatusCodes.Status404NotFound,
            Error = "NotFound",
            Message = notFound.Message,
        },
        BusinessRuleViolationException businessRule => new ApiError
        {
            Status = StatusCodes.Status409Conflict,
            Error = "BusinessRuleViolation",
            Message = businessRule.Message,
        },
        ConcurrencyConflictException concurrency => new ApiError
        {
            Status = StatusCodes.Status409Conflict,
            Error = "ConcurrencyConflict",
            Message = concurrency.Message,
        },
        _ => new ApiError
        {
            Status = StatusCodes.Status500InternalServerError,
            Error = "InternalServerError",
            Message = "An unexpected error occurred.",
        },
    };
}
