using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>Con la sync apagada, «Sincronizar ahora» tiene que decir que no hizo nada, no dar un falso éxito (suena «hecho» y habilita la poda de tareas).</summary>
public sealed class SyncDisabledTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-sync-disabled-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));
    private readonly SyncService _service;

    public SyncDisabledTests()
    {
        Directory.CreateDirectory(_dir);
        var repository = new NotesRepository(new NotesDatabase(Path.Combine(_dir, "notes.db")), _cipher);
        _service = new SyncService(repository, new AppSettings { SyncEnabled = false }, new SettingsService(Path.Combine(_dir, "settings.json")));
    }

    public void Dispose()
    {
        _cipher.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    [Fact]
    public void SyncNow_WithSyncTurnedOff_ReportsAnError()
    {
        var result = _service.Synchronize();

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AnAutomaticPass_WithNothingEnabled_IsAQuietNoOp()
    {
        var result = _service.Synchronize(automaticOnly: true);

        Assert.True(result.Succeeded);
        Assert.False(result.AppliedChanges);
    }
}
