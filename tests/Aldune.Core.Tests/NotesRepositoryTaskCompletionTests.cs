using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryTaskCompletionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-taskcompletion-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryTaskCompletionTests()
    {
        var database = new NotesDatabase(_dbPath);
        var cipher = new ContentCipher(new byte[32]); // fixed all-zero test key
        _sut = new NotesRepository(database, cipher);
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);

        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void RecordTaskCompletion_ThenGetTaskCompletions_RoundTrips()
    {
        var note = _sut.Create("☒ tarea", "#F5E3B3", "primary");
        var completedAt = new DateTimeOffset(2026, 9, 9, 10, 0, 0, TimeSpan.Zero);

        _sut.RecordTaskCompletion(note.Id, "abc123", completedAt);

        var completions = _sut.GetTaskCompletions(note.Id);
        Assert.Equal(completedAt, Assert.Single(completions).Value);
    }

    [Fact]
    public void RecordTaskCompletion_CalledTwiceForTheSameHash_OverwritesTheTimestamp()
    {
        var note = _sut.Create("☒ tarea", "#F5E3B3", "primary");
        _sut.RecordTaskCompletion(note.Id, "abc123", DateTimeOffset.UtcNow - TimeSpan.FromDays(5));

        var newer = DateTimeOffset.UtcNow;
        _sut.RecordTaskCompletion(note.Id, "abc123", newer);

        var completions = _sut.GetTaskCompletions(note.Id);
        Assert.Single(completions);
        Assert.Equal(newer, completions["abc123"]);
    }

    [Fact]
    public void ClearTaskCompletion_RemovesOnlyThatHash()
    {
        var note = _sut.Create("☒ una\r\n☒ otra", "#F5E3B3", "primary");
        _sut.RecordTaskCompletion(note.Id, "hash1", DateTimeOffset.UtcNow);
        _sut.RecordTaskCompletion(note.Id, "hash2", DateTimeOffset.UtcNow);

        _sut.ClearTaskCompletion(note.Id, "hash1");

        var completions = _sut.GetTaskCompletions(note.Id);
        Assert.Single(completions);
        Assert.True(completions.ContainsKey("hash2"));
    }

    [Fact]
    public void GetTaskCompletions_WithNoneRecorded_ReturnsEmpty()
    {
        var note = _sut.Create("sin tareas", "#F5E3B3", "primary");
        Assert.Empty(_sut.GetTaskCompletions(note.Id));
    }

    [Fact]
    public void PruneExpiredCompletedTasks_RemovesTheExpiredLineAndSavesTheNote()
    {
        var line = "☒ tarea vencida";
        var text = $"algo\r\n{line}";
        var note = _sut.Create(text, "#F5E3B3", "primary");
        var hash = TaskCompletion.HashLine(line);
        _sut.RecordTaskCompletion(note.Id, hash, DateTimeOffset.UtcNow - TimeSpan.FromDays(3));

        var changed = _sut.PruneExpiredCompletedTasks(note.Id, text, TimeSpan.FromDays(1));

        Assert.True(changed);
        var saved = _sut.GetByState(NoteState.Active).Single(n => n.Id == note.Id);
        Assert.Equal("algo", saved.Text);
    }

    [Fact]
    public void PruneExpiredCompletedTasks_CleansUpTheCompletionRecordForTheRemovedLine()
    {
        var line = "☒ tarea vencida";
        var text = line;
        var note = _sut.Create(text, "#F5E3B3", "primary");
        var hash = TaskCompletion.HashLine(line);
        _sut.RecordTaskCompletion(note.Id, hash, DateTimeOffset.UtcNow - TimeSpan.FromDays(3));

        _sut.PruneExpiredCompletedTasks(note.Id, text, TimeSpan.FromDays(1));

        Assert.Empty(_sut.GetTaskCompletions(note.Id));
    }

    [Fact]
    public void PruneExpiredCompletedTasks_WithNothingExpired_DoesNotTouchTheNote()
    {
        var line = "☒ tarea reciente";
        var text = line;
        var note = _sut.Create(text, "#F5E3B3", "primary");
        var hash = TaskCompletion.HashLine(line);
        _sut.RecordTaskCompletion(note.Id, hash, DateTimeOffset.UtcNow);

        var changed = _sut.PruneExpiredCompletedTasks(note.Id, text, TimeSpan.FromDays(1));

        Assert.False(changed);
        var saved = _sut.GetByState(NoteState.Active).Single(n => n.Id == note.Id);
        Assert.Equal(text, saved.Text);
    }

    [Fact]
    public void Delete_RemovesTaskCompletionRecordsToo()
    {
        var note = _sut.Create("☒ tarea", "#F5E3B3", "primary");
        _sut.RecordTaskCompletion(note.Id, "abc123", DateTimeOffset.UtcNow);

        _sut.Delete(note.Id);

        Assert.Empty(_sut.GetTaskCompletions(note.Id));
    }
}
