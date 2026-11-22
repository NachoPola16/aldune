using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// The shopping-list case: two devices edit the same note between two syncs. Edits that do not collide merge
/// automatically; only the same line changed differently, or a deletion against an edit, stays a conflict.
/// </summary>
public sealed class SyncMergeScenarioTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-merge-{Guid.NewGuid():N}");
    private readonly string _sharedFolder;
    private readonly Device _deviceA;
    private readonly Device _deviceB;

    public SyncMergeScenarioTests()
    {
        _sharedFolder = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_sharedFolder);
        _deviceA = CreateDevice("device-a");
        _deviceB = CreateDevice("device-b");
    }

    // B edits and syncs first; A edits second (A is the newer one) and syncs: A is the device that sees both edits.
    private (SyncResult OnA, Note Note) BothEdit(string initial, string textB, string textA, Action<NotesRepository, Guid>? alsoB = null)
    {
        var note = _deviceA.Repository.Create(initial, "#EBD38B", "primary");
        var code = _deviceA.Sync.GetOrCreateSyncCode();
        _deviceB.Sync.ImportSyncCode(code);
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        _deviceB.Repository.UpdateText(note.Id, textB);
        alsoB?.Invoke(_deviceB.Repository, note.Id);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, textA);
        var onA = _deviceA.Sync.Synchronize();
        Assert.True(onA.Succeeded, onA.Error);
        return (onA, note);
    }

    private string TextOn(Device device, Guid id) => device.Repository.GetById(id)!.Text;

    [Fact]
    public void TickingDifferentTasksMerges()
    {
        var (onA, note) = BothEdit("compra\n☐ pan\n☐ leche\n☐ huevos",
            textB: "compra\n☐ pan\n☒ leche\n☐ huevos",
            textA: "compra\n☒ pan\n☐ leche\n☐ huevos");

        Assert.Equal(0, onA.ConflictsResolved);
        Assert.Equal(1, onA.Merged);
        Assert.Empty(_deviceA.Sync.GetConflicts());

        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        const string expected = "compra\n☒ pan\n☒ leche\n☐ huevos";
        Assert.Equal(expected, TextOn(_deviceA, note.Id));
        Assert.Equal(expected, TextOn(_deviceB, note.Id));
        Assert.Empty(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void AddingDifferentLinesKeepsBoth()
    {
        var (onA, note) = BothEdit("compra\npan", textB: "compra\npan\nleche", textA: "compra\npan\nhuevos");

        Assert.Equal(1, onA.Merged);
        Assert.Equal("compra\npan\nhuevos\nleche", TextOn(_deviceA, note.Id));
    }

    [Fact]
    public void ColourAfterATextEditKeepsTheTextEdit()
    {
        var note = _deviceA.Repository.Create("compra\npan", "#EBD38B", "primary");
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        _deviceA.Repository.UpdateText(note.Id, "compra\npan\nleche");
        Thread.Sleep(20);
        _deviceB.Repository.SetColor(note.Id, "#C2D4FF"); // newer than the text edit
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        var onA = _deviceA.Sync.Synchronize();

        Assert.Equal(0, onA.ConflictsResolved);
        Assert.Equal(1, onA.Merged);
        var merged = _deviceA.Repository.GetById(note.Id)!;
        Assert.Equal("compra\npan\nleche", merged.Text);
        Assert.Equal("#C2D4FF", merged.Color);
        Assert.Empty(_deviceA.Sync.GetConflicts());
    }

    [Fact]
    public void BothEditedThenSyncOneAfterTheOtherConverges()
    {
        var (_, note) = BothEdit("compra\npan", textB: "compra\npan\nleche", textA: "compra\npan\nhuevos");

        var onB = _deviceB.Sync.Synchronize();
        var onA = _deviceA.Sync.Synchronize();

        Assert.Equal(0, onB.ConflictsResolved + onA.ConflictsResolved);
        Assert.Equal(TextOn(_deviceA, note.Id), TextOn(_deviceB, note.Id));
        Assert.Empty(_deviceA.Sync.GetConflicts());
        Assert.Empty(_deviceB.Sync.GetConflicts());
    }

    [Fact]
    public void SameLineEditedDifferentlyStillConflicts()
    {
        var (onA, _) = BothEdit("compra\npan", textB: "compra\npan integral", textA: "compra\npan de molde");

        Assert.Equal(1, onA.ConflictsResolved);
        Assert.Equal(0, onA.Merged);
        Assert.Single(_deviceA.Sync.GetConflicts());
    }

    [Fact]
    public void EditAgainstADeletionStillConflicts()
    {
        var note = _deviceA.Repository.Create("compra\npan", "#EBD38B", "primary");
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        Assert.True(_deviceB.Repository.Delete(note.Id));
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, "compra\npan\nleche");
        var onA = _deviceA.Sync.Synchronize();

        Assert.Equal(0, onA.Merged);
    }

    [Fact]
    public void WithoutASnapshotBehavesAsBefore()
    {
        var note = _deviceA.Repository.Create("compra\npan", "#EBD38B", "primary");
        _deviceB.Sync.ImportSyncCode(_deviceA.Sync.GetOrCreateSyncCode());
        Assert.True(_deviceA.Sync.Synchronize().Succeeded);
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);

        // A stored protected stub drops the snapshot (an install from before this feature has none).
        _deviceA.Repository.SetSyncBaseNotes([new(note.Id, new Note
        {
            Id = note.Id, Text = "", Color = "#EBD38B", CreatedAt = note.CreatedAt, UpdatedAt = note.UpdatedAt,
            State = NoteState.Active, ScreenOrigin = "primary", IsProtected = true
        })]);
        Assert.Empty(_deviceA.Repository.GetSyncBaseNotes());

        _deviceB.Repository.UpdateText(note.Id, "compra\npan\nleche");
        Assert.True(_deviceB.Sync.Synchronize().Succeeded);
        Thread.Sleep(20);
        _deviceA.Repository.UpdateText(note.Id, "compra\npan\nhuevos");
        var onA = _deviceA.Sync.Synchronize();

        Assert.Equal(1, onA.ConflictsResolved);
        Assert.Equal(0, onA.Merged);
    }

    [Fact]
    public void BothPreMergeTextsAreKeptInHistory()
    {
        var (_, note) = BothEdit("compra\npan", textB: "compra\npan\nleche", textA: "compra\npan\nhuevos");

        var kept = _deviceA.Repository.GetVersions(note.Id).Select(version => version.Text).ToList();

        Assert.Contains("compra\npan\nleche", kept);
        Assert.Contains("compra\npan\nhuevos", kept);
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
