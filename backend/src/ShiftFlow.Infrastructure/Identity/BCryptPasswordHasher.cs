using ShiftFlow.Application.Abstractions;

namespace ShiftFlow.Infrastructure.Identity;

/// <summary>
/// <see cref="IPasswordHasher"/> backed by BCrypt. The cost, salt and algorithm
/// marker live inside the returned string, so <see cref="Verify"/> needs nothing
/// but the hash itself. Seed data (CLAUDE.md §10, phase 15) is produced with the
/// same work factor.
/// </summary>
public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public bool Verify(string password, string passwordHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, passwordHash);
        }
        catch (Exception ex) when (ex is BCrypt.Net.SaltParseException or ArgumentException or FormatException)
        {
            // A stored value that is not a well-formed BCrypt hash (corrupt or
            // hand-edited seed row) is a failed verification, not a server error.
            return false;
        }
    }
}
