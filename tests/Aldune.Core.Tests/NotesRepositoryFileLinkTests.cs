using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryFileLinkTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-link-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryFileLinkTests()
    {
        _sut = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private NoteFileLink LinkFor(Guid id, string path = @"Z:\apuntes\tema3.md", bool sync = false) =>
        new(id, path, sync, "HASH", new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero), "TEXT");

    [Fact]
    public void SaveAndGet_RoundTripsEveryField()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        var link = LinkFor(note.Id);

        _sut.SaveFileLink(link);

        Assert.Equal(link, _sut.GetFileLink(note.Id));
        Assert.Equal(new[] { link }, _sut.GetFileLinks());
    }

    [Fact]
    public void SaveFileLink_ReplacesTheExistingRow()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id));
        _sut.SaveFileLink(LinkFor(note.Id) with { KnownHash = "OTRO", SyncEnabled = true });

        Assert.Equal("OTRO", _sut.GetFileLink(note.Id)!.KnownHash);
        Assert.Single(_sut.GetFileLinks());
    }

    [Fact]
    public void ThePathIsNotStoredInClear()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id, @"Z:\secreto\diario.md"));

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EncryptedPath FROM NoteFileLink;";
        var raw = System.Text.Encoding.UTF8.GetString((byte[])command.ExecuteScalar()!);
        Assert.DoesNotContain("diario", raw);
    }

    [Fact]
    public void FindNoteByLinkedPath_IgnoresCase()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id, @"Z:\Apuntes\Tema3.md"));

        Assert.Equal(note.Id, _sut.FindNoteByLinkedPath(@"z:\apuntes\tema3.MD"));
        Assert.Null(_sut.FindNoteByLinkedPath(@"Z:\apuntes\otro.md"));
    }

    [Fact]
    public void GetUnsyncedLinkedNoteIds_ListsOnlyThoseWithoutSync()
    {
        var local = _sut.Create("a", "#EBD38B", "primary");
        var synced = _sut.Create("b", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(local.Id, @"C:\a.md"));
        _sut.SaveFileLink(LinkFor(synced.Id, @"C:\b.md", sync: true));

        Assert.Equal(new[] { local.Id }, _sut.GetUnsyncedLinkedNoteIds());
    }

    [Fact]
    public void DeletingTheNote_DeletesTheLinkButNeverTheFile()
    {
        var file = Path.Combine(Path.GetTempPath(), $"aldune-link-{Guid.NewGuid():N}.md");
        File.WriteAllText(file, "contenido");
        try
        {
            var note = _sut.Create("contenido", "#EBD38B", "primary");
            _sut.SaveFileLink(LinkFor(note.Id, file));

            Assert.True(_sut.Delete(note.Id));

            Assert.Null(_sut.GetFileLink(note.Id));
            Assert.True(File.Exists(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void PurgingTheTrashAndRemoteTombstones_DeleteTheLink()
    {
        var purged = _sut.Create("a", "#EBD38B", "primary");
        var tombstoned = _sut.Create("b", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(purged.Id, @"C:\a.md"));
        _sut.SaveFileLink(LinkFor(tombstoned.Id, @"C:\b.md"));
        _sut.SetState(purged.Id, NoteState.Trashed);

        _sut.PurgeExpiredTrash(TimeSpan.Zero);
        _sut.ApplySyncTombstone(new SyncTombstone(tombstoned.Id, DateTimeOffset.UtcNow, "otro"));

        Assert.Empty(_sut.GetFileLinks());
    }

    [Fact]
    public void ALinkedNote_CannotBeProtected()
    {
        var note = _sut.Create("x", "#EBD38B", "primary");
        _sut.SaveFileLink(LinkFor(note.Id));

        Assert.Throws<InvalidOperationException>(() => _sut.Protect(note.Id, "contraseña-larga-1"));
    }
}
