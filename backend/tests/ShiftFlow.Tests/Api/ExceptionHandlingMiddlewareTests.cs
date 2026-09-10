using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftFlow.Api.Middleware;
using ShiftFlow.Application.Common;
using ShiftFlow.Domain.Exceptions;

namespace ShiftFlow.Tests.Api;

/// <summary>
/// The error middleware must translate every exception type to its status code
/// and always emit the same JSON shape, without leaking internals on a 500
/// (CLAUDE.md §5 auth prompt).
/// </summary>
public class ExceptionHandlingMiddlewareTests
{
    private static async Task<(int Status, JsonElement Body)> Run(Exception thrown)
    {
        var context = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        var middleware = new ExceptionHandlingMiddleware(
            _ => throw thrown,
            NullLogger<ExceptionHandlingMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        responseBody.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(responseBody);
        return (context.Response.StatusCode, body);
    }

    public static TheoryData<Exception, int, string> Cases() => new()
    {
        { new ValidationException("bad input"), 400, "ValidationFailed" },
        { new InvalidCredentialsException(), 401, "InvalidCredentials" },
        { new ForbiddenAccessException(), 403, "Forbidden" },
        { new NotFoundException("no such project"), 404, "NotFound" },
        { new BusinessRuleViolationException("rule broken"), 409, "BusinessRuleViolation" },
        { new ConcurrencyConflictException("reload and retry"), 409, "ConcurrencyConflict" },
        { new InvalidOperationException("boom"), 500, "InternalServerError" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Maps_each_exception_to_status_and_uniform_shape(
        Exception thrown, int expectedStatus, string expectedError)
    {
        var (status, body) = await Run(thrown);

        Assert.Equal(expectedStatus, status);
        Assert.Equal(expectedStatus, body.GetProperty("status").GetInt32());
        Assert.Equal(expectedError, body.GetProperty("error").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
    }

    [Fact]
    public async Task Does_not_leak_internal_detail_on_500()
    {
        var (_, body) = await Run(new InvalidOperationException("connection string with a secret in it"));

        Assert.Equal("An unexpected error occurred.", body.GetProperty("message").GetString());
        Assert.DoesNotContain("secret", body.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Field_validation_errors_are_returned_as_details()
    {
        var errors = new Dictionary<string, string[]> { ["Username"] = ["The Username field is required."] };

        var (_, body) = await Run(new ValidationException(errors));

        Assert.Equal(
            "The Username field is required.",
            body.GetProperty("details").GetProperty("Username")[0].GetString());
    }

    [Fact]
    public async Task Non_validation_errors_have_no_details_property()
    {
        var (_, body) = await Run(new BusinessRuleViolationException("rule broken"));

        Assert.False(body.TryGetProperty("details", out _));
    }
}
