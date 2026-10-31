using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Historial de versiones local de una nota: 20 como mucho, una cada 5 minutos de edición más una al cerrar, cifrado en la misma base,
/// sin sincronizar, restaurar guarda antes la versión actual y las notas protegidas no guardan nada.
/// </summary>
public sealed class NoteVersionHistoryTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aldune-hist-{Guid.NewGuid():N}.db");
    private readonly NotesRepository _repo;

    public NoteVersionHistoryTests()
    {
        _repo = new NotesRepository(new NotesDatabase(_path), new ContentCipher(new byte[32]));
    }

    [Fact]
    public void TheFirstEditKeepsTheTextThatWasThereBefore()
    {
        var note = _repo.Create("original", "#EBD38B", "primary");

        _repo.UpdateText(note.Id, "changed");

        var versions = _repo.GetVersions(note.Id);
        Assert.Single(versions);
        Assert.Equal("original", versions[0].Text);
    }

    [Fact]
    public void EditsInsideTheIntervalDoNotAddVersions()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "v1");
        _repo.UpdateText(note.Id, "v2");
        _repo.UpdateText(note.Id, "v3");

        Assert.Single(_repo.GetVersions(note.Id));
    }

    [Fact]
    public void AnEditAfterTheIntervalKeepsTheTextBeforeIt()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        var t0 = DateTimeOffset.UtcNow;
        _repo.UpdateText(note.Id, "v1", t0);
        _repo.UpdateText(note.Id, "v2", t0.AddMinutes(6));

        Assert.Equal(new[] { "v1", "v0" }, _repo.GetVersions(note.Id).Select(v => v.Text));
    }

    [Fact]
    public void ClosingKeepsTheCurrentTextOnceAndNeverTwiceInARow()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "v1");

        _repo.RecordVersion(note.Id, force: true);
        _repo.RecordVersion(note.Id, force: true);

        Assert.Equal(new[] { "v1", "v0" }, _repo.GetVersions(note.Id).Select(v => v.Text));
    }

    [Fact]
    public void OnlyTheLast20VersionsAreKept()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        var t = DateTimeOffset.UtcNow;
        for (int i = 1; i <= 30; i++) _repo.UpdateText(note.Id, $"v{i}", t.AddMinutes(10 * i));

        var versions = _repo.GetVersions(note.Id);

        Assert.Equal(NoteVersionRules.MaxVersions, versions.Count);
        Assert.Equal("v29", versions[0].Text);
        Assert.Equal("v10", versions[^1].Text);
    }

    [Fact]
    public void RestoringKeepsTheCurrentTextFirstSoItCanBeUndone()
    {
        var note = _repo.Create("old", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "current");
        var old = _repo.GetVersions(note.Id).Single();

        Assert.True(_repo.RestoreVersion(note.Id, old.Id));

        Assert.Equal("old", _repo.GetById(note.Id)!.Text);
        Assert.Contains("current", _repo.GetVersions(note.Id).Select(v => v.Text));
    }

    [Fact]
    public void RestoringAVersionOfAnotherNoteOrAMissingOneDoesNothing()
    {
        var a = _repo.Create("a0", "#EBD38B", "primary");
        var b = _repo.Create("b0", "#EBD38B", "primary");
        _repo.UpdateText(a.Id, "a1");

        var aVersion = _repo.GetVersions(a.Id).Single();

        Assert.False(_repo.RestoreVersion(b.Id, aVersion.Id));
        Assert.False(_repo.RestoreVersion(a.Id, 9999));
        Assert.Equal("b0", _repo.GetById(b.Id)!.Text);
    }

    [Fact]
    public void ProtectedNotesKeepNoHistory()
    {
        var note = _repo.Create("secret", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "secret 2");
        Assert.NotEmpty(_repo.GetVersions(note.Id));

        _repo.Protect(note.Id, "password");

        Assert.Empty(_repo.GetVersions(note.Id));
        _repo.RecordVersion(note.Id, force: true);
        Assert.Empty(_repo.GetVersions(note.Id));
    }

    [Fact]
    public void TheTextIsStoredEncrypted()
    {
        var note = _repo.Create("topsecret-marker", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "other");
        SqliteConnection.ClearAllPools();

        var raw = File.ReadAllBytes(_path);
        Assert.DoesNotContain("topsecret-marker", System.Text.Encoding.UTF8.GetString(raw));
    }

    [Fact]
    public void DeletingTheNoteForeverDeletesItsHistory()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "v1");

        _repo.Delete(note.Id);

        Assert.Empty(_repo.GetVersions(note.Id));
    }

    [Fact]
    public void ASyncedOverwriteKeepsTheLocalTextThatIsAboutToBeReplaced()
    {
        var note = _repo.Create("local text", "#EBD38B", "primary");
        var remote = new Note
        {
            Id = note.Id, Text = "text from another device", Color = note.Color, CreatedAt = note.CreatedAt,
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(1), State = NoteState.Active, ScreenOrigin = "primary"
        };

        _repo.ApplySyncNote(remote, "device-b");

        Assert.Equal("text from another device", _repo.GetById(note.Id)!.Text);
        Assert.Contains("local text", _repo.GetVersions(note.Id).Select(v => v.Text));
    }

    [Fact]
    public void TheHistoryItselfNeverTravelsInTheSyncEnvelope()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "v1");
        var key = new byte[32];

        var envelope = SyncEnvelopeCodec.CreateNote(_repo.GetById(note.Id)!, key, "d");
        var json = System.Text.Json.JsonSerializer.Serialize(envelope);

        Assert.DoesNotContain("version", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("v1", SyncEnvelopeCodec.DecryptNote(envelope, key).Text);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_path); } catch (IOException) { }
    }
}
