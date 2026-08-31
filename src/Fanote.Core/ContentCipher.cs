using System.Security.Cryptography;
using System.Text;

namespace Fanote.Core;

public readonly record struct EncryptedContent(byte[] CipherText, byte[] Nonce, byte[] Tag);

public sealed class ContentCipher : IDisposable
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBytes = 32;

    private readonly byte[] _key;
    private bool _disposed;

    public ContentCipher(byte[] key)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes (AES-256).", nameof(key));
        // Defensive copy to prevent external mutation
        _key = (byte[])key.Clone();
    }

    /// <remarks>
    /// The internal byte-array copies used during encryption are zeroed after use, but the
    /// <paramref name="plainText"/> string itself is not: .NET strings are immutable and cannot
    /// be forcibly cleared. It persists in managed memory until garbage collection reclaims it,
    /// which is not guaranteed to happen promptly and does not itself guarantee the backing
    /// memory is zeroed.
    /// </remarks>
    public EncryptedContent Encrypt(string plainText)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        try
        {
            var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
            var cipherBytes = new byte[plainBytes.Length];
            var tag = new byte[TagSizeBytes];

            using var aesGcm = new AesGcm(_key, TagSizeBytes);
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            return new EncryptedContent(cipherBytes, nonce, tag);
        }
        finally
        {
            // Zero plaintext from memory after use
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    /// <remarks>
    /// The internal byte-array copy used during decryption is zeroed after use, but the
    /// returned string is not: .NET strings are immutable and cannot be forcibly cleared. It
    /// persists in managed memory until garbage collection reclaims it, which is not guaranteed
    /// to happen promptly and does not itself guarantee the backing memory is zeroed.
    /// </remarks>
    public string Decrypt(EncryptedContent encrypted)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var plainBytes = new byte[encrypted.CipherText.Length];
        try
        {
            using var aesGcm = new AesGcm(_key, TagSizeBytes);
            aesGcm.Decrypt(encrypted.Nonce, encrypted.CipherText, encrypted.Tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        finally
        {
            // Zero plaintext from memory after use
            CryptographicOperations.ZeroMemory(plainBytes);
        }
    }

    public void Dispose()
    {
        // Zero the key from memory
        CryptographicOperations.ZeroMemory(_key);
        _disposed = true;
    }
}
