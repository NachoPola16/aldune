using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryTaskCompletionTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-taskcompletion-test-{Guid.NewGuid()}.db");
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
        var text = $"título\r\n{line}";
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
        var text = $"título\r\n{line}";
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
    [Fact]
    public void PruneExpiredCompletedTasks_NeverTouchesTheTitle()
    {
        // La ventana solo mira el cuerpo; el barrido de arranque miraba también el título y se
        // comía "☒ llamar al banco" (y con él, el título de la nota).
        var text = "☒ llamar al banco\r\ncuerpo";
        var note = _sut.Create(text, "#F5E3B3", "primary");
        _sut.RecordTaskCompletion(note.Id, TaskCompletion.HashLine("☒ llamar al banco"), DateTimeOffset.UtcNow - TimeSpan.FromDays(3));

        Assert.False(_sut.PruneExpiredCompletedTasks(note.Id, text, TimeSpan.FromDays(1)));
        Assert.Equal(text, _sut.GetById(note.Id)!.Text);
    }

    [Fact]
    public void PruneExpiredCompletedTasks_StartsTheClockOfCheckedTasksWithoutOne()
    {
        var text = "título\r\n☒ marcada sin registro";
        var note = _sut.Create(text, "#F5E3B3", "primary");
        var before = DateTimeOffset.UtcNow;

        _sut.PruneExpiredCompletedTasks(note.Id, text, TimeSpan.FromDays(1));

        var started = Assert.Single(_sut.GetTaskCompletions(note.Id));
        Assert.Equal(TaskCompletion.HashLine("☒ marcada sin registro"), started.Key);
        Assert.True(started.Value >= before.AddSeconds(-1));
    }

    [Fact]
    public void PruneExpiredCompletedTasksInActiveNotes_SkipsProtectedNotesAndKeepsTheirClocks()
    {
        // Una nota protegida se lee sin texto: podarla con ese texto vacío borraba sus relojes y sus
        // tareas vencidas ya no se borraban nunca.
        var note = _sut.Create("Secreta\r\n☒ tarea", "#F5E3B3", "primary");
        _sut.Protect(note.Id, "clave-segura-1");
        var hash = TaskCompletion.HashLine("☒ tarea");
        _sut.RecordTaskCompletion(note.Id, hash, DateTimeOffset.UtcNow - TimeSpan.FromDays(3));

        _sut.PruneExpiredCompletedTasksInActiveNotes(TimeSpan.FromDays(1), new HashSet<Guid>());

        Assert.True(_sut.GetTaskCompletions(note.Id).ContainsKey(hash));
        Assert.True(_sut.TryUnlock(note.Id, "clave-segura-1", out var unlocked));
        Assert.Equal("Secreta\r\n☒ tarea", unlocked!.Text);
    }

    [Fact]
    public void PruneExpiredCompletedTasksInActiveNotes_SkipsTheGivenNotes_AndPrunesTheRest()
    {
        // Las notas abiertas se podan desde su ventana: podarlas aquí dejaría la ventana con el
        // texto viejo, que volvería a guardarse encima.
        var open = _sut.Create("abierta\r\n☒ vencida", "#F5E3B3", "primary");
        var closed = _sut.Create("cerrada\r\n☒ vencida", "#F5E3B3", "primary");
        var hash = TaskCompletion.HashLine("☒ vencida");
        _sut.RecordTaskCompletion(open.Id, hash, DateTimeOffset.UtcNow - TimeSpan.FromDays(3));
        _sut.RecordTaskCompletion(closed.Id, hash, DateTimeOffset.UtcNow - TimeSpan.FromDays(3));

        int pruned = _sut.PruneExpiredCompletedTasksInActiveNotes(TimeSpan.FromDays(1), new HashSet<Guid> { open.Id });

        Assert.Equal(1, pruned);
        Assert.Equal("abierta\r\n☒ vencida", _sut.GetById(open.Id)!.Text);
        Assert.Equal("cerrada", _sut.GetById(closed.Id)!.Text);
    }
}
