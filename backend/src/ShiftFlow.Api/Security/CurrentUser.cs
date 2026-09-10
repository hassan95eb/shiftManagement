using System.Globalization;
using System.Security.Claims;
using ShiftFlow.Application.Abstractions;
using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Api.Security;

/// <summary>
/// <see cref="ICurrentUser"/> read from <c>HttpContext.User</c>. The JWT bearer
/// handler is configured with <c>MapInboundClaims = false</c>, so the claim
/// types are exactly the ones the token was issued with
/// (<see cref="ClaimNames"/>).
/// </summary>
/// <remarks>
/// Fails loudly by design. When the request carries no valid bearer token, or
/// the principal is missing / has a malformed <c>sub</c> or <c>role</c> claim,
/// reading <see cref="UserId"/> or <see cref="Role"/> throws
/// <see cref="InvalidOperationException"/> rather than returning <c>0</c>,
/// <c>null</c> or a default role. Anonymous endpoints must therefore never read
/// those members.
/// </remarks>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int UserId
    {
        get
        {
            var principal = AuthenticatedPrincipal();
            var raw = principal.FindFirst(ClaimNames.Sub)?.Value;

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var userId)
                ? userId
                : throw new InvalidOperationException(
                    $"The authenticated principal has no valid '{ClaimNames.Sub}' claim.");
        }
    }

    public UserRole Role
    {
        get
        {
            var principal = AuthenticatedPrincipal();
            var raw = principal.FindFirst(ClaimNames.Role)?.Value;

            return Enum.TryParse<UserRole>(raw, ignoreCase: false, out var role) && Enum.IsDefined(role)
                ? role
                : throw new InvalidOperationException(
                    $"The authenticated principal has no valid '{ClaimNames.Role}' claim.");
        }
    }

    public int? EmployerId => ReadOptionalInt(ClaimNames.EmployerId);

    public int? ExpertId => ReadOptionalInt(ClaimNames.ExpertId);

    public int RequireEmployerId() =>
        EmployerId ?? throw new InvalidOperationException(
            $"The authenticated principal has no '{ClaimNames.EmployerId}' claim; "
            + "an Employer-scoped operation was reached without one.");

    public int RequireExpertId() =>
        ExpertId ?? throw new InvalidOperationException(
            $"The authenticated principal has no '{ClaimNames.ExpertId}' claim; "
            + "an Expert-scoped operation was reached without one.");

    private ClaimsPrincipal AuthenticatedPrincipal()
    {
        if (Principal is { Identity.IsAuthenticated: true } principal)
        {
            return principal;
        }

        throw new InvalidOperationException(
            "The current request is not authenticated; ICurrentUser.UserId / Role must not be read on an anonymous path.");
    }

    private int? ReadOptionalInt(string claimType) =>
        IsAuthenticated
        && Principal!.FindFirst(claimType)?.Value is { } raw
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
