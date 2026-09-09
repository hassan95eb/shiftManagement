namespace ShiftFlow.Application.Common;

/// <summary>
/// Raised by login when the username is unknown, the account is inactive, or the
/// password does not match. Mapped to HTTP 401. The message is deliberately the
/// same for every case so it cannot be used to probe which usernames exist.
/// </summary>
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Invalid username or password.")
    {
    }
}
