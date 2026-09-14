using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-repo-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryTests()
    {
        var database = new NotesDatabase(_dbPath);
        var cipher = new ContentCipher(new byte[32]); // fixed all-zero test key
        _sut = new NotesRepository(database, cipher);
    }

    public void Dispose()
    {
        // Clear the SQLite connection pool to release file handles.
        // Microsoft.Data.Sqlite pools connections by default; Dispose() returns them to the pool
        // rather than closing them, so we must explicitly clear the pool before deleting the file.
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);

        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void Create_ThenGetByState_ReturnsTheNoteActive()
    {
        var created = _sut.Create("hola nota", "#F5E3B3", "primary");

        var active = _sut.GetByState(NoteState.Active);

        Assert.Single(active);
        Assert.Equal(created.Id, active[0].Id);
        Assert.Equal("hola nota", active[0].Text);
        Assert.Equal("#F5E3B3", active[0].Color);
        Assert.Equal(NoteState.Active, active[0].State);
        Assert.Equal("primary", active[0].ScreenOrigin);
    }

    [Fact]
    public void Create_EncryptsTextAtRest()
    {
        _sut.Create("texto secreto", "#FFFFFF", "primary");

        var database = new NotesDatabase(_dbPath);
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EncryptedText FROM Note;";
        var storedBytes = (byte[])command.ExecuteScalar()!;
        var storedAsText = System.Text.Encoding.UTF8.GetString(storedBytes);

        Assert.DoesNotContain("texto secreto", storedAsText);
    }

    [Fact]
    public void UpdateText_ChangesTextAndUpdatedAt()
    {
        var created = _sut.Create("texto original", "#FFFFFF", "primary");
        var originalUpdatedAt = created.UpdatedAt;

        _sut.UpdateText(created.Id, "texto editado");

        var reloaded = _sut.GetByState(NoteState.Active)[0];
        Assert.Equal("texto editado", reloaded.Text);
        Assert.True(reloaded.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void ProtectedNote_HidesTextUntilCorrectPassword()
    {
        var created = _sut.Create("Título secreto\r\nContenido privado", "#FFFFFF", "primary");

        _sut.Protect(created.Id, "clave-segura");

        var locked = _sut.GetById(created.Id)!;
        Assert.True(locked.IsProtected);
        Assert.False(locked.IsUnlocked);
        Assert.Empty(locked.Text);
        Assert.False(_sut.TryUnlock(created.Id, "incorrecta", out _));
        Assert.True(_sut.TryUnlock(created.Id, "clave-segura", out var unlocked));
        Assert.Equal("Título secreto\r\nContenido privado", unlocked!.Text);
    }

    [Fact]
    public void ProtectedNote_CanBeEditedAndProtectionRemoved()
    {
        var created = _sut.Create("uno", "#FFFFFF", "primary");
        _sut.Protect(created.Id, "clave-segura");

        _sut.UpdateProtectedText(created.Id, "dos", "clave-segura");
        Assert.True(_sut.TryUnlock(created.Id, "clave-segura", out var unlocked));
        Assert.Equal("dos", unlocked!.Text);

        Assert.True(_sut.RemoveProtection(created.Id, "clave-segura"));
        var plain = _sut.GetById(created.Id)!;
        Assert.False(plain.IsProtected);
        Assert.Equal("dos", plain.Text);
    }

    [Fact]
    public void SetState_MovesNoteBetweenStateQueries()
    {
        var created = _sut.Create("nota a archivar", "#FFFFFF", "primary");

        _sut.SetState(created.Id, NoteState.Archived);

        Assert.Empty(_sut.GetByState(NoteState.Active));
        var archived = _sut.GetByState(NoteState.Archived);
        Assert.Single(archived);
        Assert.Equal(created.Id, archived[0].Id);
    }

    [Fact]
    public void SetState_ToTrashed_ThenBackToActive_RestoresIt()
    {
        var created = _sut.Create("nota a borrar y restaurar", "#FFFFFF", "primary");

        _sut.SetState(created.Id, NoteState.Trashed);
        Assert.Single(_sut.GetByState(NoteState.Trashed));

        _sut.SetState(created.Id, NoteState.Active);
        Assert.Single(_sut.GetByState(NoteState.Active));
        Assert.Empty(_sut.GetByState(NoteState.Trashed));
    }

    [Fact]
    public void SetColor_ChangesColorAndUpdatedAt()
    {
        var created = _sut.Create("nota a recolorear", "#FFFFFF", "primary");
        var originalUpdatedAt = created.UpdatedAt;

        _sut.SetColor(created.Id, "#C9E4DE");

        var reloaded = _sut.GetByState(NoteState.Active)[0];
        Assert.Equal("#C9E4DE", reloaded.Color);
        Assert.True(reloaded.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void PurgeExpiredTrash_RemovesOnlyTrashedNotesOlderThanRetention()
    {
        var oldTrashed = _sut.Create("vieja en papelera", "#FFFFFF", "primary");
        _sut.SetState(oldTrashed.Id, NoteState.Trashed);
        BackdateUpdatedAt(oldTrashed.Id, DateTimeOffset.UtcNow.AddDays(-31));

        var recentTrashed = _sut.Create("reciente en papelera", "#FFFFFF", "primary");
        _sut.SetState(recentTrashed.Id, NoteState.Trashed);

        var oldArchived = _sut.Create("vieja archivada", "#FFFFFF", "primary");
        _sut.SetState(oldArchived.Id, NoteState.Archived);
        BackdateUpdatedAt(oldArchived.Id, DateTimeOffset.UtcNow.AddDays(-100));

        var purgedCount = _sut.PurgeExpiredTrash(TimeSpan.FromDays(30));

        Assert.Equal(1, purgedCount);
        Assert.DoesNotContain(_sut.GetByState(NoteState.Trashed), n => n.Id == oldTrashed.Id);
        Assert.Contains(_sut.GetByState(NoteState.Trashed), n => n.Id == recentTrashed.Id);
        Assert.Contains(_sut.GetByState(NoteState.Archived), n => n.Id == oldArchived.Id);
    }

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
    public void GetByState_OrdersByCreatedAt()
    {
        var first = _sut.Create("primera", "#FFFFFF", "primary");
        System.Threading.Thread.Sleep(10); // ensure a distinct timestamp
        var second = _sut.Create("segunda", "#FFFFFF", "primary");

        var active = _sut.GetByState(NoteState.Active);

        Assert.Equal(first.Id, active[0].Id);
        Assert.Equal(second.Id, active[1].Id);
    }
}
