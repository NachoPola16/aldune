using Microsoft.Data.Sqlite;

namespace Fanote.Core.Tests;

/// <summary>
/// Reproduces the first real multi-device scenarios against two independent databases and one
/// shared folder transport. These tests deliberately sync in both directions instead of testing
/// only SyncVersion.Compare, because the dangerous bugs live at the boundary between tombstones,
/// encrypted envelopes and repository application.
/// </summary>
public sealed class SyncConflictTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fanote-sync-conflicts-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;
    private readonly Device _deviceA;
    private readonly Device _deviceB;

    public SyncConflictTests()
    {
        _sharedFolder = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_sharedFolder);
        _deviceA = CreateDevice("device-a");
        _deviceB = CreateDevice("device-b");
    }

    [Fact]
    public void ConcurrentEdits_UseTheNewestVersionAndConverge()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        // B publishes first; A edits second, so A is the deterministic winner. B must converge
        // to A on its next sync instead of keeping the stale local text.
        _deviceB.Repository.UpdateText(note.Id, "edit from B");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, "edit from A");

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(1, uploaded.ConflictsResolved);
        Assert.Equal(1, downloaded.ConflictsResolved);
        Assert.Equal("edit from A", _deviceB.Repository.GetAllForSync().Single().Text);
        Assert.Single(_deviceB.Sync.GetConflicts());

        // The losing version remains recoverable locally and can be promoted deliberately. The
        // restore gets a fresh timestamp so the next sync can publish the user's decision.
        var conflict = _deviceB.Sync.GetConflicts().Single();
        Assert.True(_deviceB.Sync.RestoreConflict(conflict.Id));
        Assert.Equal("edit from B", _deviceB.Repository.GetAllForSync().Single().Text);
        Assert.Empty(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void NewerDelete_BeatsAnOlderEditAndDoesNotResurrect()
    {
        var note = _deviceA.Repository.Create("original", "#AAE6B1", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        _deviceB.Repository.UpdateText(note.Id, "stale edit");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        Assert.True(_deviceA.Repository.Delete(note.Id));

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(1, uploaded.ConflictsResolved);
        Assert.Equal(1, downloaded.ConflictsResolved);
        Assert.Empty(_deviceB.Repository.GetAllForSync());
        Assert.Equal("device-a", _deviceB.Repository.GetSyncTombstones().Single().DeviceId);
    }

    [Fact]
    public void TombstoneEnvelope_UsesPublishingDeviceForTieBreaks()
    {
        var noteId = Guid.NewGuid();
        var deletedAt = DateTimeOffset.UtcNow;
        var tombstone = new SyncTombstone(noteId, deletedAt, "database-owner");

        var envelope = SyncEnvelopeCodec.CreateTombstone(tombstone, "publishing-device");

        Assert.Equal("publishing-device", envelope.DeviceId);
        Assert.Equal(deletedAt, envelope.UpdatedAt);
    }

    [Fact]
    public void SelectedScope_ExchangesOnlyTheChosenNote()
    {
        var selected = _deviceA.Repository.Create("shared", "#EBD38B", "primary");
        _deviceA.Repository.Create("private", "#AAE6B1", "primary");
        _deviceA.Settings.SyncScope = SyncScopeKind.SelectedNotes;
        _deviceA.Settings.SyncNoteIds = new List<Guid> { selected.Id };

        var code = _deviceA.Sync.GetOrCreateSyncCode();
        _deviceB.Sync.ImportSyncCode(code);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.Single(_deviceB.Repository.GetAllForSync());

        _deviceB.Repository.Create("created on B", "#83E7F2", "primary");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);

        Assert.DoesNotContain(_deviceA.Repository.GetAllForSync(), note => note.Text == "created on B");
    }

    [Fact]
    public void SharedProfileCodeCarriesTheSelectionToANewDevice()
    {
        var selected = _deviceA.Repository.Create("shared with another user", "#EBD38B", "primary");
        _deviceA.Repository.Create("private", "#AAE6B1", "primary");
        _deviceA.Settings.SyncScope = SyncScopeKind.SelectedNotes;
        _deviceA.Settings.SyncNoteIds = new List<Guid> { selected.Id };

        var code = _deviceA.Sync.GetOrCreateShareCode();
        _deviceB.Sync.ImportSyncCode(code);

        Assert.Equal(SyncScopeKind.SelectedNotes, _deviceB.Settings.SyncScope);
        Assert.Equal(new[] { selected.Id }, _deviceB.Settings.SyncNoteIds);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Assert.Equal("shared with another user", _deviceB.Repository.GetAllForSync().Single().Text);
    }

    [Fact]
    public void RevokingSharedAccessRequiresANewInvitation()
    {
        var note = _deviceA.Repository.Create("shared", "#EBD38B", "primary");
        ShareKeyAndSynchronizeInitialNote(note.Id);

        Assert.True(_deviceA.Sync.RevokeSharedAccess(out var error), error);

        // El código antiguo ya no puede descifrar la nueva versión del sobre.
        var oldCodeResult = _deviceB.Sync.Synchronize();
        Assert.False(oldCodeResult.Succeeded);

        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        var updated = _deviceB.Sync.Synchronize();
        Assert.True(updated.Succeeded, updated.Error);
        Assert.Equal("shared", _deviceB.Repository.GetAllForSync().Single().Text);
    }

    private void ShareKeyAndSynchronizeInitialNote(Guid noteId)
    {
        var code = _deviceA.Sync.GetOrCreateSyncCode();
        _deviceB.Sync.ImportSyncCode(code);

        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();

        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        Assert.Equal(1, downloaded.Downloaded);
        Assert.Equal(noteId, _deviceB.Repository.GetAllForSync().Single().Id);
    }

    private Device CreateDevice(string deviceId)
    {
        var deviceRoot = Path.Combine(_root, deviceId);
        Directory.CreateDirectory(deviceRoot);
        var settingsPath = Path.Combine(deviceRoot, "settings.json");
        var settingsService = new SettingsService(settingsPath);
        var settings = new AppSettings
        {
            SyncEnabled = true,
            SyncTransport = SyncTransportKind.Folder,
            SyncFolderPath = _sharedFolder,
            SyncDeviceId = deviceId
        };
        settingsService.Save(settings);

        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(
            new NotesDatabase(databasePath),
            new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, settings, new SyncService(repository, settings, settingsService));
    }

    public void Dispose()
    {
        foreach (var device in new[] { _deviceA, _deviceB })
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = device.DatabasePath }.ToString());
            SqliteConnection.ClearPool(connection);
        }

        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record Device(string DatabasePath, NotesRepository Repository, AppSettings Settings, SyncService Sync);
}
