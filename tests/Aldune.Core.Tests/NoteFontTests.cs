using System.Text;
using Aldune.Core;
using Xunit;

namespace Aldune.Core.Tests;

public class NoteFontTests
{
    // A minimal sfnt: header, one table record ('name'), and a name table with the given records (platform, nameId, text).
    private static byte[] Font(string tag, params (ushort Platform, ushort NameId, string Text)[] names)
    {
        var strings = new List<byte>();
        var records = new List<byte>();
        foreach (var (platform, nameId, text) in names)
        {
            var bytes = platform == 1 ? Encoding.Latin1.GetBytes(text) : Encoding.BigEndianUnicode.GetBytes(text);
            WriteU16(records, platform); WriteU16(records, platform == 1 ? (ushort)0 : (ushort)1); WriteU16(records, 0);
            WriteU16(records, nameId); WriteU16(records, (ushort)bytes.Length); WriteU16(records, (ushort)strings.Count);
            strings.AddRange(bytes);
        }
        var table = new List<byte>();
        WriteU16(table, 0); WriteU16(table, (ushort)names.Length); WriteU16(table, (ushort)(6 + records.Count));
        table.AddRange(records); table.AddRange(strings);

        var file = new List<byte>();
        file.AddRange(tag == "OTTO" ? Encoding.ASCII.GetBytes("OTTO") : new byte[] { 0, 1, 0, 0 });
        WriteU16(file, 1); WriteU16(file, 16); WriteU16(file, 0); WriteU16(file, 0);
        file.AddRange(Encoding.ASCII.GetBytes("name"));
        file.AddRange(new byte[4]);
        WriteU32(file, 28); WriteU32(file, (uint)table.Count);
        file.AddRange(table);
        return file.ToArray();
    }

    private static void WriteU16(List<byte> list, ushort value) { list.Add((byte)(value >> 8)); list.Add((byte)value); }
    private static void WriteU32(List<byte> list, uint value) { WriteU16(list, (ushort)(value >> 16)); WriteU16(list, (ushort)value); }

    [Fact]
    public void ReadsTheFamilyOfATrueTypeFont()
    {
        var info = FontFileInspector.Inspect(Font("ttf", (3, 1, "Lora")), "lora.ttf");
        Assert.True(info.IsValid);
        Assert.Equal("Lora", info.Family);
    }

    [Fact]
    public void ReadsTheFamilyOfAnOpenTypeFont()
    {
        var info = FontFileInspector.Inspect(Font("OTTO", (3, 1, "Fira Sans")), "x.OTF");
        Assert.True(info.IsValid);
        Assert.Equal("Fira Sans", info.Family);
    }

    [Fact]
    public void PrefersTheTypographicFamilyOverTheLegacyOne()
    {
        var info = FontFileInspector.Inspect(Font("ttf", (3, 1, "Roboto Light"), (3, 16, "Roboto")), "r.ttf");
        Assert.Equal("Roboto", info.Family);
    }

    [Fact]
    public void ReadsAMacintoshRomanNameWhenThereIsNoWindowsOne()
    {
        var info = FontFileInspector.Inspect(Font("ttf", (1, 1, "Old Mac")), "m.ttf");
        Assert.Equal("Old Mac", info.Family);
    }

    [Theory]
    [InlineData("font.exe")]
    [InlineData("font")]
    [InlineData("font.ttf.txt")]
    public void RefusesAWrongExtension(string name)
    {
        var info = FontFileInspector.Inspect(Font("ttf", (3, 1, "X")), name);
        Assert.False(info.IsValid);
        Assert.Equal(FontFileProblem.Extension, info.Problem);
    }

    [Fact]
    public void RefusesBytesThatAreNotAFont()
    {
        var info = FontFileInspector.Inspect(Encoding.ASCII.GetBytes("MZ this is a program, not a font........"), "evil.ttf");
        Assert.False(info.IsValid);
        Assert.Equal(FontFileProblem.NotAFont, info.Problem);
    }

    [Fact]
    public void RefusesAFileLargerThanTenMegabytes()
    {
        var big = new byte[FontFileInspector.MaxBytes + 1];
        var info = FontFileInspector.Inspect(big, "big.ttf");
        Assert.Equal(FontFileProblem.TooLarge, info.Problem);
    }

    [Fact]
    public void RefusesAFontWithoutAReadableName()
    {
        var info = FontFileInspector.Inspect(Font("ttf"), "n.ttf");
        Assert.Equal(FontFileProblem.NoName, info.Problem);
    }

    [Fact]
    public void RefusesTruncatedAndLyingTables()
    {
        var font = Font("ttf", (3, 1, "Lora"));
        Assert.Equal(FontFileProblem.NotAFont, FontFileInspector.Inspect(font.AsSpan(0, 20).ToArray(), "t.ttf").Problem);
        var lying = (byte[])font.Clone();
        lying[20] = 0x7F; // the table offset points far past the end
        Assert.False(FontFileInspector.Inspect(lying, "l.ttf").IsValid);
    }

    [Fact]
    public void ACollectionIsNotAccepted()
    {
        var ttc = Encoding.ASCII.GetBytes("ttcf").Concat(new byte[64]).ToArray();
        Assert.Equal(FontFileProblem.NotAFont, FontFileInspector.Inspect(ttc, "c.ttf").Problem);
    }

    [Theory]
    [InlineData(NoteFontChoice.System, null, "")]
    [InlineData(NoteFontChoice.Serif, null, "Georgia")]
    [InlineData(NoteFontChoice.EasyToRead, null, "Atkinson Hyperlegible")]
    [InlineData(NoteFontChoice.Monospace, null, "Consolas")]
    [InlineData(NoteFontChoice.Installed, "Candara", "Candara")]
    [InlineData(NoteFontChoice.Installed, "", "")]
    [InlineData(NoteFontChoice.Imported, "Lora", "")]   // an imported font is loaded from its file, never by name
    public void ResolvesTheWindowsFamily(NoteFontChoice choice, string? name, string expected) =>
        Assert.Equal(expected, NoteFonts.WindowsFamily(choice, name));

    [Fact]
    public void AnUnknownStoredChoiceLoadsAsSystem() =>
        Assert.Equal(NoteFontChoice.System, NoteFonts.Normalize((NoteFontChoice)99));

    [Fact]
    public void ALookWithItsOwnFontKeepsIt()
    {
        Assert.True(NoteFonts.LookKeepsItsFont("Consolas"));
        Assert.False(NoteFonts.LookKeepsItsFont(""));
        Assert.False(NoteFonts.LookKeepsItsFont(null));
    }

    [Fact]
    public void DefaultsAreSystemAndEmpty()
    {
        var settings = new AppSettings();
        Assert.Equal(NoteFontChoice.System, settings.NoteFontChoice);
        Assert.Null(settings.NoteFontName);
        Assert.Null(settings.NoteFontFile);
    }
}
