using System.Security.Cryptography;
using System.Text;

namespace Aldune.Core;

/// <summary>
/// Cifrado adicional de una nota protegida. La contraseña nunca se guarda: solo viajan la sal y
/// el contenido cifrado, por lo que cada dispositivo debe pedir la contraseña para desbloquearla.
/// </summary>
public sealed class ProtectedNoteContent
{
    public const int CurrentVersion = 1;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Pbkdf2Iterations = 600_000;

    public int Version { get; init; } = CurrentVersion;
    public string Salt { get; init; } = string.Empty;
    public string CipherText { get; init; } = string.Empty;
    public string Nonce { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;

    public static ProtectedNoteContent Protect(string plaintext, string password)
    {
        ValidatePassword(password);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        try { return Encrypt(plaintext, password, salt); }
        finally { CryptographicOperations.ZeroMemory(salt); }
    }

    public bool TryUnprotect(string password, out string plaintext)
    {
        plaintext = string.Empty;
        if (Version != CurrentVersion || string.IsNullOrWhiteSpace(password)) return false;
        try
        {
            var salt = Convert.FromBase64String(Salt);
            var cipherText = Convert.FromBase64String(CipherText);
            var nonce = Convert.FromBase64String(Nonce);
            var tag = Convert.FromBase64String(Tag);
            if (salt.Length != SaltSize || nonce.Length != 12 || tag.Length != 16) return false;
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            try
            {
                var key = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Pbkdf2Iterations,
                    HashAlgorithmName.SHA256, KeySize);
                try
                {
                    using var cipher = new ContentCipher(key);
                    plaintext = cipher.Decrypt(new EncryptedContent(cipherText, nonce, tag));
                    return true;
                }
                finally { CryptographicOperations.ZeroMemory(key); }
            }
            finally { CryptographicOperations.ZeroMemory(passwordBytes); }
        }
        catch (FormatException) { return false; }
        catch (CryptographicException) { return false; }
    }

    public ProtectedNoteContent Reprotect(string plaintext, string password)
    {
        ValidatePassword(password);
        var salt = Convert.FromBase64String(Salt);
        if (salt.Length != SaltSize) throw new FormatException("The protected note salt is invalid.");
        try { return Encrypt(plaintext, password, salt); }
        finally { CryptographicOperations.ZeroMemory(salt); }
    }

    private static ProtectedNoteContent Encrypt(string plaintext, string password, byte[] salt)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            var key = Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, Pbkdf2Iterations,
                HashAlgorithmName.SHA256, KeySize);
            try
            {
                using var cipher = new ContentCipher(key);
                var encrypted = cipher.Encrypt(plaintext);
                return new ProtectedNoteContent
                {
                    Salt = Convert.ToBase64String(salt),
                    CipherText = Convert.ToBase64String(encrypted.CipherText),
                    Nonce = Convert.ToBase64String(encrypted.Nonce),
                    Tag = Convert.ToBase64String(encrypted.Tag)
                };
            }
            finally { CryptographicOperations.ZeroMemory(key); }
        }
        finally { CryptographicOperations.ZeroMemory(passwordBytes); }
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            throw new ArgumentException("A protected note password must contain at least four characters.", nameof(password));
    }
}
