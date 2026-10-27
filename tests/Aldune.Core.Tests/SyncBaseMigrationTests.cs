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
    public void WhenTheActiveProfileIsNotTheFirst_TheOldBasesMoveToIt()
    {
        var settings = TwoProfilesWithTheSecondActive(out var second);
        var id = Guid.NewGuid();
        _repository.SetSyncBases([new(id, new SyncBaseVersion(DateTimeOffset.UtcNow, "pc"))], "");

        SyncBaseMigration.Run(settings, _repository);

        Assert.Empty(_repository.GetSyncBases(""));
        Assert.Single(_repository.GetSyncBases(SyncProfileStore.BaseKeyOfActive(settings)));
        Assert.True(settings.SyncBasesKeyed);
    }

    [Fact]
    public void WhenTheActiveProfileIsTheFirst_NothingMoves_ButItIsMarkedDone()
    {
        var settings = new AppSettings { SyncEnabled = true };
        SyncProfileStore.Ensure(settings);
        _repository.SetSyncBases([new(Guid.NewGuid(), new SyncBaseVersion(DateTimeOffset.UtcNow, "pc"))], "");

        SyncBaseMigration.Run(settings, _repository);

        Assert.Single(_repository.GetSyncBases(""));
        Assert.True(settings.SyncBasesKeyed);
    }

    [Fact]
    public void ItRunsOnlyOnce_SoLaterBasesOfTheFirstProfileAreNotStolen()
    {
        var settings = TwoProfilesWithTheSecondActive(out _);
        SyncBaseMigration.Run(settings, _repository);

        // Después de la migración, la clave vacía vuelve a ser la del primer perfil.
        _repository.SetSyncBases([new(Guid.NewGuid(), new SyncBaseVersion(DateTimeOffset.UtcNow, "pc"))], "");
        SyncBaseMigration.Run(settings, _repository);

        Assert.Single(_repository.GetSyncBases(""));
    }
}
