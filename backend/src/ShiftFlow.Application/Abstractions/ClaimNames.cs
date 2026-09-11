namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// The custom JWT claim types ShiftFlow issues and reads. Kept in one place so
/// the token issuer (Infrastructure) and the request-scoped reader
/// (<see cref="ICurrentUser"/> in Api) cannot drift apart. Registered claims
/// such as <c>jti</c> keep their standard names and are not repeated here.
/// </summary>
public static class ClaimNames
{
    /// <summary>Subject — the <c>Users.Id</c> of the authenticated account.</summary>
    public const string Sub = "sub";

    /// <summary>The account role, the string form of <c>UserRole</c>.</summary>
    public const string Role = "role";

    /// <summary>Present only for a Supervisor account: the <c>Supervisors.Id</c>.</summary>
    public const string SupervisorId = "supervisorId";

    /// <summary>Present only for a CallAgent account: the <c>CallAgents.Id</c>.</summary>
    public const string CallAgentId = "callAgentId";
}
