using System.Security.Cryptography;
using System.Text;

namespace Aldune.Core;

public enum LinkedEncoding { Utf8, Utf8Bom, Utf16LE, Utf16BE }

public enum LinkedFileRejection { None, TooLarge, UnsupportedEncoding }

public sealed record LinkedFileContent(string Text, LinkedEncoding Encoding);

/// <summary>
/// Cómo se leen y se escriben los bytes de un archivo vinculado (ver la spec de notas vinculadas). Se
/// guarda la codificación con la que llegó para escribirlo igual: un archivo con BOM sigue con BOM. Lo
/// que no sea UTF-8 o UTF-16 con BOM se rechaza en vez de adivinar: guardar como UTF-8 un archivo ANSI
/// estropearía sus tildes, y eso sería perder datos de un archivo que no es de Aldune.
/// </summary>
public static class LinkedFileFormat
{
    /// <summary>El mismo tope que el editor de notas.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    public static IReadOnlyList<string> Extensions { get; } = [".md", ".markdown", ".txt"];

    // throwOnInvalidBytes: un byte que no es UTF-8 tiene que dar error, no un carácter de sustitución.
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding StrictUtf16LE = new(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);
    private static readonly UnicodeEncoding StrictUtf16BE = new(bigEndian: true, byteOrderMark: false, throwOnInvalidBytes: true);

    public static bool IsSupportedExtension(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static (LinkedFileContent? Content, LinkedFileRejection Rejection) Decode(byte[] bytes)
    {
        if (bytes.Length > MaxBytes) return (null, LinkedFileRejection.TooLarge);
        try
        {
            var content = bytes switch
            {
                [0xEF, 0xBB, 0xBF, ..] => new LinkedFileContent(StrictUtf8.GetString(bytes, 3, bytes.Length - 3), LinkedEncoding.Utf8Bom),
                [0xFF, 0xFE, ..] => new LinkedFileContent(StrictUtf16LE.GetString(bytes, 2, bytes.Length - 2), LinkedEncoding.Utf16LE),
                [0xFE, 0xFF, ..] => new LinkedFileContent(StrictUtf16BE.GetString(bytes, 2, bytes.Length - 2), LinkedEncoding.Utf16BE),
                _ => new LinkedFileContent(StrictUtf8.GetString(bytes), LinkedEncoding.Utf8),
            };
            // Un NUL no aparece en texto: es un binario que por casualidad es UTF-8 válido.
            return content.Text.Contains('\0') ? (null, LinkedFileRejection.UnsupportedEncoding) : (content, LinkedFileRejection.None);
        }
        catch (DecoderFallbackException)
        {
            return (null, LinkedFileRejection.UnsupportedEncoding);
        }
    }

    public static byte[] Encode(string text, LinkedEncoding encoding) => encoding switch
    {
        LinkedEncoding.Utf8Bom => [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)],
        LinkedEncoding.Utf16LE => [0xFF, 0xFE, .. Encoding.Unicode.GetBytes(text)],
        LinkedEncoding.Utf16BE => [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes(text)],
        _ => Encoding.UTF8.GetBytes(text),
    };

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));

    public static string HashText(string text) => Hash(Encoding.UTF8.GetBytes(text));
}
