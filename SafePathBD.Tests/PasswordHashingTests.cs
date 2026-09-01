using Microsoft.AspNetCore.Identity;
using SafePathBD.Web.Models.Entities;

namespace SafePathBD.Tests;

public class PasswordHashingTests
{
    private readonly PasswordHasher<Users> _hasher = new();

    [Fact]
    public void HashPassword_DoesNotReturnThePlainTextPassword()
    {
        var user = new Users { Email = "hash@example.com" };

        var hash = _hasher.HashPassword(user, "Str0ng!Passphrase");

        Assert.NotEqual("Str0ng!Passphrase", hash);
        Assert.DoesNotContain("Str0ng!Passphrase", hash, StringComparison.Ordinal);
    }

    [Fact]
    public void HashPassword_ProducesADifferentHashEachTime()
    {
        var user = new Users { Email = "salt@example.com" };

        Assert.NotEqual(
            _hasher.HashPassword(user, "Str0ng!Passphrase"),
            _hasher.HashPassword(user, "Str0ng!Passphrase"));
    }

    [Fact]
    public void HashPassword_FitsTheDatabaseColumnLength()
    {
        var user = new Users { Email = "length@example.com" };

        // users.password_hash is VARCHAR(255).
        Assert.True(_hasher.HashPassword(user, "Str0ng!Passphrase").Length <= 255);
    }

    [Fact]
    public void VerifyHashedPassword_SucceedsForTheCorrectPassword()
    {
        var user = new Users { Email = "verify@example.com" };
        var hash = _hasher.HashPassword(user, "Str0ng!Passphrase");

        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(user, hash, "Str0ng!Passphrase"));
    }

    [Fact]
    public void VerifyHashedPassword_FailsForTheWrongPassword()
    {
        var user = new Users { Email = "verify@example.com" };
        var hash = _hasher.HashPassword(user, "Str0ng!Passphrase");

        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(user, hash, "wrong-password"));
    }
}
