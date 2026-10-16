using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryPlacementTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"aldune-placement-test-{Guid.NewGuid()}.db");
    private readonly NotesRepository _sut;

    public NotesRepositoryPlacementTests()
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
    public void SavePlacement_ThenGetPlacement_RoundTrips()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");

        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 120, 80, 300, 320);

        var placement = _sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1");

        Assert.NotNull(placement);
        Assert.Equal(note.Id, placement!.NoteId);
        Assert.Equal("\\\\.\\DISPLAY1", placement.MonitorKey);
        Assert.Equal(120, placement.Left);
        Assert.Equal(80, placement.Top);
        Assert.Equal(300, placement.Width);
        Assert.Equal(320, placement.Height);
    }

    [Fact]
    public void SavePlacement_WithoutTheFlag_IsNotAManualHeight()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 120, 80, 300, 320);

        Assert.False(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1")!.ManualHeight);
    }

    [Fact]
    public void SavePlacement_RemembersWhetherTheHeightWasSetByHand()
    {
        // Una nota encogida a mano por debajo de su texto no se distingue por el alto solo de una
        // que el ajuste automático dejó así: hace falta la marca para no devolverla al alto del texto.
        var note = _sut.Create("hola", "#F5E3B3", "primary");
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 120, 80, 300, 200, manualHeight: true);

        Assert.True(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1")!.ManualHeight);

        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 120, 80, 300, 200, manualHeight: false);
        Assert.False(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1")!.ManualHeight);
    }

    [Fact]
    public void OpeningADatabaseWithoutTheColumn_KeepsThePlacements()
    {
        // Una base de datos de la 1.7.8 no tiene ManualHeight: se añade sin tirar lo guardado.
        var path = Path.Combine(Path.GetTempPath(), $"aldune-placement-old-{Guid.NewGuid()}.db");
        var id = Guid.NewGuid();
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();
            using (var old = new SqliteConnection(cs))
            {
                old.Open();
                using var create = old.CreateCommand();
                create.CommandText = """
                    CREATE TABLE NotePlacement (
                        NoteId TEXT NOT NULL, MonitorKey TEXT NOT NULL,
                        Left REAL NOT NULL, Top REAL NOT NULL, Width REAL NOT NULL, Height REAL NOT NULL,
                        PRIMARY KEY (NoteId, MonitorKey));
                    INSERT INTO NotePlacement VALUES ('%ID%', 'D1', 10, 20, 300, 400);
                    """.Replace("%ID%", id.ToString());
                create.ExecuteNonQuery();
            }

            var repo = new NotesRepository(new NotesDatabase(path), new ContentCipher(new byte[32]));
            var placement = repo.GetPlacement(id, "D1");

            Assert.NotNull(placement);
            Assert.Equal(400, placement!.Height);
            Assert.False(placement.ManualHeight);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void DeletePlacement_RemovesOnlyThatMonitorsRow()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");
        _sut.SavePlacement(note.Id, "A", 1, 2, 300, 320);
        _sut.SavePlacement(note.Id, "B", 3, 4, 300, 320);

        _sut.DeletePlacement(note.Id, "A");

        Assert.Null(_sut.GetPlacement(note.Id, "A"));
        Assert.NotNull(_sut.GetPlacement(note.Id, "B"));
    }

    [Fact]
    public void GetPlacement_ForADifferentMonitor_ReturnsNull()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 120, 80, 300, 320);

        Assert.Null(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY2"));
    }

    [Fact]
    public void SavePlacement_OnTwoMonitors_KeepsBothIndependently()
    {
        // El punto de guardar por pantalla: la misma nota puede tener un sitio distinto en cada
        // monitor, y guardar en uno no debe pisar lo que hay guardado en el otro.
        var note = _sut.Create("hola", "#F5E3B3", "primary");

        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 100, 100, 300, 320);
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY2", -500, 200, 300, 320);

        Assert.Equal(100, _sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1")!.Left);
        Assert.Equal(-500, _sut.GetPlacement(note.Id, "\\\\.\\DISPLAY2")!.Left);
    }

    [Fact]
    public void SavePlacement_CalledTwiceForTheSameMonitor_OverwritesTheFirst()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");

        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 100, 100, 300, 320);
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 200, 150, 300, 320);

        Assert.Equal(200, _sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1")!.Left);
    }

    [Fact]
    public void GetPlacement_WithNoneSaved_ReturnsNull()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");
        Assert.Null(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1"));
    }

    [Fact]
    public void Delete_RemovesPlacementsOnAllMonitors()
    {
        var note = _sut.Create("hola", "#F5E3B3", "primary");
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY1", 100, 100, 300, 320);
        _sut.SavePlacement(note.Id, "\\\\.\\DISPLAY2", -500, 200, 300, 320);

        _sut.Delete(note.Id);

        Assert.Null(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY1"));
        Assert.Null(_sut.GetPlacement(note.Id, "\\\\.\\DISPLAY2"));
    }
}
