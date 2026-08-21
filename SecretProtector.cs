using System;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;

namespace ModelTimer;

/// <summary>
/// Encrypts secrets (the AI API key) at rest using Windows DPAPI, scoped to the current
/// Windows user account. The app is Windows-only, so this costs nothing extra to depend on.
/// Anything that fails to decrypt is treated as a pre-existing plaintext value (from before
/// this protection existed) and passed through as-is — it gets encrypted automatically on the
/// next save.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class SecretProtector
{
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return string.Empty;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(plaintext);
            var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(protectedBytes);
        }
        catch (Exception ex)
        {
            JsonStore.LogError("Failed to encrypt secret", ex);
            return plaintext;
        }
    }

    public static string Unprotect(string stored)
    {
        if (string.IsNullOrEmpty(stored)) return string.Empty;

        try
        {
            var bytes = Convert.FromBase64String(stored);
            var plainBytes = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // Not DPAPI-protected data - either a legacy plaintext key, or it belongs to a
            // different Windows user. Either way, hand back what's on disk rather than lose it.
            return stored;
        }
    }
}
