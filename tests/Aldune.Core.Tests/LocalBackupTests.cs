using System.Security.Cryptography;
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class LocalBackupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-backup-test-{Guid.NewGuid():N}");
    private readonly string _databasePath;
    private readonly string _settingsPath;

    public LocalBackupTests()
    {
        Directory.CreateDirectory(_dir);
        _databasePath = Path.Combine(_dir, "notes.db");
        _settingsPath = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private void CreateNotes(params string[] texts)
    {
        var repository = new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(new byte[32]));
        foreach (var text in texts) repository.Create(text, "#EBD38B", "primary");
        File.WriteAllText(_settingsPath, "{\"KeepDockOpen\":true}");
    }

    [Fact]
    public void CreateDaily_CopiesTheDatabaseAndSettingsIntoAFolderForToday()
    {
        CreateNotes("uno", "dos");

        var folder = LocalBackup.CreateDaily(_dir, new DateTimeOffset(2026, 9, 23, 10, 0, 0, TimeSpan.Zero));

        Assert.NotNull(folder);
        Assert.Equal("2026-09-23", Path.GetFileName(folder));
        Assert.Equal("{\"KeepDockOpen\":true}", File.ReadAllText(Path.Combine(folder!, "settings.json")));
        var copy = new NotesRepository(new NotesDatabase(Path.Combine(folder!, "notes.db")), new ContentCipher(new byte[32]));
        Assert.Equal(new[] { "uno", "dos" }, copy.GetByState(NoteState.Active).Select(note => note.Text).OrderByDescending(t => t));
    }

    [Fact]
    public void CreateDaily_OnlyOncePerDay()
    {
        CreateNotes("uno");
        var morning = new DateTimeOffset(2026, 9, 23, 8, 0, 0, TimeSpan.Zero);

        Assert.NotNull(LocalBackup.CreateDaily(_dir, morning));
        Assert.Null(LocalBackup.CreateDaily(_dir, morning.AddHours(10)));
    }

    [Fact]
    public void CreateDaily_KeepsOnlyTheNewestDays()
    {
        CreateNotes("uno");
        var start = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

        for (int day = 0; day < 10; day++) LocalBackup.CreateDaily(_dir, start.AddDays(day), keep: 7);

        var folders = LocalBackup.ListNewestFirst(_dir).Select(Path.GetFileName).ToArray();
        Assert.Equal(7, folders.Length);
        Assert.Equal("2026-09-10", folders[0]);
        Assert.Equal("2026-09-04", folders[^1]);
    }

    [Fact]
    public void CreateDaily_WithNothingToBackUp_DoesNothing()
    {
        Assert.Null(LocalBackup.CreateDaily(_dir, DateTimeOffset.UtcNow));
        Assert.Empty(LocalBackup.ListNewestFirst(_dir));
    }

    [Fact]
    public void CreateDaily_LeavesTheLiveDatabaseUsable()
    {
        CreateNotes("uno");
        LocalBackup.CreateDaily(_dir, DateTimeOffset.UtcNow);

        var repository = new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(new byte[32]));
        repository.Create("después de la copia", "#EBD38B", "primary");

        Assert.Equal(2, repository.GetByState(NoteState.Active).Count);
    }

    // --- Recuperar la clave -----------------------------------------------------------------------

    [Fact]
    public void DatabaseHasNotes_IsFalseForAMissingOrEmptyDatabase()
    {
        Assert.False(DatabaseKeyRecovery.DatabaseHasNotes(_databasePath));
        new NotesDatabase(_databasePath);
        Assert.False(DatabaseKeyRecovery.DatabaseHasNotes(_databasePath));
        CreateNotes("uno");
        Assert.True(DatabaseKeyRecovery.DatabaseHasNotes(_databasePath));
    }

    [Fact]
    public void FindWrappedKey_ReturnsTheNewestBackedUpKeyThatOpensTheDatabase()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(key)).Create("secreto", "#EBD38B", "primary");
        var wrapped = DatabaseKeyProvider.Wrap(key);
        var wrongWrapped = DatabaseKeyProvider.Wrap(RandomNumberGenerator.GetBytes(32));

        WriteBackupSettings("2026-09-20", wrapped);
        WriteBackupSettings("2026-09-22", wrongWrapped); // más nueva, pero de otra base de datos

        var found = DatabaseKeyRecovery.FindWrappedKey(_dir, _databasePath);

        Assert.Equal(wrapped, found);
    }

    [Fact]
    public void FindWrappedKey_WithoutAUsableBackup_ReturnsNull()
    {
        new NotesRepository(new NotesDatabase(_databasePath), new ContentCipher(RandomNumberGenerator.GetBytes(32)))
            .Create("secreto", "#EBD38B", "primary");
        WriteBackupSettings("2026-09-20", DatabaseKeyProvider.Wrap(RandomNumberGenerator.GetBytes(32)));
        Directory.CreateDirectory(Path.Combine(_dir, LocalBackup.FolderName, "2026-09-21"));
        File.WriteAllText(Path.Combine(_dir, LocalBackup.FolderName, "2026-09-21", "settings.json"), "{roto");

        Assert.Null(DatabaseKeyRecovery.FindWrappedKey(_dir, _databasePath));
    }

    private void WriteBackupSettings(string day, byte[] wrappedKey)
    {
        var folder = Path.Combine(_dir, LocalBackup.FolderName, day);
        Directory.CreateDirectory(folder);
        new SettingsService(Path.Combine(folder, "settings.json")).Save(new AppSettings { WrappedDatabaseKey = wrappedKey });
    }

    [Fact]
    public void CreateDaily_RemovesStagingFoldersLeftByAFailedCopy()
    {
        // Una copia que falló (disco lleno, app cerrada a medias) dejaba su carpeta ".tmp" para siempre.
        CreateNotes("uno");
        var leftover = Path.Combine(_dir, LocalBackup.FolderName, "2026-09-01.tmp");
        Directory.CreateDirectory(leftover);
        File.WriteAllText(Path.Combine(leftover, "notes.db"), "a medias");

        LocalBackup.CreateDaily(_dir, new DateTimeOffset(2026, 9, 23, 12, 0, 0, TimeSpan.Zero));

        Assert.False(Directory.Exists(leftover));
    }
}
