using System.Security.Cryptography;

namespace Fanote.Core;

public static class DatabaseKeyProvider
{
    private const int KeySizeBytes = 32;

    public static byte[] GenerateKey() => RandomNumberGenerator.GetBytes(KeySizeBytes);

    public static byte[] Wrap(byte[] rawKey) =>
        ProtectedData.Protect(rawKey, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public static byte[] Unwrap(byte[] wrappedKey) =>
        ProtectedData.Unprotect(wrappedKey, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
