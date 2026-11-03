using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

/// <summary>One row that does not decrypt (a partial restore, a damaged file) must not make every list, the sync and the automatic copy fail.</summary>
public class NotesRepositoryUnreadableRowTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"aldune-unreadable-{Guid.NewGuid():N}.db");
    private readonly NotesDatabase _database;
    private readonly NotesRepository _repository;

    public NotesRepositoryUnreadableRowTests()
    {
        _database = new NotesDatabase(_databasePath);
        _repository = new NotesRepository(_database, new ContentCipher(new byte[32]));
    }

    private void Damage(Guid id)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Note SET Tag = zeroblob(16) WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    [Fact]
    public void GetByState_SkipsTheUnreadableNoteAndReportsIt()
    {
        var good = _repository.Create("buena", "#EBD38B", "primary");
        var bad = _repository.Create("rota", "#AAE6B1", "primary");
        Damage(bad.Id);
        var reported = new List<string>();
        _repository.UnreadableNoteReported = reported.Add;

        var notes = _repository.GetByState(NoteState.Active);

        Assert.Equal([good.Id], notes.Select(n => n.Id));
        Assert.Single(reported);
        Assert.Contains(bad.Id.ToString(), reported[0]);
    }

    [Fact]
    public void GetAllForSync_SkipsTheUnreadableNote()
    {
        var good = _repository.Create("buena", "#EBD38B", "primary");
        var bad = _repository.Create("rota", "#AAE6B1", "primary");
        Damage(bad.Id);

        Assert.Equal([good.Id], _repository.GetAllForSync().Select(n => n.Id));
    }

    [Fact]
    public void ADamagedNoteAloneInItsStateDoesNotThrowWhileOthersDecrypt()
    {
        _repository.Create("activa buena", "#EBD38B", "primary");
        var archived = _repository.Create("archivada rota", "#AAE6B1", "primary");
        _repository.SetState(archived.Id, NoteState.Archived);
        Damage(archived.Id);

        Assert.Empty(_repository.GetByState(NoteState.Archived));
    }

    [Fact]
    public void ReportsAnUnreadableNoteOnlyOnce()
    {
        _repository.Create("buena", "#EBD38B", "primary");
        var bad = _repository.Create("rota", "#AAE6B1", "primary");
        Damage(bad.Id);
        var reported = new List<string>();
        _repository.UnreadableNoteReported = reported.Add;

        _repository.GetByState(NoteState.Active);
        _repository.GetByState(NoteState.Active);

        Assert.Single(reported);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_databasePath); } catch (IOException) { }
    }
}
