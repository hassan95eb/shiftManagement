using ShiftFlow.Infrastructure.Identity;

namespace ShiftFlow.Tests.Auth;

/// <summary>
/// Password hashing round-trips: the right password verifies, a wrong password
/// does not, and a stored value that is not a BCrypt hash fails cleanly instead
/// of throwing (CLAUDE.md §5 auth prompt).
/// </summary>
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Verify_accepts_the_original_password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_rejects_a_wrong_password()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("Correct Horse Battery Staple", hash));
    }

    [Fact]
    public void Hash_does_not_contain_the_plain_text()
    {
        var hash = _hasher.Hash("s3cr3t-value");

        Assert.DoesNotContain("s3cr3t-value", hash);
    }

    [Fact]
    public void Hash_is_salted_so_two_hashes_of_one_password_differ()
    {
        Assert.NotEqual(_hasher.Hash("same-input"), _hasher.Hash("same-input"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-bcrypt-hash")]
    [InlineData("$2a$12$too-short")]
    public void Verify_returns_false_for_a_malformed_stored_hash(string storedHash)
    {
        Assert.False(_hasher.Verify("whatever", storedHash));
    }
}
