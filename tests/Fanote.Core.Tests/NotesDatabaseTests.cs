using Fanote.Core;
using Xunit;

namespace Fanote.Core.Tests;

public class NotesDatabaseTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-test-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); }
            catch { /* Ignore file deletion errors */ }
        }
    }

    [Fact]
    public void Constructor_CreatesNoteTable()
    {
        var sut = new NotesDatabase(_dbPath);

        using var connection = sut.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Note';";
        var tableName = command.ExecuteScalar() as string;

        Assert.Equal("Note", tableName);
    }

    [Fact]
    public void Constructor_IsIdempotent_DoesNotThrowIfCalledTwiceOnSamePath()
    {
        _ = new NotesDatabase(_dbPath);
        var second = new NotesDatabase(_dbPath); // must not throw "table already exists"

        using var connection = second.OpenConnection();
        Assert.NotNull(connection);
    }

    [Fact]
    public void OpenConnection_ReturnsAlreadyOpenConnection()
    {
        var sut = new NotesDatabase(_dbPath);
        using var connection = sut.OpenConnection();
        Assert.Equal(System.Data.ConnectionState.Open, connection.State);
    }
}
