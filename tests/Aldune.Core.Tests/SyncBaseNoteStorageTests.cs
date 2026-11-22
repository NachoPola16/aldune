using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

public sealed class SyncBaseNoteStorageTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-base-note-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));
    private readonly NotesRepository _repository;

    public SyncBaseNoteStorageTests()
    {
        Directory.CreateDirectory(_dir);
        _repository = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "notes.db")), _cipher);
    }

    public void Dispose()
    {
        _cipher.Dispose();
        TestPools.ReleaseDirectory(_dir);
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static Note Make(string text, bool isProtected = false) => new()
    {
        Id = Guid.NewGuid(), Text = text, Color = "#EBD38B", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        State = NoteState.Active, ScreenOrigin = "primary", Tags = new[] { "casa" }, IsProtected = isProtected
    };

    [Fact]
    public void RoundTripKeepsTheContent()
    {
        var note = Make("compra\npan");
        _repository.SetSyncBaseNotes([new(note.Id, note)]);

        var back = _repository.GetSyncBaseNotes()[note.Id];

        Assert.Equal("compra\npan", back.Text);
        Assert.Equal("#EBD38B", back.Color);
        Assert.Equal(new[] { "casa" }, back.Tags);
    }

    [Fact]
    public void StoredBlobIsNotPlaintext()
    {
        var note = Make("texto-secreto-12345");
        _repository.SetSyncBaseNotes([new(note.Id, note)]);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(_dir, "notes.db")};Pooling=False");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EncryptedNote FROM SyncBaseNote;";
        var blob = (byte[])command.ExecuteScalar()!;
        Assert.DoesNotContain("texto-secreto", System.Text.Encoding.UTF8.GetString(blob));
    }

    [Fact]
    public void ProfileKeysAreSeparate()
    {
        var note = Make("x");
        _repository.SetSyncBaseNotes([new(note.Id, note)], "other");

        Assert.Empty(_repository.GetSyncBaseNotes());
        Assert.Single(_repository.GetSyncBaseNotes("other"));
    }

    [Fact]
    public void MoveSyncBasesMovesTheSnapshotsToo()
    {
        var note = Make("x");
        _repository.SetSyncBaseNotes([new(note.Id, note)], "old");

        _repository.MoveSyncBases("old", "new");

        Assert.Empty(_repository.GetSyncBaseNotes("old"));
        Assert.Single(_repository.GetSyncBaseNotes("new"));
    }

    [Fact]
    public void DeletingAProfileForgetsItsSnapshots()
    {
        var note = Make("x");
        _repository.SetSyncBaseNotes([new(note.Id, note)], "p");

        _repository.DeleteSyncProfile("p");

        Assert.Empty(_repository.GetSyncBaseNotes("p"));
    }

    [Fact]
    public void ProtectedAndOversizedNotesAreNotStored()
    {
        var protectedNote = Make("x", isProtected: true);
        var big = Make(new string('a', NotesRepository.SyncBaseNoteMaxTextBytes + 1));
        var ok = Make(new string('a', NotesRepository.SyncBaseNoteMaxTextBytes));

        _repository.SetSyncBaseNotes([new(protectedNote.Id, protectedNote), new(big.Id, big), new(ok.Id, ok)]);

        Assert.Equal(new[] { ok.Id }, _repository.GetSyncBaseNotes().Keys);
    }

    [Fact]
    public void StampsAndSingleReadDoNotNeedTheWholeSet()
    {
        var a = Make("a");
        var b = Make("b");
        _repository.SetSyncBaseNotes([new(a.Id, a), new(b.Id, b)]);

        var stamps = _repository.GetSyncBaseNoteStamps();

        Assert.Equal(a.UpdatedAt, stamps[a.Id]);
        Assert.Equal(b.UpdatedAt, stamps[b.Id]);
        Assert.Equal("b", _repository.GetSyncBaseNote(b.Id)!.Text);
        Assert.Null(_repository.GetSyncBaseNote(Guid.NewGuid()));
    }

    [Fact]
    public void ReplacingASnapshotKeepsOnlyTheLatest()
    {
        var note = Make("one");
        _repository.SetSyncBaseNotes([new(note.Id, note)]);
        note.Text = "two";
        _repository.SetSyncBaseNotes([new(note.Id, note)]);

        Assert.Equal("two", _repository.GetSyncBaseNotes().Single().Value.Text);
    }
}
