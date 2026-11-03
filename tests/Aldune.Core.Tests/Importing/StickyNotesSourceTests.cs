using Aldune.Core.Importing;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests.Importing;

/// <summary>Synthetic plum.sqlite files: the schema is the one Microsoft Sticky Notes 3.x is known to use (Note, Media and Stroke tables).</summary>
public sealed class StickyNotesSourceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sticky-{Guid.NewGuid():N}");

    public StickyNotesSourceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch { }
    }

    private string CreateDb(Action<SqliteConnection> fill, bool withExtraTables = true)
    {
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + ".sqlite");
        using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()))
        {
            c.Open();
            Exec(c, "CREATE TABLE Note (Text varchar, WindowPosition varchar, IsOpen integer, IsAlwaysOnTop integer, Theme varchar, Type varchar, " +
                    "Id varchar primary key, ParentId varchar, CreatedAt integer, DeletedAt integer, UpdatedAt integer);");
            if (withExtraTables)
            {
                Exec(c, "CREATE TABLE Media (Id varchar primary key, ParentId varchar, Data blob);");
                Exec(c, "CREATE TABLE Stroke (Id varchar primary key, ParentId varchar, Data blob);");
            }
            fill(c);
        }
        return path;
    }

    private static void Exec(SqliteConnection c, string sql, params (string, object?)[] args)
    {
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static void AddNote(SqliteConnection c, string id, string? text, string? theme = "Yellow", long? created = null, long? updated = null, long? deleted = null) =>
        Exec(c, "INSERT INTO Note (Text, Theme, Type, Id, CreatedAt, DeletedAt, UpdatedAt) VALUES ($t, $th, 'Note', $id, $c, $d, $u);",
            ("$t", text), ("$th", theme), ("$id", id), ("$c", created), ("$d", deleted), ("$u", updated));

    [Fact]
    public void Text_LosesTheFormattingPrefixes_AndKeepsTheLines()
    {
        var created = new DateTime(2024, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        var updated = created.AddHours(2);
        var path = CreateDb(c => AddNote(c, "n1", "\\id=11111111-1111-1111-1111-111111111111 Title\r\\id=22222222-2222-2222-2222-222222222222 second line",
            "Blue", created.Ticks, updated.Ticks));

        var preview = new StickyNotesSource().Read(path);

        var n = Assert.Single(preview.Notes);
        Assert.Equal("Title\r\nsecond line", n.Text);
        Assert.Equal(ImportHue.Blue, n.Hue);
        Assert.Equal(new DateTimeOffset(created), n.CreatedAt);
        Assert.Equal(new DateTimeOffset(updated), n.UpdatedAt);
        Assert.Equal(NoteState.Active, n.State);
    }

    [Fact]
    public void DeletedNotes_GoToTheTrash_AndEmptyOnesAreSkipped()
    {
        var path = CreateDb(c =>
        {
            AddNote(c, "a", "\\id=aaaa gone", deleted: DateTime.UtcNow.Ticks);
            AddNote(c, "b", "\\id=bbbb ", "Pink");
            AddNote(c, "c", null, "Green");
        });

        var preview = new StickyNotesSource().Read(path);

        Assert.Equal(NoteState.Trashed, Assert.Single(preview.Notes).State);
        Assert.Equal(2, preview.Skipped.Count(s => s.Reason == ImportSkipReason.Empty));
    }

    [Fact]
    public void PlainTextWithoutPrefixes_AndBackslashesInsideTheText_AreKept()
    {
        var path = CreateDb(c => AddNote(c, "a", "path C:\\Users\\x\nnext"));

        Assert.Equal("path C:\\Users\\x\r\nnext", Assert.Single(new StickyNotesSource().Read(path).Notes).Text);
    }

    [Fact]
    public void ImagesAndInk_AreCountedAsAttachments()
    {
        var path = CreateDb(c =>
        {
            AddNote(c, "a", "\\id=x hello");
            Exec(c, "INSERT INTO Media (Id, ParentId, Data) VALUES ('m1','a',x'00');");
            Exec(c, "INSERT INTO Stroke (Id, ParentId, Data) VALUES ('s1','a',x'00');");
        });

        var preview = new StickyNotesSource().Read(path);

        Assert.Equal(2, preview.AttachmentsIgnored);
        Assert.Equal(1, preview.NotesWithAttachments);
    }

    [Fact]
    public void ADatabaseWithoutTheExtraTables_StillWorks()
    {
        var path = CreateDb(c => AddNote(c, "a", "\\id=x hi"), withExtraTables: false);

        Assert.Single(new StickyNotesSource().Read(path).Notes);
    }

    [Fact]
    public void TheOriginalFile_IsNeverModified_AndNoTempCopyIsLeft()
    {
        var path = CreateDb(c => AddNote(c, "a", "\\id=x hi"));
        var before = File.ReadAllBytes(path);
        var tempBefore = Directory.GetDirectories(Path.GetTempPath(), "aldune-import-*").Length;

        new StickyNotesSource().Read(path);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(tempBefore, Directory.GetDirectories(Path.GetTempPath(), "aldune-import-*").Length);
    }

    [Fact]
    public void AFolder_IsSearchedForPlumSqlite()
    {
        var path = CreateDb(c => AddNote(c, "a", "\\id=x hi"));
        var folder = Path.Combine(_root, "LocalState");
        Directory.CreateDirectory(folder);
        File.Copy(path, Path.Combine(folder, "plum.sqlite"));

        Assert.Single(new StickyNotesSource().Read(folder).Notes);
    }

    [Fact]
    public void NotADatabase_AndAnotherDatabase_AreAWrongFormat()
    {
        var junk = Path.Combine(_root, "junk.sqlite");
        File.WriteAllText(junk, "this is not sqlite at all");
        var other = Path.Combine(_root, "other.sqlite");
        using (var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = other, Pooling = false }.ToString()))
        {
            c.Open();
            Exec(c, "CREATE TABLE Foo (X integer);");
        }

        Assert.Equal(ImportFailure.WrongFormat, new StickyNotesSource().Read(junk).Failure);
        Assert.Equal(ImportFailure.WrongFormat, new StickyNotesSource().Read(other).Failure);
        Assert.Equal(ImportFailure.NotFound, new StickyNotesSource().Read(Path.Combine(_root, "zzz.sqlite")).Failure);
    }

    [Fact]
    public void DefaultPath_IsInsideTheStickyNotesPackage()
    {
        var p = StickyNotesSource.DefaultPath();
        Assert.True(p is null || p.EndsWith("plum.sqlite", StringComparison.OrdinalIgnoreCase));
    }
}
