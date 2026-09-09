namespace ShiftFlow.Application.Abstractions;

/// <summary>
/// Hashes and verifies user passwords. The only implementation is a BCrypt one
/// in Infrastructure; the Application layer never sees a plain-text password
/// beyond the boundary of a single use-case call (docs/02-repository-structure.md §3).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a self-describing hash (algorithm, cost and salt embedded).</summary>
    string Hash(string password);

    /// <summary>
    /// Returns <c>true</c> only when <paramref name="password"/> matches
    /// <paramref name="passwordHash"/>. A malformed stored hash verifies as
    /// <c>false</c> rather than throwing.
    /// </summary>
    bool Verify(string password, string passwordHash);
}
