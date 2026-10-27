using System.Security.Cryptography;
using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public sealed class NotesRepositorySyncLookupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-sync-lookup-{Guid.NewGuid():N}");
    private readonly ContentCipher _cipher = new(RandomNumberGenerator.GetBytes(32));
    private readonly NotesRepository _repository;

    public NotesRepositorySyncLookupTests()
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

    [Fact]
    public void GetSyncBase_ReturnsOnlyThatNotesBase()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var at = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
        _repository.SetSyncBases([new(a, new SyncBaseVersion(at, "pc"))]);

        Assert.Equal(new SyncBaseVersion(at, "pc"), _repository.GetSyncBase(a));
        Assert.Null(_repository.GetSyncBase(b));
    }

    [Fact]
    public void DeleteSyncProfile_RemovesOnlyThatProfilesBases_SoTheSignalNoLongerStaysPending()
    {
        var id = Guid.NewGuid();
        var old = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
        var recent = old.AddDays(2);
        _repository.SetSyncBases([new(id, new SyncBaseVersion(old, "pc"))], "perfil-borrado");
        _repository.SetSyncBases([new(id, new SyncBaseVersion(recent, "pc"))], "perfil-vivo");
        // Con las dos filas vale la más antigua, la del perfil que ya no existe.
        Assert.Equal(old, _repository.GetSyncBase(id)!.UpdatedAt);

        _repository.DeleteSyncProfile("perfil-borrado");

        Assert.Equal(recent, _repository.GetSyncBase(id)!.UpdatedAt);
        Assert.Single(_repository.GetSyncBases("perfil-vivo"));
        Assert.Empty(_repository.GetSyncBases("perfil-borrado"));
    }

    [Fact]
    public void GetSyncBase_WithProfileKeys_IgnoresTheBasesOfOtherProfiles()
    {
        var id = Guid.NewGuid();
        var old = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
        var recent = old.AddDays(2);
        _repository.SetSyncBases([new(id, new SyncBaseVersion(old, "pc"))], "perfil-apagado");
        _repository.SetSyncBases([new(id, new SyncBaseVersion(recent, "pc"))], "perfil-vivo");

        Assert.Equal(recent, _repository.GetSyncBase(id, ["perfil-vivo"])!.UpdatedAt);
        Assert.Equal(old, _repository.GetSyncBase(id, ["perfil-vivo", "perfil-apagado"])!.UpdatedAt); // con los dos vale el más atrasado
        Assert.Null(_repository.GetSyncBase(id, ["otro"]));
    }

    [Fact]
    public void DeleteSyncProfile_KeepsItsUnresolvedConflicts_BecauseTheyHoldTheOnlyCopyOfTheLosingVersion()
    {
        var at = new DateTimeOffset(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
        var noteId = Guid.NewGuid();
        _repository.SaveSyncConflict(new SyncConflict(Guid.NewGuid(), noteId, at,
            new SyncConflictVersion(at, "pc", false, null), new SyncConflictVersion(at.AddMinutes(-1), "movil", true, null), "perfil-borrado"));

        _repository.DeleteSyncProfile("perfil-borrado");

        Assert.Single(_repository.GetSyncConflicts());
    }

    [Fact]
    public void HasSyncConflict_IsFalseWithoutConflicts()
    {
        Assert.False(_repository.HasSyncConflict(Guid.NewGuid()));
    }
}
