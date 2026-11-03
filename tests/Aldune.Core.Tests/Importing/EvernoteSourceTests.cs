using System.Text;
using Aldune.Core.Importing;

namespace Aldune.Core.Tests.Importing;

public sealed class EvernoteSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-enex-{Guid.NewGuid():N}");

    public EvernoteSourceTests() => Directory.CreateDirectory(_root);

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private string Write(string xml)
    {
        var p = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".enex");
        File.WriteAllText(p, xml, new UTF8Encoding(false));
        return p;
    }

    private static string Note(string title, string enml, string extra = "") =>
        $"<note><title>{title}</title><content><![CDATA[<?xml version=\"1.0\" encoding=\"UTF-8\"?><!DOCTYPE en-note SYSTEM \"http://xml.evernote.com/pub/enml2.dtd\"><en-note>{enml}</en-note>]]></content>{extra}</note>";

    private static string Export(params string[] notes) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><en-export export-date=\"20240101T000000Z\" application=\"Evernote\">" + string.Concat(notes) + "</en-export>";

    [Fact]
    public void Note_ReadsTitleBodyTagsAndDates()
    {
        var path = Write(Export(Note("Trip", "<div>pack</div><div><en-todo checked=\"true\"/>passport</div><ul><li>a</li></ul>",
            "<created>20240102T030405Z</created><updated>20240103T030405Z</updated><tag>travel</tag><tag>2024</tag>")));

        var n = Assert.Single(new EvernoteSource().Read(path).Notes);

        Assert.Equal("Trip\r\npack\r\n☒ passport\r\n→ a", n.Text);
        Assert.Equal(new[] { "travel", "2024" }, n.Tags);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), n.CreatedAt);
        Assert.Equal(new DateTimeOffset(2024, 1, 3, 3, 4, 5, TimeSpan.Zero), n.UpdatedAt);
        Assert.Equal(NoteState.Active, n.State);
    }

    [Fact]
    public void Resources_AreCounted_NotImported()
    {
        var path = Write(Export(Note("Pic", "<div>x</div><en-media hash=\"1\" type=\"image/png\"/>",
            "<resource><data encoding=\"base64\">AAAA</data><mime>image/png</mime></resource><resource><data encoding=\"base64\">BBBB</data></resource>")));

        var preview = new EvernoteSource().Read(path);

        Assert.Single(preview.Notes);
        Assert.Equal(2, preview.AttachmentsIgnored);
        Assert.Equal(1, preview.NotesWithAttachments);
    }

    [Fact]
    public void EmptyNotes_AreSkipped()
    {
        var preview = new EvernoteSource().Read(Write(Export(Note("", "<div><br/></div>"), Note("Keep", "<div>x</div>"))));

        Assert.Single(preview.Notes);
        Assert.Equal(ImportSkipReason.Empty, Assert.Single(preview.Skipped).Reason);
    }

    [Fact]
    public void ADtd_IsNeverResolved_SoNoLocalFileLeaks()
    {
        var secret = Path.Combine(_root, "secret.txt");
        File.WriteAllText(secret, "TOP-SECRET-VALUE");
        var xml = $"<?xml version=\"1.0\"?><!DOCTYPE en-export [<!ENTITY x SYSTEM \"{new Uri(secret).AbsoluteUri}\">]><en-export><note><title>&x;</title><content><![CDATA[<en-note>&x;</en-note>]]></content></note></en-export>";

        var preview = new EvernoteSource().Read(Write(xml));

        Assert.Empty(preview.Notes);
        Assert.NotEqual(ImportFailure.None, preview.Failure);
    }

    [Fact]
    public void NotAnEnex_IsAWrongFormat()
    {
        Assert.Equal(ImportFailure.WrongFormat, new EvernoteSource().Read(Write("<html><body>hi</body></html>")).Failure);
        Assert.Equal(ImportFailure.WrongFormat, new EvernoteSource().Read(Write("plain text, not xml")).Failure);
    }

    [Fact]
    public void MoreNotesThanTheLimit_AreLeftOut_WithTheirReason()
    {
        var notes = Enumerable.Range(0, ImportLimits.MaxNotes + 3).Select(i => Note("n" + i, "<div>t</div>")).ToArray();

        var preview = new EvernoteSource().Read(Write(Export(notes)));

        Assert.Equal(ImportLimits.MaxNotes, preview.Notes.Count);
        Assert.Equal(3, preview.Skipped.Count(s => s.Reason == ImportSkipReason.LimitReached));
    }

    [Fact]
    public void MissingFile_IsReported() =>
        Assert.Equal(ImportFailure.NotFound, new EvernoteSource().Read(Path.Combine(_root, "none.enex")).Failure);
}
