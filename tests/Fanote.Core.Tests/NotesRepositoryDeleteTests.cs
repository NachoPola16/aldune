using Fanote.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Fanote.Core.Tests;

/// <summary>
/// El borrado permanente es la unica accion sin vuelta atras de la app, asi que conviene que este
/// atada: que borre lo que dice, que no se lleve nada mas por delante, y que no explote si la nota
/// ya no esta.
/// </summary>
public class NotesRepositoryDeleteTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"fanote-delete-{Guid.NewGuid():N}.db");
    private readonly NotesRepository _repository;

    public NotesRepositoryDeleteTests()
    {
        var database = new NotesDatabase(_databasePath);
        _repository = new NotesRepository(database, new ContentCipher(new byte[32]));
    }

    [Fact]
    public void Delete_RemovesTheNote()
    {
        var note = _repository.Create("una nota", "#EBD38B", "primary");
        _repository.SetState(note.Id, NoteState.Trashed);

        Assert.True(_repository.Delete(note.Id));
        Assert.Empty(_repository.GetByState(NoteState.Trashed));
    }

    [Fact]
    public void Delete_LeavesEveryOtherNoteAlone()
    {
        var doomed = _repository.Create("la que se va", "#EBD38B", "primary");
        var active = _repository.Create("activa", "#AAE6B1", "primary");
        var archived = _repository.Create("archivada", "#83E7F2", "primary");
        _repository.SetState(archived.Id, NoteState.Archived);

        _repository.Delete(doomed.Id);

        Assert.Single(_repository.GetByState(NoteState.Active));
        Assert.Equal(active.Id, _repository.GetByState(NoteState.Active)[0].Id);
        Assert.Single(_repository.GetByState(NoteState.Archived));
    }

    [Fact]
    public void Delete_OnAMissingNote_ReportsThatNothingWasRemoved()
    {
        // Puede pasar de verdad: dos ventanas del gestor abiertas, o la purga automatica de los 30
        // dias llevandose la nota justo antes. No debe lanzar.
        Assert.False(_repository.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Delete_IsIdempotent()
    {
        var note = _repository.Create("dos veces", "#EBD38B", "primary");

        Assert.True(_repository.Delete(note.Id));
        Assert.False(_repository.Delete(note.Id));
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite agrupa conexiones: Dispose las devuelve al pool en vez de
        // cerrarlas, asi que hay que vaciarlo antes de poder borrar el fichero.
        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString());
        SqliteConnection.ClearPool(connection);

        if (File.Exists(_databasePath)) File.Delete(_databasePath);
    }
}
