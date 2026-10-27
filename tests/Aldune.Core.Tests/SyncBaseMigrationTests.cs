using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Antes solo sincronizaba el perfil elegido, y sus bases vivían en una única tabla. Al pasar a una clave por perfil, la migración de la
/// base de datos las deja todas bajo la clave vacía (la del primer perfil); si quien sincronizaba era otro, hay que moverlas a ese.
/// </summary>
public sealed class SyncBaseMigrationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-base-migration-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));
    private readonly NotesRepository _repository;

    public SyncBaseMigrationTests()
    {
        Directory.CreateDirectory(_dir);
        _repository = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "notes.db")), _cipher);
    }

    /// <summary>Una base de datos con la tabla SyncBase de antes de los perfiles (sin ProfileKey), que se migra al abrirla.</summary>
    private NotesRepository LegacyRepository(Guid noteId)
    {
        var path = Path.Combine(_dir, "legacy.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE SyncBase (NoteId TEXT PRIMARY KEY, UpdatedAt TEXT NOT NULL, DeviceId TEXT NOT NULL); " +
                $"INSERT INTO SyncBase VALUES ('{noteId}', '2026-10-03T09:00:00.0000000+00:00', 'pc');";
            command.ExecuteNonQuery();
        }
        return new NotesRepository(new NotesDatabase(path), _cipher);
    }

    public void Dispose()
    {
        _cipher.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private static AppSettings TwoProfilesWithTheSecondActive(out SyncProfileSettings second)
    {
        var settings = new AppSettings { SyncEnabled = true };
        SyncProfileStore.Ensure(settings);
        second = SyncProfileStore.Create(settings, "Familia");
        settings.ActiveSyncProfileId = second.Id;
        SyncProfileStore.LoadActiveToLegacy(settings);
        return settings;
    }

    [Fact]
    public void FromAPreProfilesDatabase_WhenTheActiveProfileIsNotTheFirst_TheOldBasesMoveToIt()
    {
        var settings = TwoProfilesWithTheSecondActive(out _);
        var legacy = LegacyRepository(Guid.NewGuid());
        Assert.True(legacy.LegacySyncBasesMigratedOnOpen);
        Assert.Single(legacy.GetSyncBases(""));

        SyncBaseMigration.Run(settings, legacy);

        Assert.Empty(legacy.GetSyncBases(""));
        Assert.Single(legacy.GetSyncBases(SyncProfileStore.BaseKeyOfActive(settings)));
        Assert.True(settings.SyncBasesKeyed);
    }

    [Fact]
    public void FromAPreProfilesDatabase_WhenTheActiveProfileIsTheFirst_NothingMoves_ButItIsMarkedDone()
    {
        var settings = new AppSettings { SyncEnabled = true };
        SyncProfileStore.Ensure(settings);
        var legacy = LegacyRepository(Guid.NewGuid());

        SyncBaseMigration.Run(settings, legacy);

        Assert.Single(legacy.GetSyncBases(""));
        Assert.True(settings.SyncBasesKeyed);
    }

    [Fact]
    public void AnAlreadyKeyedDatabase_IsNeverTouched_BecauseItsEmptyKeyBelongsToTheFirstProfile()
    {
        // La 1.7.18-rc1 ya guardaba las bases por perfil: lo que hay bajo la clave vacía es del primero, aunque el elegido sea otro.
        var settings = TwoProfilesWithTheSecondActive(out _);
        Assert.False(_repository.LegacySyncBasesMigratedOnOpen);
        _repository.SetSyncBases([new(Guid.NewGuid(), new SyncBaseVersion(DateTimeOffset.UtcNow, "pc"))], "");

        SyncBaseMigration.Run(settings, _repository);

        Assert.Single(_repository.GetSyncBases(""));
        Assert.True(settings.SyncBasesKeyed);
    }

    [Fact]
    public void ItRunsOnlyOnce()
    {
        var settings = TwoProfilesWithTheSecondActive(out _);
        var legacy = LegacyRepository(Guid.NewGuid());
        SyncBaseMigration.Run(settings, legacy);

        legacy.SetSyncBases([new(Guid.NewGuid(), new SyncBaseVersion(DateTimeOffset.UtcNow, "pc"))], "");
        SyncBaseMigration.Run(settings, legacy);

        Assert.Single(legacy.GetSyncBases(""));
    }
}
