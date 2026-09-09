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
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public int UserId =>
        ReadInt(ClaimNames.Sub)
        ?? throw new InvalidOperationException("The current request is not authenticated.");

    public UserRole Role =>
        Principal?.FindFirst(ClaimNames.Role)?.Value is { } raw && Enum.TryParse<UserRole>(raw, out var role)
            ? role
            : throw new InvalidOperationException("The current request has no valid role claim.");

    public int? EmployerId => ReadInt(ClaimNames.EmployerId);

    public int? ExpertId => ReadInt(ClaimNames.ExpertId);

    private int? ReadInt(string claimType) =>
        Principal?.FindFirst(claimType)?.Value is { } raw
        && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
