using System.Runtime.Versioning;

namespace ModelTimer.Tests;

[SupportedOSPlatform("windows")]
public class SecretProtectorTests
{
    [Fact]
    public void ProtectThenUnprotect_RoundTrips()
    {
        var protectedValue = SecretProtector.Protect("sk-test-12345");

        Assert.NotEqual("sk-test-12345", protectedValue);
        Assert.Equal("sk-test-12345", SecretProtector.Unprotect(protectedValue));
    }

    [Fact]
    public void Unprotect_OnPreExistingPlaintextKey_PassesItThroughUnchanged()
    {
        // Settings files saved before encryption existed have the raw key on disk - those must
        // keep working rather than losing the user's saved key.
        Assert.Equal("sk-legacy-plaintext-key", SecretProtector.Unprotect("sk-legacy-plaintext-key"));
    }

    [Fact]
    public void Protect_OnEmptyString_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, SecretProtector.Protect(string.Empty));
    }

    [Fact]
    public void Unprotect_OnEmptyString_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, SecretProtector.Unprotect(string.Empty));
    }
}
