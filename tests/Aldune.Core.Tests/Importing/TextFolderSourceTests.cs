using System.Text;
using Aldune.Core.Importing;

namespace Aldune.Core.Tests.Importing;

public sealed class TextFolderSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-md-{Guid.NewGuid():N}");

    public TextFolderSourceTests() => Directory.CreateDirectory(_root);

    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private void Put(string name, string content)
    {
        var p = Path.Combine(_root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(p)!);
        File.WriteAllText(p, content, new UTF8Encoding(false));
    }

    [Fact]
    public void FileName_IsTheTitle_WhenThereIsNoHeading()
    {
        Put("Shopping list.md", "- [ ] milk\n- [x] eggs");

        var n = Assert.Single(new TextFolderSource().Read(_root).Notes);

        Assert.Equal("Shopping list\r\n☐ milk\r\n☒ eggs", n.Text);
    }

    [Fact]
    public void Heading_IsTheTitle_AndTheNameIsNotAdded()
    {
        Put("file.md", "# Real title\n\nbody");

        var n = Assert.Single(new TextFolderSource().Read(_root).Notes);

        Assert.Equal("Real title\r\n\r\nbody", n.Text);
    }

    [Fact]
    public void TxtFiles_AndSubfolders_AreRead_OtherExtensionsIgnored()
    {
        Put("a.txt", "plain");
        Put("sub/b.markdown", "deep");
        Put("c.png", "not a note");

        var texts = new TextFolderSource().Read(_root).Notes.Select(n => n.Text).OrderBy(t => t).ToList();

        Assert.Equal(new[] { "a\r\nplain", "b\r\ndeep" }, texts);
    }

    [Fact]
    public void BinaryContent_IsRejected_NotImportedAsGarbage()
    {
        File.WriteAllBytes(Path.Combine(_root, "bin.txt"), [0x00, 0x01, 0x02, 0xFF, 0xFE, 0x00]);
        File.WriteAllBytes(Path.Combine(_root, "latin.txt"), [0x63, 0x61, 0x66, 0xE9]);
        Put("ok.md", "fine");

        var preview = new TextFolderSource().Read(_root);

        Assert.Single(preview.Notes);
        Assert.Equal(2, preview.Skipped.Count(s => s.Reason == ImportSkipReason.NotText));
    }

    [Fact]
    public void EmptyFiles_AreSkipped_AndBigOnesToo()
    {
        Put("empty.md", "   \n");
        File.WriteAllText(Path.Combine(_root, "big.txt"), new string('x', LinkedFileFormat.MaxBytes + 1));
        Put("ok.md", "x");

        var preview = new TextFolderSource().Read(_root);

        Assert.Single(preview.Notes);
        Assert.Contains(preview.Skipped, s => s.Reason == ImportSkipReason.Empty);
        Assert.Contains(preview.Skipped, s => s.Reason == ImportSkipReason.TooLarge);
    }

    [Fact]
    public void DatesComeFromTheFile_AndASingleFilePathWorks()
    {
        Put("d.md", "x");
        var file = Path.Combine(_root, "d.md");
        var when = new DateTime(2023, 5, 6, 7, 8, 9, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, when);

        var n = Assert.Single(new TextFolderSource().Read(file).Notes);

        Assert.Equal(new DateTimeOffset(when), n.UpdatedAt);
    }

    [Fact]
    public void FolderWithoutNotes_AndMissingPath_AreReported()
    {
        Assert.Equal(ImportFailure.NothingFound, new TextFolderSource().Read(_root).Failure);
        Assert.Equal(ImportFailure.NotFound, new TextFolderSource().Read(Path.Combine(_root, "zzz")).Failure);
    }
}
