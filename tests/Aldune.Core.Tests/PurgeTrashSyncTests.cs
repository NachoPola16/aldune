using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Purging the bin through real sync (two databases and a shared folder). The tombstone a purge leaves must be dated
/// with the moment the note entered the bin, not with the purge: otherwise it beats a restore and edit made on a device
/// that was offline during those 30 days, and that note would be lost.
/// </summary>
public sealed class PurgeTrashSyncTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-purge-sync-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;
    private readonly Device _deviceA;
    private readonly Device _deviceB;

    public PurgeTrashSyncTests()
    {
        _sharedFolder = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_sharedFolder);
        _deviceA = CreateDevice("device-a");
        _deviceB = CreateDevice("device-b");
    }

    [Fact]
    public void ANoteRestoredAndEditedOnAnotherDeviceSurvivesThePurgeOnTheOneWhereItWasTrashed()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetState(note.Id, NoteState.Trashed);
        SyncBoth();
        Thread.Sleep(20);

        // B restores and edits the note while A is not looking; A then purges its bin.
        _deviceB.Repository.SetState(note.Id, NoteState.Active);
        _deviceB.Repository.UpdateText(note.Id, "edited on B");
        Thread.Sleep(20);
        Assert.Equal(1, _deviceA.Repository.PurgeExpiredTrash(TimeSpan.Zero));
        SyncBoth();
        SyncBoth();

        foreach (var device in new[] { _deviceA, _deviceB })
        {
            var kept = device.Repository.GetById(note.Id);
            Assert.NotNull(kept);
            Assert.Equal("edited on B", kept!.Text);
            Assert.Equal(NoteState.Active, kept.State);
        }
    }

    [Fact]
    public void ThePurgeStillReachesAnotherDeviceThatLeftTheNoteInTheBin()
    {
        var note = _deviceA.Repository.Create("original", "#EBD38B", "primary");
        ShareKeyAndSynchronize();
        _deviceA.Repository.SetState(note.Id, NoteState.Trashed);
        SyncBoth();
        Thread.Sleep(20);

        Assert.Equal(1, _deviceA.Repository.PurgeExpiredTrash(TimeSpan.Zero));
        SyncBoth();

        Assert.Null(_deviceA.Repository.GetById(note.Id));
        Assert.Null(_deviceB.Repository.GetById(note.Id));
    }

    [Fact]
    public void ThePurgeLeavesNoOrderOrTaskLeftovers()
    {
        var note = _deviceA.Repository.Create("[ ] task", "#EBD38B", "primary");
        var other = _deviceA.Repository.Create("other", "#EBD38B", "primary");
        _deviceA.Repository.MoveNote(other.Id, 0, new[] { other.Id, note.Id });
        _deviceA.Repository.RecordTaskCompletion(note.Id, "hash", DateTimeOffset.UtcNow);
        _deviceA.Repository.SetState(note.Id, NoteState.Trashed);
        Thread.Sleep(20);

        Assert.Equal(1, _deviceA.Repository.PurgeExpiredTrash(TimeSpan.Zero));

        Assert.Empty(_deviceA.Repository.GetTaskCompletions(note.Id));
        Assert.DoesNotContain(note.Id, _deviceA.Repository.GetOrder().Keys);
    }

    [Fact]
    public void DeletingForGoodAlsoDropsTheManualOrderRow()
    {
        var note = _deviceA.Repository.Create("a", "#EBD38B", "primary");
        var other = _deviceA.Repository.Create("b", "#EBD38B", "primary");
        _deviceA.Repository.MoveNote(other.Id, 0, new[] { other.Id, note.Id });
        Assert.Contains(note.Id, _deviceA.Repository.GetOrder().Keys);

        Assert.True(_deviceA.Repository.Delete(note.Id));

        Assert.DoesNotContain(note.Id, _deviceA.Repository.GetOrder().Keys);
    }

    private void ShareKeyAndSynchronize()
    {
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        SyncBoth();
    }

    private void SyncBoth()
    {
        var uploaded = _deviceA.Sync.Synchronize();
        var downloaded = _deviceB.Sync.Synchronize();
        Assert.True(uploaded.Succeeded, uploaded.Error);
        Assert.True(downloaded.Succeeded, downloaded.Error);
        var back = _deviceA.Sync.Synchronize();
        Assert.True(back.Succeeded, back.Error);
    }

    private Device CreateDevice(string deviceId)
    {
        var deviceRoot = Path.Combine(_root, deviceId);
        Directory.CreateDirectory(deviceRoot);
        var settingsService = new SettingsService(Path.Combine(deviceRoot, "settings.json"));
        var settings = new AppSettings
        {
            SyncEnabled = true,
            SyncTransport = SyncTransportKind.Folder,
            SyncFolderPath = _sharedFolder,
            SyncDeviceId = deviceId
        };
        settingsService.Save(settings);

        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, new SyncService(repository, settings, settingsService));
    }

    public void Dispose()
    {
        foreach (var device in new[] { _deviceA, _deviceB })
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = device.DatabasePath }.ToString());
            SqliteConnection.ClearPool(connection);
        }

        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record Device(string DatabasePath, NotesRepository Repository, SyncService Sync);
}
