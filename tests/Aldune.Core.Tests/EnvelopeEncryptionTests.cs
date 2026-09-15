using System.Security.Cryptography;
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

/// <summary>
/// Tests the full envelope-encryption chain end-to-end: generate a raw key, wrap it via DPAPI,
/// persist it through SettingsService, load it back from disk in a completely independent object
/// graph (simulating a fresh app launch), unwrap it, build a ContentCipher from it, and confirm it
/// can read notes written by a different ContentCipher/NotesRepository instance built the same way.
///
/// Every piece of this chain is already tested in isolation elsewhere (DatabaseKeyProviderTests,
/// ContentCipherTests, SettingsServiceTests, NotesRepositoryTests), but nothing else exercises the
/// composed sequence a real app run actually goes through.
/// </summary>
public class EnvelopeEncryptionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-envelope-test-{Guid.NewGuid()}.db");
    private readonly string _settingsDir = Path.Combine(Path.GetTempPath(), $"aldune-envelope-settings-{Guid.NewGuid()}");
    private readonly string _settingsPath;

    public EnvelopeEncryptionTests()
    {
        _settingsPath = Path.Combine(_settingsDir, "settings.json");
    }

    public void Dispose()
    {
        // Clear the SQLite connection pool to release file handles before deleting the temp .db
        // file. Microsoft.Data.Sqlite pools connections by default; Dispose() returns them to the
        // pool rather than closing them, so a plain File.Delete silently fails without this.
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);

        if (Directory.Exists(_settingsDir)) Directory.Delete(_settingsDir, recursive: true);
    }

    /// <summary>
    /// Builds a fresh, independent object graph exactly as App.xaml.cs's bootstrap would on a real
    /// launch: load settings from disk, unwrap the key, construct a ContentCipher, and wrap a
    /// NotesRepository around the shared database file. Each call constructs brand-new instances
    /// of every type in the chain, so two calls never share any in-memory state.
    /// </summary>
    private NotesRepository BuildRepositoryFromDisk()
    {
        var settingsService = new SettingsService(_settingsPath);
        var settings = settingsService.Load();
        var rawKey = DatabaseKeyProvider.Unwrap(settings.WrappedDatabaseKey!);
        var cipher = new ContentCipher(rawKey);
        var database = new NotesDatabase(_dbPath);
        return new NotesRepository(database, cipher);
    }

    [Fact]
    public void FullEnvelopeRoundTrip_KeyGeneratedWrappedSavedLoadedUnwrapped_DecryptsNotesWrittenByASeparateCipherInstance()
    {
        // Step 1: generate a key and wrap+save it, as the very first app launch would.
        var rawKey = DatabaseKeyProvider.GenerateKey();
        var wrapped = DatabaseKeyProvider.Wrap(rawKey);
        var initialSettingsService = new SettingsService(_settingsPath);
        initialSettingsService.Save(new AppSettings { WrappedDatabaseKey = wrapped });

        // Step 2: simulate a fresh app launch writing a note. A completely independent object
        // graph: new SettingsService, new load, new unwrap, new ContentCipher, new NotesRepository.
        var writerRepository = BuildRepositoryFromDisk();
        var created = writerRepository.Create("nota persistida a traves del sobre completo", "#F5E3B3", "primary");

        // Step 3: simulate reading it back on a later, again completely independent launch.
        var readerRepository = BuildRepositoryFromDisk();
        var active = readerRepository.GetByState(NoteState.Active);

        Assert.Single(active);
        Assert.Equal(created.Id, active[0].Id);
        Assert.Equal("nota persistida a traves del sobre completo", active[0].Text);
        Assert.Equal("#F5E3B3", active[0].Color);
        Assert.Equal(NoteState.Active, active[0].State);
        Assert.Equal("primary", active[0].ScreenOrigin);
    }

    [Fact]
    public void DecryptWithWrongKey_Fails()
    {
        var key1 = DatabaseKeyProvider.GenerateKey();
        var key2 = DatabaseKeyProvider.GenerateKey();

        var database = new NotesDatabase(_dbPath);

        using (var cipher1 = new ContentCipher(key1))
        {
            var writerRepository = new NotesRepository(database, cipher1);
            writerRepository.Create("texto cifrado con la clave correcta", "#FFFFFF", "primary");
        }

        using var cipher2 = new ContentCipher(key2);
        var readerRepository = new NotesRepository(database, cipher2);

        Assert.Throws<AuthenticationTagMismatchException>(() => readerRepository.GetByState(NoteState.Active));
    }

    [Fact]
    public void Create_WithEmptyText_RoundTrips()
    {
        var database = new NotesDatabase(_dbPath);
        var cipher = new ContentCipher(DatabaseKeyProvider.GenerateKey());
        var repository = new NotesRepository(database, cipher);

        var created = repository.Create(string.Empty, "#F5E3B3", "primary");

        var active = repository.GetByState(NoteState.Active);

        Assert.Single(active);
        Assert.Equal(created.Id, active[0].Id);
        Assert.Equal(string.Empty, active[0].Text);
    }
}
