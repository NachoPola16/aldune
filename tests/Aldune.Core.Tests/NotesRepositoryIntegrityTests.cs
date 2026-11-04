using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

/// <summary>
/// Data-integrity guards of the repository: a stale window must not overwrite what another device synced, a half-applied sync note must not
/// exist, and what is about to be lost (a replaced conflict, a protected note's clear history) is not left readable or discarded.
/// </summary>
public sealed class NotesRepositoryIntegrityTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aldune-integrity-{Guid.NewGuid():N}.db");
    private readonly NotesDatabase _database;
    private readonly NotesRepository _repo;

    public NotesRepositoryIntegrityTests()
    {
        _database = new NotesDatabase(_path);
        _repo = new NotesRepository(_database, new ContentCipher(new byte[32]));
    }

    public void Dispose()
    {
        TestPools.ReleaseFile(_path);
        try { File.Delete(_path); } catch (IOException) { }
    }

    private static Note Remote(Note basis, string text, int minutes = 1) => new()
    {
        Id = basis.Id, Text = text, Color = basis.Color, CreatedAt = basis.CreatedAt,
        UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(minutes), State = NoteState.Active, ScreenOrigin = "primary"
    };

    private static Note RemoteProtected(Note basis, string text, string password, int minutes = 1)
    {
        var note = Remote(basis, string.Empty, minutes);
        note.IsProtected = true;
        note.ProtectedContent = ProtectedNoteContent.Protect(text, password);
        return note;
    }

    // ---- A: protected note, stale write ----

    [Fact]
    public void ProtectedSave_AfterAnotherDeviceChangedTheContent_IsRefusedAndKeepsTheRemoteContent()
    {
        var note = _repo.Create("secret", "#EBD38B", "primary");
        _repo.Protect(note.Id, "pass1234");
        var opened = _repo.GetById(note.Id)!.ProtectedContent!;
        var remote = RemoteProtected(note, "edited on the phone", "pass1234");
        _repo.ApplySyncNote(remote, "phone");

        var result = _repo.TryUpdateProtectedText(note.Id, "typed on the pc", "pass1234", opened);

        Assert.False(result.Saved);
        Assert.True(_repo.TryUnlock(note.Id, "pass1234", out var unlocked));
        Assert.Equal("edited on the phone", unlocked!.Text);
    }

    [Fact]
    public void ProtectedSave_WhenNothingChangedRemotely_SavesAndReturnsTheNewContent()
    {
        var note = _repo.Create("secret", "#EBD38B", "primary");
        _repo.Protect(note.Id, "pass1234");
        var opened = _repo.GetById(note.Id)!.ProtectedContent!;

        var first = _repo.TryUpdateProtectedText(note.Id, "one", "pass1234", opened);
        var second = _repo.TryUpdateProtectedText(note.Id, "two", "pass1234", first.Content!);

        Assert.True(first.Saved);
        Assert.True(second.Saved);
        Assert.True(_repo.TryUnlock(note.Id, "pass1234", out var unlocked));
        Assert.Equal("two", unlocked!.Text);
    }

    // ---- B: pending edit over a synced text ----

    [Fact]
    public void SavingAnEdit_OverATextThatChangedUnderneath_KeepsTheRemoteTextAsAVersionEvenInsideTheInterval()
    {
        var note = _repo.Create("base", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "base typed");           // a version was just taken: the 5-minute interval is open
        _repo.ApplySyncNote(Remote(note, "from the phone"), "phone");

        var saved = _repo.SaveEditedText(note.Id, "base typed more", baseText: "base typed");

        Assert.True(saved);
        Assert.Equal("base typed more", _repo.GetById(note.Id)!.Text);
        Assert.Contains("from the phone", _repo.GetVersions(note.Id).Select(v => v.Text));
    }

    [Fact]
    public void SavingAnEdit_WhenTheStoredTextIsWhatTheWindowKnew_AddsNoExtraVersion()
    {
        var note = _repo.Create("base", "#EBD38B", "primary");

        _repo.SaveEditedText(note.Id, "next", baseText: "base");

        Assert.Equal(["base"], _repo.GetVersions(note.Id).Select(v => v.Text));
    }

    // ---- H: UpdateText does not discard silently ----

    [Fact]
    public void UpdateText_ReportsWhetherARowWasWritten()
    {
        var note = _repo.Create("x", "#EBD38B", "primary");
        Assert.True(_repo.UpdateText(note.Id, "y"));

        _repo.ApplySyncNote(RemoteProtected(note, "s", "pass1234"), "phone");

        Assert.False(_repo.UpdateText(note.Id, "z"));
        Assert.False(_repo.SaveEditedText(note.Id, "z", baseText: "y"));
        Assert.False(_repo.UpdateText(Guid.NewGuid(), "nobody"));
    }

    // ---- E: history of a note protected elsewhere ----

    [Fact]
    public void ASyncedNoteThatArrivesProtected_DropsItsClearHistory()
    {
        var note = _repo.Create("v0", "#EBD38B", "primary");
        _repo.UpdateText(note.Id, "v1");
        Assert.NotEmpty(_repo.GetVersions(note.Id));

        _repo.ApplySyncNote(RemoteProtected(note, "secret", "pass1234"), "phone");

        Assert.Empty(_repo.GetVersions(note.Id));
    }

    // ---- F: ApplySyncNote is all or nothing ----

    [Fact]
    public void ApplySyncNote_WhenTheTagsFail_LeavesTheNoteAsItWas()
    {
        var note = _repo.Create("old text", "#EBD38B", "primary");
        using (var connection = _database.OpenConnection())
        using (var trigger = connection.CreateCommand())
        {
            trigger.CommandText = "CREATE TRIGGER fail_tags BEFORE INSERT ON NoteTag BEGIN SELECT RAISE(ABORT, 'boom'); END;";
            trigger.ExecuteNonQuery();
        }
        var remote = Remote(note, "new text");
        remote.Tags = ["urgent"];

        Assert.ThrowsAny<Exception>(() => _repo.ApplySyncNote(remote, "phone"));

        Assert.Equal("old text", _repo.GetById(note.Id)!.Text);
    }

    // ---- G: pruning against a stale read ----

    [Fact]
    public void Prune_WithATextThatNoLongerMatchesTheStoredOne_WritesNothing()
    {
        var stale = "list\n☒ done";
        var note = _repo.Create(stale, "#EBD38B", "primary");
        _repo.RecordTaskCompletion(note.Id, TaskCompletion.HashLine("☒ done"), DateTimeOffset.UtcNow.AddDays(-2));
        _repo.ApplySyncNote(Remote(note, "list\nnew from the phone"), "phone");

        var changed = _repo.PruneExpiredCompletedTasks(note.Id, stale, TimeSpan.FromHours(1));

        Assert.False(changed);
        Assert.Equal("list\nnew from the phone", _repo.GetById(note.Id)!.Text);
    }

    // ---- I: a second conflict keeps the first loser ----

    [Fact]
    public void ASecondConflictForTheSameNote_KeepsTheFirstLosingTextInTheHistory()
    {
        var note = _repo.Create("current", "#EBD38B", "primary");
        var at = DateTimeOffset.UtcNow;
        SyncConflict Conflict(string losingText, int minutesAgo) => new(Guid.NewGuid(), note.Id, at,
            new SyncConflictVersion(at, "pc", false, null),
            new SyncConflictVersion(at.AddMinutes(-minutesAgo), "phone", false, Remote(note, losingText)), "profile");

        _repo.SaveSyncConflict(Conflict("first loser", 5));
        _repo.SaveSyncConflict(Conflict("second loser", 1));

        Assert.Single(_repo.GetSyncConflicts());
        Assert.Contains("first loser", _repo.GetVersions(note.Id).Select(v => v.Text));
    }
}
