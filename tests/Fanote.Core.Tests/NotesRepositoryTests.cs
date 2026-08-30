using Fanote.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fanote.Core.Tests;

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
