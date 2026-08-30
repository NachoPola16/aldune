using System.Security.Cryptography;
using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class ContentCipherTests
{
    private static readonly byte[] TestKey = new byte[32]; // all-zero key, fine for a fixed test fixture

    [Fact]
    public void EncryptThenDecrypt_RoundTrips()
    {
        using var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("hola, esto es una nota");
        var decrypted = sut.Decrypt(encrypted);
        Assert.Equal("hola, esto es una nota", decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCipherTextForSamePlainText()
    {
        using var sut = new ContentCipher(TestKey);
        var first = sut.Encrypt("misma nota");
        var second = sut.Encrypt("misma nota");
        Assert.NotEqual(Convert.ToBase64String(first.CipherText), Convert.ToBase64String(second.CipherText));
        Assert.NotEqual(Convert.ToBase64String(first.Nonce), Convert.ToBase64String(second.Nonce));
    }

    [Fact]
    public void Decrypt_WithTamperedCipherText_Throws()
    {
        using var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("texto original");
        encrypted.CipherText[0] ^= 0xFF;

        Assert.Throws<AuthenticationTagMismatchException>(() => sut.Decrypt(encrypted));
    }

    [Fact]
    public void Constructor_WithWrongKeyLength_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ContentCipher(new byte[16]));
    }

    [Fact]
    public void EncryptThenDecrypt_HandlesEmptyString()
    {
        using var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("");
        Assert.Equal("", sut.Decrypt(encrypted));
    }

    [Fact]
    public void Encrypt_AfterDispose_Throws()
    {
        var sut = new ContentCipher(TestKey);
        sut.Dispose();

        Assert.Throws<ObjectDisposedException>(() => sut.Encrypt("texto"));
    }

    [Fact]
    public void Decrypt_AfterDispose_Throws()
    {
        var sut = new ContentCipher(TestKey);
        var encrypted = sut.Encrypt("texto");
        sut.Dispose();

        Assert.Throws<ObjectDisposedException>(() => sut.Decrypt(encrypted));
    }
}
