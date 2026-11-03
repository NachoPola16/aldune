using System.IO.Compression;
using System.Text;
using Aldune.Core.Importing;

namespace Aldune.Core.Tests.Importing;

public sealed class KeepTakeoutSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-keep-{Guid.NewGuid():N}");

    public KeepTakeoutSourceTests() => Directory.CreateDirectory(_root);

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private string Folder() { var f = Path.Combine(_root, "Keep"); Directory.CreateDirectory(f); return f; }

    private static void Put(string folder, string name, string json) => File.WriteAllText(Path.Combine(folder, name), json, new UTF8Encoding(false));

    [Fact]
    public void TextNote_BecomesTitleThenBody_WithDatesLabelsAndColour()
    {
        var f = Folder();
        Put(f, "a.json", """
            {"color":"YELLOW","isTrashed":false,"isPinned":true,"isArchived":false,"textContent":"line1\nline2","title":"Shopping",
             "userEditedTimestampUsec":1700000100000000,"createdTimestampUsec":1700000000000000,"labels":[{"name":"home"},{"name":"food"}]}
            """);

        var preview = new KeepTakeoutSource().Read(f);

        var n = Assert.Single(preview.Notes);
        Assert.Equal("Shopping\r\nline1\r\nline2", n.Text);
        Assert.Equal(ImportHue.Yellow, n.Hue);
        Assert.Equal(new[] { "home", "food" }, n.Tags);
        Assert.True(n.Pinned);
        Assert.Equal(NoteState.Active, n.State);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), n.CreatedAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000100), n.UpdatedAt);
    }

    [Fact]
    public void Checklist_BecomesTaskLines()
    {
        var f = Folder();
        Put(f, "a.json", """{"title":"","listContent":[{"text":"milk","isChecked":false},{"text":"eggs","isChecked":true}],"color":"DEFAULT"}""");

        var n = Assert.Single(new KeepTakeoutSource().Read(f).Notes);

        Assert.Equal("☐ milk\r\n☒ eggs", n.Text);
        Assert.Null(n.Hue);
        Assert.All(n.Text.Split("\r\n"), l => Assert.True(TaskLines.IsTaskLine(l)));
    }

    [Fact]
    public void TrashAndArchive_MapToStates_AndTrashWins()
    {
        var f = Folder();
        Put(f, "t.json", """{"title":"t","textContent":"x","isTrashed":true,"isArchived":true}""");
        Put(f, "r.json", """{"title":"r","textContent":"x","isArchived":true}""");

        var states = new KeepTakeoutSource().Read(f).Notes.ToDictionary(n => n.Text.Split("\r\n")[0], n => n.State);

        Assert.Equal(NoteState.Trashed, states["t"]);
        Assert.Equal(NoteState.Archived, states["r"]);
    }

    [Fact]
    public void EmptyNotes_AreSkipped_AndAttachmentsCounted()
    {
        var f = Folder();
        Put(f, "e.json", """{"title":"","textContent":""}""");
        Put(f, "p.json", """{"title":"photo","textContent":"see","attachments":[{"filePath":"a.jpg","mimetype":"image/jpeg"},{"filePath":"b.3gp","mimetype":"audio/3gpp"}]}""");

        var preview = new KeepTakeoutSource().Read(f);

        Assert.Single(preview.Notes);
        Assert.Equal(ImportSkipReason.Empty, Assert.Single(preview.Skipped).Reason);
        Assert.Equal(2, preview.AttachmentsIgnored);
        Assert.Equal(1, preview.NotesWithAttachments);
    }

    [Fact]
    public void JsonThatIsNotANote_IsIgnored_AndAFolderWithoutNotesIsReported()
    {
        var f = Folder();
        Put(f, "other.json", """{"hello":"world"}""");
        Put(f, "broken.json", "{not json");

        var preview = new KeepTakeoutSource().Read(f);

        Assert.Empty(preview.Notes);
        Assert.Equal(ImportFailure.NothingFound, preview.Failure);
    }

    [Fact]
    public void ZipFromTakeout_IsReadWithoutExtractingToDisk()
    {
        var zip = Path.Combine(_root, "takeout.zip");
        using (var z = ZipFile.Open(zip, ZipArchiveMode.Create))
        {
            var e = z.CreateEntry("Takeout/Keep/a.json");
            using (var s = new StreamWriter(e.Open(), new UTF8Encoding(false)))
                s.Write("""{"title":"zipped","textContent":"body"}""");
            z.CreateEntry("Takeout/Keep/pic.jpg");
        }

        var n = Assert.Single(new KeepTakeoutSource().Read(zip).Notes);

        Assert.Equal("zipped\r\nbody", n.Text);
    }

    [Fact]
    public void HtmlWithoutAJsonTwin_IsUsedAsAFallback()
    {
        var f = Folder();
        Put(f, "twin.json", """{"title":"twin","textContent":"json"}""");
        Put(f, "twin.html", "<html><head><title>twin</title></head><body><div class=\"title\">twin</div><div class=\"content\">html</div></body></html>");
        Put(f, "solo.html", "<html><head><title>solo</title></head><body><div class=\"title\">solo</div><div class=\"content\">only html</div></body></html>");

        var texts = new KeepTakeoutSource().Read(f).Notes.Select(n => n.Text).OrderBy(t => t).ToList();

        Assert.Equal(new[] { "solo\r\nonly html", "twin\r\njson" }, texts);
    }

    [Fact]
    public void ANoteOverTheSizeLimit_IsSkipped_NotLoaded()
    {
        var f = Folder();
        Put(f, "big.json", "{\"title\":\"big\",\"textContent\":\"" + new string('x', ImportLimits.MaxNoteChars + 10) + "\"}");
        Put(f, "ok.json", """{"title":"ok","textContent":"y"}""");

        var preview = new KeepTakeoutSource().Read(f);

        Assert.Single(preview.Notes);
        Assert.Contains(preview.Skipped, s => s.Reason == ImportSkipReason.TooLarge);
    }

    [Fact]
    public void MissingPath_IsReported()
    {
        Assert.Equal(ImportFailure.NotFound, new KeepTakeoutSource().Read(Path.Combine(_root, "nope")).Failure);
    }
}
