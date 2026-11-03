using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// A key rotation that is cut halfway leaves the store with objects under the old key and objects under the new one, while
/// another device that still holds the old code keeps editing. Resuming must merge what that device wrote instead of
/// stamping the stale local copies with "now" (which beat the remote edits) and deleting the notes created meanwhile.
/// </summary>
public sealed class SyncInterruptedRotationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-rotation-{Guid.NewGuid():N}");
    private readonly string _shared;
    private readonly Device _a;
    private readonly Device _b;

    public SyncInterruptedRotationTests()
    {
        _shared = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_shared);
        _a = CreateDevice("device-a");
        _b = CreateDevice("device-b");
    }

    /// <summary>A transport that accepts a fixed number of writes and then fails like a dropped connection.</summary>
    private sealed class CutTransport(ISyncTransport inner, int writesAllowed) : ISyncTransport
    {
        public IReadOnlyList<SyncRemoteObject> ReadAll() => inner.ReadAll();
        public IReadOnlyCollection<Guid> UnreadableIds => inner.UnreadableIds;
        public void Write(SyncEnvelope envelope)
        {
            if (writesAllowed-- <= 0) throw new IOException("connection lost");
            inner.Write(envelope);
        }
        public void Delete(Guid noteId) => inner.Delete(noteId);
    }

    private sealed record Scene(Note Done, Note Stale, byte[] OldKey, byte[] NextKey);

    /// <summary>Two shared notes; A's rotation is cut after rewriting exactly one of them. Returns which one is which.</summary>
    private Scene InterruptARotation()
    {
        var first = _a.Repository.Create("first", "#EBD38B", "primary");
        var second = _a.Repository.Create("second", "#EBD38B", "primary");
        var code = _a.Sync.GetOrCreateSyncCode();
        _b.Sync.ImportSyncCode(code);
        Assert.True(_a.Sync.Synchronize().Succeeded);
        Assert.True(_b.Sync.Synchronize().Succeeded);
        var oldKey = DatabaseKeyProvider.Unwrap(_a.Settings.WrappedSyncKey!);

        _a.Sync.TransportFactoryOverride = () => new CutTransport(new FolderSyncTransport(_shared), writesAllowed: 1);
        Assert.False(_a.Sync.RevokeSharedAccess(out _));
        _a.Sync.TransportFactoryOverride = null;
        Assert.True(_a.Settings.SyncKeyRotationPending);

        var nextKey = DatabaseKeyProvider.Unwrap(_a.Settings.WrappedPendingSyncKey!);
        var stored = new FolderSyncTransport(_shared).ReadAll().ToDictionary(item => item.Envelope.NoteId);
        bool UnderNext(Guid id) => Decrypts(stored[id].Envelope, nextKey);
        Assert.NotEqual(UnderNext(first.Id), UnderNext(second.Id)); // exactly one was rewritten before the cut
        return UnderNext(first.Id) ? new Scene(first, second, oldKey, nextKey) : new Scene(second, first, oldKey, nextKey);
    }

    private static bool Decrypts(SyncEnvelope envelope, byte[] key)
    {
        try { SyncEnvelopeCodec.DecryptNote(envelope, key); return true; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }

    /// <summary>What device B (still on the old code) would have uploaded: written under the old key with its own date.</summary>
    private void RemoteWritesWithOldKey(Note note, byte[] oldKey) =>
        new FolderSyncTransport(_shared).Write(SyncEnvelopeCodec.CreateNote(note, oldKey, "device-b"));

    [Fact]
    public void ResumedRotation_KeepsARemoteEditAndANewRemoteNoteMadeWithTheOldKey()
    {
        var scene = InterruptARotation();
        Thread.Sleep(20);
        _b.Repository.UpdateText(scene.Stale.Id, "edited on B meanwhile");
        var edited = _b.Repository.GetById(scene.Stale.Id)!;
        var created = _b.Repository.Create("created on B meanwhile", "#EBD38B", "primary");
        RemoteWritesWithOldKey(edited, scene.OldKey);
        RemoteWritesWithOldKey(created, scene.OldKey);

        var result = _a.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.False(_a.Settings.SyncKeyRotationPending);
        Assert.Equal("edited on B meanwhile", _a.Repository.GetById(scene.Stale.Id)!.Text);
        Assert.Equal("created on B meanwhile", _a.Repository.GetById(created.Id)!.Text);
        var stored = new FolderSyncTransport(_shared).ReadAll().ToDictionary(item => item.Envelope.NoteId);
        Assert.Equal(3, stored.Count);
        Assert.All(stored.Values, item => Assert.True(Decrypts(item.Envelope, scene.NextKey)));
        // The date of the edit is the real one: a stamp of "now" would beat a later edit made elsewhere.
        Assert.Equal(edited.UpdatedAt, stored[scene.Stale.Id].Envelope.UpdatedAt);
        Assert.Equal("device-b", stored[scene.Stale.Id].Envelope.DeviceId);

        // A device joining with the new code gets everything.
        _b.Sync.ImportSyncCode(_a.Sync.GetOrCreateSyncCode());
        Assert.True(_b.Sync.Synchronize().Succeeded);
        Assert.Equal(3, _b.Repository.GetAllForSync().Count);
        Assert.Equal("edited on B meanwhile", _b.Repository.GetById(scene.Stale.Id)!.Text);
    }

    [Fact]
    public void ResumedRotation_KeepsALocalEditNewerThanTheRemoteOne()
    {
        var scene = InterruptARotation();
        _b.Repository.UpdateText(scene.Stale.Id, "older edit on B");
        RemoteWritesWithOldKey(_b.Repository.GetById(scene.Stale.Id)!, scene.OldKey);
        Thread.Sleep(20);
        _a.Repository.UpdateText(scene.Stale.Id, "newer edit on A");

        var result = _a.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("newer edit on A", _a.Repository.GetById(scene.Stale.Id)!.Text);
        var stored = new FolderSyncTransport(_shared).ReadAll().ToDictionary(item => item.Envelope.NoteId);
        Assert.Equal("newer edit on A", SyncEnvelopeCodec.DecryptNote(stored[scene.Stale.Id].Envelope, scene.NextKey).Text);
    }

    [Fact]
    public void ResumedRotation_AppliesASignedRemoteDeletionMadeWithTheOldKey()
    {
        var scene = InterruptARotation();
        Thread.Sleep(20);
        var deletion = new SyncTombstone(scene.Stale.Id, DateTimeOffset.UtcNow, "device-b");
        new FolderSyncTransport(_shared).Write(SyncEnvelopeCodec.CreateTombstone(deletion, scene.OldKey, "device-b"));

        var result = _a.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Null(_a.Repository.GetById(scene.Stale.Id));
        var stored = new FolderSyncTransport(_shared).ReadAll().ToDictionary(item => item.Envelope.NoteId);
        Assert.True(stored[scene.Stale.Id].Envelope.Tombstone);
        Assert.True(SyncEnvelopeCodec.IsAuthenticTombstone(stored[scene.Stale.Id].Envelope, scene.NextKey));
        Assert.Equal(deletion.DeletedAt, stored[scene.Stale.Id].Envelope.UpdatedAt);
    }

    [Fact]
    public void ResumedRotation_LeavesAnUnreadableObjectAloneInsteadOfDeletingIt()
    {
        var scene = InterruptARotation();
        var foreign = new SyncEnvelope
        {
            NoteId = Guid.NewGuid(), UpdatedAt = DateTimeOffset.UtcNow, DeviceId = "other",
            CipherText = Convert.ToBase64String(new byte[16]), Nonce = Convert.ToBase64String(new byte[12]), Tag = Convert.ToBase64String(new byte[16])
        };
        new FolderSyncTransport(_shared).Write(foreign);

        var result = _a.Sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains(new FolderSyncTransport(_shared).ReadAll(), item => item.Envelope.NoteId == foreign.NoteId);
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
            SyncFolderPath = _shared,
            SyncDeviceId = deviceId
        };
        settingsService.Save(settings);
        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, settings, new SyncService(repository, settings, settingsService));
    }

    public void Dispose()
    {
        foreach (var device in new[] { _a, _b })
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = device.DatabasePath }.ToString());
            SqliteConnection.ClearPool(connection);
        }
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed record Device(string DatabasePath, NotesRepository Repository, AppSettings Settings, SyncService Sync);
}
