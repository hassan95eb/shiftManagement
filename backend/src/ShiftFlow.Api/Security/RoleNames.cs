using ShiftFlow.Domain.Enums;

namespace ShiftFlow.Api.Security;

/// <summary>
/// Role names as string constants for <c>[Authorize(Roles = ...)]</c>. Kept in
/// sync with <see cref="UserRole"/> by <c>nameof</c> — the JWT carries the role
/// as the enum's string form and the bearer handler maps it to the role claim.
/// </summary>
public static class RoleNames
{
    public const string Supervisor = nameof(UserRole.Supervisor);

    public const string CallAgent = nameof(UserRole.CallAgent);
}
