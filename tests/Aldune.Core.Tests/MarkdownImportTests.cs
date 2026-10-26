using System.Text;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class MarkdownImportTests
{
    [Fact]
    public void ToNoteText_HeadingBecomesTitleLine()
    {
        Assert.Equal("Comprar pan", MarkdownImport.ToNoteText("# Comprar pan"));
    }

    [Fact]
    public void ToNoteText_OnlyTheFirstLineLosesItsHash()
    {
        var text = MarkdownImport.ToNoteText("# Titulo\n## Otro\ncuerpo");
        Assert.Equal("Titulo\r\n## Otro\r\ncuerpo", text);
    }

    [Fact]
    public void ToNoteText_TasksAndBulletsBecomeGlyphs()
    {
        var text = MarkdownImport.ToNoteText("# Lista\n\n- [ ] leche\n- [x] pan\n- nota");
        Assert.Equal($"Lista\r\n\r\n{TaskLines.Unchecked} leche\r\n{TaskLines.Checked} pan\r\n{BulletLines.Prefix}nota", text);
    }

    [Fact]
    public void ToNoteText_NewLinesBecomeCrLf()
    {
        Assert.Equal("a\r\nb\r\nc", MarkdownImport.ToNoteText("a\nb\r\nc"));
    }

    [Fact]
    public void ToNoteText_RoundTripsWithExport()
    {
        var note = $"Compra\r\n{TaskLines.Unchecked} leche\r\n{TaskLines.Checked} pan";
        var back = MarkdownImport.ToNoteText(MarkdownExport.ToMarkdown(note));
        // La exportación deja una línea en blanco tras el título; el resto vuelve igual.
        Assert.Equal($"Compra\r\n\r\n{TaskLines.Unchecked} leche\r\n{TaskLines.Checked} pan", back);
    }

    [Fact]
    public void Read_DecodesBomAndRejectsBinaries()
    {
        var withBom = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes("# Hola")).ToArray();
        Assert.Equal(("Hola", LinkedFileRejection.None), MarkdownImport.Read(withBom));

        var (text, rejection) = MarkdownImport.Read([0x61, 0x00, 0x62]);
        Assert.Null(text);
        Assert.Equal(LinkedFileRejection.UnsupportedEncoding, rejection);
    }

    [Fact]
    public void Read_RejectsFilesOverTheLimit()
    {
        var (text, rejection) = MarkdownImport.Read(new byte[LinkedFileFormat.MaxBytes + 1]);
        Assert.Null(text);
        Assert.Equal(LinkedFileRejection.TooLarge, rejection);
    }
}
