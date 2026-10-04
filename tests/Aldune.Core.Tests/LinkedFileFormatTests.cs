using System.Text;
using Xunit;

namespace Aldune.Core.Tests;

public class LinkedFileFormatTests
{
    [Theory]
    [InlineData("apuntes.md", true)]
    [InlineData("APUNTES.MD", true)]
    [InlineData("tema.markdown", true)]
    [InlineData("lista.txt", true)]
    [InlineData("foto.png", false)]
    [InlineData("sin-extension", false)]
    public void IsSupportedExtension(string path, bool expected) =>
        Assert.Equal(expected, LinkedFileFormat.IsSupportedExtension(path));

    [Fact]
    public void Utf8WithoutBom_RoundTripsByteForByte()
    {
        var bytes = Encoding.UTF8.GetBytes("# Tema\n- [ ] repasar ñ y tildes\r\n");
        var (content, rejection) = LinkedFileFormat.Decode(bytes);

        Assert.Equal(LinkedFileRejection.None, rejection);
        Assert.Equal(LinkedEncoding.Utf8, content!.Encoding);
        Assert.Equal("# Tema\n- [ ] repasar ñ y tildes\r\n", content.Text);
        Assert.Equal(bytes, LinkedFileFormat.Encode(content.Text, content.Encoding));
    }

    [Fact]
    public void Utf8WithBom_KeepsTheBom()
    {
        byte[] bytes = [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("hola")];
        var (content, _) = LinkedFileFormat.Decode(bytes);

        Assert.Equal(LinkedEncoding.Utf8Bom, content!.Encoding);
        Assert.Equal("hola", content.Text);
        Assert.Equal(bytes, LinkedFileFormat.Encode(content.Text, content.Encoding));
    }

    [Fact]
    public void Utf16LittleAndBigEndian_RoundTrip()
    {
        byte[] le = [0xFF, 0xFE, .. Encoding.Unicode.GetBytes("añadir")];
        byte[] be = [0xFE, 0xFF, .. Encoding.BigEndianUnicode.GetBytes("añadir")];

        var (leContent, _) = LinkedFileFormat.Decode(le);
        var (beContent, _) = LinkedFileFormat.Decode(be);

        Assert.Equal((LinkedEncoding.Utf16LE, "añadir"), (leContent!.Encoding, leContent.Text));
        Assert.Equal((LinkedEncoding.Utf16BE, "añadir"), (beContent!.Encoding, beContent.Text));
        Assert.Equal(le, LinkedFileFormat.Encode(leContent.Text, leContent.Encoding));
        Assert.Equal(be, LinkedFileFormat.Encode(beContent.Text, beContent.Encoding));
    }

    [Fact]
    public void AnsiText_IsRejected()
    {
        // "año" en Windows-1252: 0xF1 suelto no es UTF-8 válido. Guardarlo como UTF-8 estropearía la ñ.
        byte[] ansi = [0x61, 0xF1, 0x6F];
        Assert.Equal(LinkedFileRejection.UnsupportedEncoding, LinkedFileFormat.Decode(ansi).Rejection);
    }

    [Fact]
    public void BinaryWithNulBytes_IsRejected()
    {
        byte[] binary = [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00];
        Assert.Equal(LinkedFileRejection.UnsupportedEncoding, LinkedFileFormat.Decode(binary).Rejection);
    }

    [Fact]
    public void LargerThanTheLimit_IsRejected()
    {
        var bytes = new byte[LinkedFileFormat.MaxBytes + 1];
        Array.Fill(bytes, (byte)'a');
        Assert.Equal(LinkedFileRejection.TooLarge, LinkedFileFormat.Decode(bytes).Rejection);
    }

    [Fact]
    public void EmptyFile_IsUtf8AndEmpty()
    {
        var (content, rejection) = LinkedFileFormat.Decode([]);
        Assert.Equal(LinkedFileRejection.None, rejection);
        Assert.Equal(("", LinkedEncoding.Utf8), (content!.Text, content.Encoding));
    }

    [Fact]
    public void Hash_IsStableAndDistinguishesContent()
    {
        Assert.Equal(LinkedFileFormat.Hash([1, 2, 3]), LinkedFileFormat.Hash([1, 2, 3]));
        Assert.NotEqual(LinkedFileFormat.Hash([1, 2, 3]), LinkedFileFormat.Hash([1, 2, 4]));
        Assert.Equal(LinkedFileFormat.HashText("a\r\nb"), LinkedFileFormat.HashText("a\r\nb"));
        Assert.NotEqual(LinkedFileFormat.HashText("a\r\nb"), LinkedFileFormat.HashText("a\nb"));
    }
}
