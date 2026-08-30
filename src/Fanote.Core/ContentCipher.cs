using System.Security.Cryptography;
using System.Text;

namespace Fanote.Core;

public readonly record struct EncryptedContent(byte[] CipherText, byte[] Nonce, byte[] Tag);

public sealed class ContentCipher
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes = 16;
    private const int KeySizeBytes = 32;

    private readonly byte[] _key;

    public ContentCipher(byte[] key)
    {
        if (key.Length != KeySizeBytes)
            throw new ArgumentException($"Key must be {KeySizeBytes} bytes (AES-256).", nameof(key));
        _key = key;
    }

    public EncryptedContent Encrypt(string plainText)
    {
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        return new EncryptedContent(cipherBytes, nonce, tag);
    }

    public string Decrypt(EncryptedContent encrypted)
    {
        var plainBytes = new byte[encrypted.CipherText.Length];
        using var aesGcm = new AesGcm(_key, TagSizeBytes);
        aesGcm.Decrypt(encrypted.Nonce, encrypted.CipherText, encrypted.Tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
