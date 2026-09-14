using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryOrderTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-order-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryOrderTests()
    {
        var database = new NotesDatabase(_dbPath);
        var cipher = new ContentCipher(new byte[32]);
        _sut = new NotesRepository(database, cipher);
    }

    public void Dispose()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString());
        SqliteConnection.ClearPool(connection);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private List<Note> CreateNotes(params string[] texts) =>
        texts.Select(t => _sut.Create(t, "#EBD38B", "primary")).ToList();

    private List<string> CurrentOrder() =>
        _sut.GetByState(NoteState.Active).Select(n => n.Text).ToList();

    private List<Guid> CurrentIds() =>
        _sut.GetByState(NoteState.Active).Select(n => n.Id).ToList();

    [Fact]
    public void WithoutAnyManualOrder_NotesKeepCreationOrder()
    {
        CreateNotes("a", "b", "c");
        Assert.Equal(new[] { "a", "b", "c" }, CurrentOrder());
    }

    [Fact]
    public void MoveNote_ToTheTop_PutsItFirst()
    {
        var notes = CreateNotes("a", "b", "c");

        _sut.MoveNote(notes[2].Id, targetIndex: 0, CurrentIds());

        Assert.Equal(new[] { "c", "a", "b" }, CurrentOrder());
    }

    [Fact]
    public void MoveNote_ToTheBottom_PutsItLast()
    {
        var notes = CreateNotes("a", "b", "c");

        _sut.MoveNote(notes[0].Id, targetIndex: 2, CurrentIds());

        Assert.Equal(new[] { "b", "c", "a" }, CurrentOrder());
    }

    [Fact]
    public void MoveNote_IntoTheMiddle_LandsBetweenItsNeighbours()
    {
        var notes = CreateNotes("a", "b", "c", "d");

        _sut.MoveNote(notes[3].Id, targetIndex: 1, CurrentIds());

        Assert.Equal(new[] { "a", "d", "b", "c" }, CurrentOrder());
    }

    [Fact]
    public void MoveNote_ToItsOwnPlace_ChangesNothing()
    {
        var notes = CreateNotes("a", "b", "c");

        _sut.MoveNote(notes[1].Id, targetIndex: 1, CurrentIds());

        Assert.Equal(new[] { "a", "b", "c" }, CurrentOrder());
    }

    [Fact]
    public void MoveNote_SurvivesManyMovesIntoTheSameGap()
    {
        // El caso que agota el hueco: siempre al mismo sitio, partiendo el intervalo por la mitad.
        // El repositorio tiene que renumerar solo y seguir funcionando, no dejar de mover.
        var notes = CreateNotes("a", "b", "c", "d", "e");

        for (int i = 0; i < 60; i++)
        {
            var ids = CurrentIds();
            _sut.MoveNote(ids[^1], targetIndex: 1, ids);
        }

        var order = CurrentOrder();
        Assert.Equal(5, order.Count);
        Assert.Equal(5, order.Distinct().Count()); // ninguna se ha perdido ni duplicado
        Assert.Equal("a", order[0]);
    }

    [Fact]
    public void MoveNote_ThenCreatingANewOne_LeavesTheNewOneLast()
    {
        // Una nota nueva no tiene orden manual, y COALESCE la manda al final: es donde se espera que
        // aparezca al crearla.
        var notes = CreateNotes("a", "b");
        _sut.MoveNote(notes[1].Id, targetIndex: 0, CurrentIds());

        _sut.Create("nueva", "#EBD38B", "primary");

        Assert.Equal(new[] { "b", "a", "nueva" }, CurrentOrder());
    }

    [Fact]
    public void MoveNote_KeepsWorking_WhenSomeNotesHaveNoOrderYet()
    {
        // Mezcla: unas ya numeradas por un arrastre anterior y otras no. No puede perder ninguna.
        var notes = CreateNotes("a", "b", "c");
        _sut.MoveNote(notes[2].Id, targetIndex: 0, CurrentIds());

        _sut.Create("d", "#EBD38B", "primary");
        var ids = CurrentIds();
        _sut.MoveNote(ids[^1], targetIndex: 1, ids);

        var order = CurrentOrder();
        Assert.Equal(4, order.Count);
        Assert.Equal("d", order[1]);
    }

    [Fact]
    public void SetOrder_ThenGetOrder_RoundTrips()
    {
        var notes = CreateNotes("a");
        _sut.SetOrder(notes[0].Id, 1234.5);

        Assert.Equal(1234.5, _sut.GetOrder()[notes[0].Id]);
    }
}
