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
    public void HasSyncConflict_IsFalseWithoutConflicts()
    {
        Assert.False(_repository.HasSyncConflict(Guid.NewGuid()));
    }
}
