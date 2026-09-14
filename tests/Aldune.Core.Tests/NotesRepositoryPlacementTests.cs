using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesRepositoryPlacementTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-placement-test-{Guid.NewGuid()}.db");
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
