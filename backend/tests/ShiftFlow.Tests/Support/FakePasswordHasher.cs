using ShiftFlow.Application.Abstractions;

namespace ShiftFlow.Tests.Support;

/// <summary>
/// A deterministic stand-in for BCrypt so the expert-creation tests stay fast
/// and can assert the stored value is a hash, not the raw password.
/// </summary>
public sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hash:";

    public string Hash(string password) => Prefix + password;

    public bool Verify(string password, string passwordHash) => passwordHash == Prefix + password;
}
