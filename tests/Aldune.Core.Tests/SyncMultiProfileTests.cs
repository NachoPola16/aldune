using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// La sincronización recorre todos los perfiles activados, no solo el elegido en el desplegable de Ajustes
/// (como ya hace Android). Cada perfil lleva sus propias bases de conflicto: si compartieran tabla, la versión
/// acordada con un almacén taparía un conflicto real en el otro.
/// </summary>
public sealed class SyncMultiProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"aldune-sync-multi-{Guid.NewGuid():N}");
    private readonly string _folderAll;
    private readonly string _folderShared;
    private readonly string _databasePath;
    private readonly NotesRepository _repository;
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly SyncService _sync;
    private readonly SyncProfileSettings _personal;
    private readonly SyncProfileSettings _family;
    private readonly Note _shared;

    public SyncMultiProfileTests()
    {
        _folderAll = Path.Combine(_root, "all");
        _folderShared = Path.Combine(_root, "shared");
        Directory.CreateDirectory(_folderAll);
        Directory.CreateDirectory(_folderShared);
        _databasePath = Path.Combine(_root, "notes.db");
        _repository = new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(new byte[32]));
        _settingsService = new SettingsService(Path.Combine(_root, "settings.json"));
        _settings = new AppSettings { SyncEnabled = true, SyncTransport = SyncTransportKind.Folder, SyncFolderPath = _folderAll };
        _settingsService.Save(_settings);
        _sync = new SyncService(_repository, _settings, _settingsService);

        _repository.Create("personal", "#EBD38B", "primary");
        _shared = _repository.Create("compra", "#EBD38B", "primary");

        _sync.GetOrCreateSyncCode();
        _personal = SyncProfileStore.GetActive(_settings);

        // El segundo perfil solo comparte la nota de la compra, en otro almacén.
        _family = SyncProfileStore.Create(_settings, "Familia");
        _settings.SyncEnabled = true;
        _settings.SyncFolderPath = _folderShared;
        _settings.SyncScope = SyncScopeKind.SelectedNotes;
        _settings.SyncNoteIds = [_shared.Id];
        _sync.GetOrCreateSyncCode();

        // Se deja elegido el primero: el otro NO está seleccionado en el desplegable.
        _settingsService.Save(_settings);
        _settings.ActiveSyncProfileId = _personal.Id;
        SyncProfileStore.LoadActiveToLegacy(_settings);
    }

    [Fact]
    public void AutomaticPass_SkipsTheProfilesSetToManual_WhileSyncNowStillRunsAll()
    {
        // La personal es automática y la de la familia, manual: el temporizador y la apertura de una nota solo tocan la primera.
        _settings.SyncAutomatically = true;

        var automatic = _sync.Synchronize(automaticOnly: true);

        Assert.True(automatic.Succeeded, automatic.Error);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(_folderAll, "objects")));
        Assert.False(Directory.Exists(Path.Combine(_folderShared, "objects")) && Directory.GetFiles(Path.Combine(_folderShared, "objects")).Length > 0);

        // «Sincronizar ahora» es una petición explícita: recorre todos.
        var manual = _sync.Synchronize();
        Assert.True(manual.Succeeded, manual.Error);
        Assert.NotEmpty(Directory.GetFiles(Path.Combine(_folderShared, "objects")));
    }

    [Fact]
    public void Synchronize_UploadsToEveryEnabledProfile_NotOnlyTheSelectedOne()
    {
        var result = _sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(3, result.Uploaded); // 2 notas al almacén personal + 1 al de la familia
        Assert.Equal(2, new FolderSyncTransport(_folderAll).ReadAll().Count);
        Assert.Single(new FolderSyncTransport(_folderShared).ReadAll());
        Assert.Equal(_personal.Id, _settings.ActiveSyncProfileId);
    }

    [Fact]
    public void Synchronize_SkipsAProfileThatIsNotEnabled()
    {
        _family.SyncEnabled = false;

        var result = _sync.Synchronize();

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.Uploaded);
        Assert.Empty(new FolderSyncTransport(_folderShared).ReadAll());
    }

    [Fact]
    public void Bases_AreKeptPerProfile()
    {
        Assert.True(_sync.Synchronize().Succeeded);

        Assert.Equal(2, _repository.GetSyncBases(_personal.BaseKey).Count);
        Assert.Single(_repository.GetSyncBases(_family.BaseKey));
        Assert.NotEqual(_personal.BaseKey, _family.BaseKey);
    }

    [Fact]
    public void AFailingProfile_DoesNotStopTheOthers_AndNamesItselfInTheError()
    {
        _family.SyncTransport = SyncTransportKind.Server;
        _family.SyncServerUrl = "http://127.0.0.1:1/";

        var result = _sync.Synchronize();

        Assert.False(result.Succeeded);
        Assert.Contains("Familia", result.Error);
        Assert.Equal(2, new FolderSyncTransport(_folderAll).ReadAll().Count);
    }

    [Fact]
    public void FirstProfileKeepsTheLegacyKey_OthersGetTheirOwn()
    {
        Assert.Equal("", _personal.BaseKey);
        Assert.Equal(_family.Id, _family.BaseKey);
    }

    [Fact]
    public void Conflicts_AreStoredPerProfile()
    {
        var at = DateTimeOffset.UtcNow;
        SyncConflict Make(string key) => new(Guid.NewGuid(), _shared.Id, at,
            new SyncConflictVersion(at, "a", false, null), new SyncConflictVersion(at, "b", true, null), key);

        _repository.SaveSyncConflict(Make(_personal.BaseKey));
        _repository.SaveSyncConflict(Make(_family.BaseKey));

        Assert.Equal(2, _repository.GetSyncConflicts().Count);
    }

    [Fact]
    public void LegacySyncBaseTable_KeepsItsRowsUnderTheFirstProfileKey()
    {
        var legacyPath = Path.Combine(_root, "legacy.db");
        var noteId = Guid.NewGuid();
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath }.ToString()))
        {
            connection.Open();
            using var create = connection.CreateCommand();
            create.CommandText = $"""
                CREATE TABLE SyncBase (NoteId TEXT PRIMARY KEY NOT NULL, UpdatedAt TEXT NOT NULL, DeviceId TEXT NOT NULL);
                INSERT INTO SyncBase VALUES ('{noteId}', '2026-10-01T10:00:00.0000000+00:00', 'pc');
                CREATE TABLE SyncConflict (Id TEXT PRIMARY KEY NOT NULL, NoteId TEXT NOT NULL, OccurredAt TEXT NOT NULL,
                    WinnerUpdatedAt TEXT NOT NULL, WinnerDeviceId TEXT NOT NULL, LosingUpdatedAt TEXT NOT NULL,
                    LosingDeviceId TEXT NOT NULL, LosingTombstone INTEGER NOT NULL, EncryptedNote BLOB, NoteNonce BLOB, NoteTag BLOB);
                """;
            create.ExecuteNonQuery();
        }

        var repository = new NotesRepository(new NotesDatabase(legacyPath), new ContentCipher(new byte[32]));

        Assert.Equal("pc", repository.GetSyncBases()[noteId].DeviceId);
        Assert.Empty(repository.GetSyncBases("otro"));
        Assert.Empty(repository.GetSyncConflicts());
        using var again = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = legacyPath }.ToString());
        SqliteConnection.ClearPool(again);
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
