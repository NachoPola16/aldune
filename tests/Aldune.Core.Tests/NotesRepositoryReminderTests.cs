using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryReminderTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-reminder-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryReminderTests()
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
    public void SetReminder_ThenGetReminder_RoundTrips()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        var dueAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.Zero);

        _sut.SetReminder(note.Id, dueAt);

        Assert.Equal(dueAt, _sut.GetReminder(note.Id));
    }

    [Fact]
    public void SetReminder_WithLocalOffset_StoresTheSameInstant()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        var dueAt = new DateTimeOffset(2026, 9, 15, 10, 0, 0, TimeSpan.FromHours(2)); // e.g. CEST

        _sut.SetReminder(note.Id, dueAt);

        Assert.Equal(dueAt, _sut.GetReminder(note.Id)); // DateTimeOffset equality compares the instant
    }

    [Fact]
    public void SetReminder_CalledTwice_ReplacesInsteadOfAccumulating()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddDays(1));

        var newer = DateTimeOffset.UtcNow.AddDays(2);
        _sut.SetReminder(note.Id, newer);

        Assert.Equal(newer, _sut.GetReminder(note.Id));
        Assert.Single(_sut.GetPendingReminders());
    }

    [Fact]
    public void GetReminder_WithNoneSet_ReturnsNull()
    {
        var note = _sut.Create("sin recordatorio", "#F5E3B3", "primary");
        Assert.Null(_sut.GetReminder(note.Id));
    }

    [Fact]
    public void ClearReminder_RemovesIt()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));

        _sut.ClearReminder(note.Id);

        Assert.Null(_sut.GetReminder(note.Id));
    }

    [Fact]
    public void GetDueReminders_ReturnsOnlyThoseAtOrBeforeNow()
    {
        var overdue = _sut.Create("vencida", "#F5E3B3", "primary");
        var future = _sut.Create("futura", "#F5E3B3", "primary");
        var now = DateTimeOffset.UtcNow;
        _sut.SetReminder(overdue.Id, now.AddMinutes(-5));
        _sut.SetReminder(future.Id, now.AddHours(1));

        var due = _sut.GetDueReminders(now);

        var dueId = Assert.Single(due).NoteId;
        Assert.Equal(overdue.Id, dueId);
    }

    [Fact]
    public void GetPendingReminders_ReturnsAllActiveOnes()
    {
        var a = _sut.Create("a", "#F5E3B3", "primary");
        var b = _sut.Create("b", "#F5E3B3", "primary");
        _sut.SetReminder(a.Id, DateTimeOffset.UtcNow.AddHours(1));
        _sut.SetReminder(b.Id, DateTimeOffset.UtcNow.AddDays(1));

        var pending = _sut.GetPendingReminders();

        Assert.Equal(2, pending.Count);
        Assert.True(pending.ContainsKey(a.Id));
        Assert.True(pending.ContainsKey(b.Id));
    }

    [Fact]
    public void Delete_RemovesTheReminderToo()
    {
        var note = _sut.Create("comprar pan", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));

        _sut.Delete(note.Id);

        Assert.Empty(_sut.GetPendingReminders());
    }

    [Fact]
    public void PurgeExpiredTrash_RemovesTheReminderOfThePurgedNote()
    {
        var note = _sut.Create("vieja", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));
        _sut.SetState(note.Id, NoteState.Trashed);
        BackdateUpdatedAt(note.Id, DateTimeOffset.UtcNow.AddDays(-31));

        _sut.PurgeExpiredTrash(TimeSpan.FromDays(30));

        Assert.Empty(_sut.GetPendingReminders());
    }

    // Mismo patrón que NotesRepositoryTests.BackdateUpdatedAt: PurgeExpiredTrash corta por
    // UpdatedAt, así que hay que poder ponerlo en el pasado sin esperar de verdad.
    private void BackdateUpdatedAt(Guid id, DateTimeOffset updatedAt)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Note SET UpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$updatedAt", updatedAt.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    [Fact]
    public void GetById_ReturnsTheDecryptedNote()
    {
        var note = _sut.Create("mi nota", "#F5E3B3", "primary");

        var found = _sut.GetById(note.Id);

        Assert.NotNull(found);
        Assert.Equal("mi nota", found!.Text);
    }

    [Fact]
    public void GetById_WithUnknownId_ReturnsNull()
    {
        Assert.Null(_sut.GetById(Guid.NewGuid()));
    }

    [Fact]
    public void GetDueReminders_ExcludesTrashedNotes()
    {
        var note = _sut.Create("nota archivada en la papelera", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddMinutes(-5));
        _sut.SetState(note.Id, NoteState.Trashed);

        var due = _sut.GetDueReminders(DateTimeOffset.UtcNow);

        Assert.Empty(due);
    }

    [Fact]
    public void GetDueReminders_ExcludesArchivedNotes()
    {
        var note = _sut.Create("nota archivada", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddMinutes(-5));
        _sut.SetState(note.Id, NoteState.Archived);

        var due = _sut.GetDueReminders(DateTimeOffset.UtcNow);

        Assert.Empty(due);
    }

    [Fact]
    public void GetPendingReminders_ExcludesTrashedNotes()
    {
        var note = _sut.Create("nota en la papelera", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));
        _sut.SetState(note.Id, NoteState.Trashed);

        var pending = _sut.GetPendingReminders();

        Assert.Empty(pending);
    }

    [Fact]
    public void GetPendingReminders_ExcludesArchivedNotes()
    {
        var note = _sut.Create("nota archivada", "#F5E3B3", "primary");
        _sut.SetReminder(note.Id, DateTimeOffset.UtcNow.AddHours(1));
        _sut.SetState(note.Id, NoteState.Archived);

        var pending = _sut.GetPendingReminders();

        Assert.Empty(pending);
    }
}
