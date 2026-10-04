using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

// "Siempre encima" por nota que sobrevive a cerrar la nota y la app. Por defecto toda nota va fijada; lo que se
// guarda es la excepción (las que el usuario soltó), en una tabla propia: la tabla Note tiene datos reales y no
// hay migraciones.
public class NotePinTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"aldune-pin-{Guid.NewGuid():N}");
    private readonly string _dbPath;
    private readonly NotesRepository _repository;

    public NotePinTests()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "notes.db");
        _repository = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        Directory.Delete(_dir, recursive: true);
    }

    [Fact]
    public void ANewNote_IsPinned() =>
        Assert.False(_repository.IsUnpinned(_repository.Create("x", "#EBD38B", "primary").Id));

    [Fact]
    public void UnpinningAndPinningAgain_IsRemembered()
    {
        var id = _repository.Create("x", "#EBD38B", "primary").Id;

        _repository.SetUnpinned(id, true);
        Assert.True(_repository.IsUnpinned(id));

        _repository.SetUnpinned(id, false);
        Assert.False(_repository.IsUnpinned(id));
    }

    [Fact]
    public void Unpinning_SurvivesReopeningTheDatabase()
    {
        var id = _repository.Create("x", "#EBD38B", "primary").Id;
        _repository.SetUnpinned(id, true);

        var reopened = new NotesRepository(new NotesDatabase(_dbPath), new ContentCipher(new byte[32]));

        Assert.True(reopened.IsUnpinned(id));
    }

    [Fact]
    public void DeletingTheNote_ForgetsIt()
    {
        var id = _repository.Create("x", "#EBD38B", "primary").Id;
        _repository.SetUnpinned(id, true);

        Assert.True(_repository.Delete(id));

        Assert.False(_repository.IsUnpinned(id));
    }

    [Fact]
    public void Unpinning_DoesNotTouchTheNoteItself()
    {
        var note = _repository.Create("x", "#EBD38B", "primary");
        _repository.SetUnpinned(note.Id, true);

        Assert.Equal(note.UpdatedAt, _repository.GetById(note.Id)!.UpdatedAt);
    }
}
