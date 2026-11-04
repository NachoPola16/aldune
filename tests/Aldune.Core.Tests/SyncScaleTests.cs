using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Aldune.Core.Tests;

/// <summary>
/// Sync with thousands of notes through the real folder transport (two independent databases and one shared folder): the first pass must
/// converge in one round trip, the next pass must be quiet (nothing re-uploaded or re-downloaded), and a handful of edits afterwards must
/// cost a handful of transfers, not a full pass. The timings go to the test output so a regression in speed is visible.
/// </summary>
public sealed class SyncScaleTests : IDisposable
{
    private const int NoteCount = 3000;

    private readonly ITestOutputHelper _output;
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-scale-{Guid.NewGuid():N}");
    private readonly string _shared;
    private readonly Device _a;
    private readonly Device _b;

    public SyncScaleTests(ITestOutputHelper output)
    {
        _output = output;
        _shared = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_shared);
        _a = CreateDevice("device-a");
        _b = CreateDevice("device-b");
    }

    [Fact]
    public void ThousandsOfNotes_ConvergeInOneRoundTrip_ThenTheNextPassIsQuiet_AndAFewEditsCostAFewTransfers()
    {
        for (int i = 0; i < NoteCount; i++)
            _a.Repository.Create($"Note {i}\nbody of note {i} " + new string('x', i % 200), "#EBD38B", "primary");
        _b.Sync.ImportSyncCode(_a.Sync.GetOrCreateSyncCode());

        var upload = Timed("A first pass (upload)", () => _a.Sync.Synchronize());
        Assert.True(upload.Succeeded, upload.Error);
        Assert.Equal(NoteCount, upload.Uploaded);

        var downloadClock = Stopwatch.StartNew();
        var download = Timed("B first pass (download)", () => _b.Sync.Synchronize());
        // It was 30 s (one commit per note); grouped writes make it a fraction of a second. The bound is loose on purpose (slow CI disks).
        Assert.True(downloadClock.Elapsed < TimeSpan.FromSeconds(15), $"first download took {downloadClock.Elapsed}");
        Assert.True(download.Succeeded, download.Error);
        Assert.Equal(NoteCount, download.Downloaded);
        Assert.Equal(NoteCount, _b.Repository.GetByState(NoteState.Active).Count);

        var quietA = Timed("A quiet pass", () => _a.Sync.Synchronize());
        var quietB = Timed("B quiet pass", () => _b.Sync.Synchronize());
        foreach (var quiet in new[] { quietA, quietB })
        {
            Assert.True(quiet.Succeeded, quiet.Error);
            Assert.Equal(0, quiet.Uploaded);
            Assert.Equal(0, quiet.Downloaded);
            Assert.Equal(0, quiet.ConflictsResolved);
        }

        // A few edits, one trash and one new note on B: the next passes move exactly that.
        var some = _b.Repository.GetByState(NoteState.Active).Take(5).ToList();
        foreach (var note in some.Take(3)) _b.Repository.UpdateText(note.Id, note.Text + " edited");
        _b.Repository.SetState(some[3].Id, NoteState.Trashed);
        _b.Repository.Create("brand new", "#EBD38B", "primary");

        var push = Timed("B pass after 5 changes", () => _b.Sync.Synchronize());
        Assert.True(push.Succeeded, push.Error);
        Assert.Equal(5, push.Uploaded);
        var pull = Timed("A pass after B's changes", () => _a.Sync.Synchronize());
        Assert.True(pull.Succeeded, pull.Error);
        Assert.Equal(5, pull.Downloaded);
        Assert.Equal(NoteCount, _a.Repository.GetByState(NoteState.Active).Count); // -1 trashed, +1 new
    }

    [Fact]
    public void LargeNotes_DoNotStallTheGroupedWrites()
    {
        // 300 notes of ~20 KB: one group of 250 holds ~5 MB, more than SQLite's page cache, so it spills and takes the write lock. A read on another
        // connection made at that moment used to wait for the timeout and fail the pass.
        const int count = 300;
        for (int i = 0; i < count; i++)
            _a.Repository.Create($"Big {i}\n" + new string('x', 20_000), "#EBD38B", "primary");
        _b.Sync.ImportSyncCode(_a.Sync.GetOrCreateSyncCode());
        Assert.True(_a.Sync.Synchronize().Succeeded);

        var clock = Stopwatch.StartNew();
        var download = _b.Sync.Synchronize();

        Assert.True(download.Succeeded, download.Error);
        Assert.Equal(count, download.Downloaded);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(20), $"took {clock.Elapsed}");
        Assert.Equal(count, _b.Repository.GetByState(NoteState.Active).Count);
    }

    private T Timed<T>(string label, Func<T> action)
    {
        var clock = Stopwatch.StartNew();
        var result = action();
        _output.WriteLine($"{label}: {clock.ElapsedMilliseconds} ms ({result})");
        return result;
    }

    private Device CreateDevice(string deviceId)
    {
        var deviceRoot = Path.Combine(_root, deviceId);
        Directory.CreateDirectory(deviceRoot);
        var settingsService = new SettingsService(Path.Combine(deviceRoot, "settings.json"));
        var settings = new AppSettings
        {
            SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = _shared, SyncDeviceId = deviceId
        };
        settingsService.Save(settings);
        var databasePath = Path.Combine(deviceRoot, "notes.db");
        var repository = new NotesRepository(new NotesDatabase(databasePath), new ContentCipher(new byte[32]));
        return new Device(databasePath, repository, new SyncService(repository, settings, settingsService));
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

    private sealed record Device(string DatabasePath, NotesRepository Repository, SyncService Sync);
}
