using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class NotesDatabaseTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fanote-test-{Guid.NewGuid()}.db");

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

    [Fact]
    public void Constructor_RecreatesNotePlacementTable_WhenItPredatesMonitorKey()
    {
        // Reproduce una base de datos real creada antes de que la posición recordada pasara a ser
        // por pantalla: NotePlacement existe, pero sin la columna MonitorKey.
        using (var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = _dbPath }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE NotePlacement (
                    NoteId TEXT PRIMARY KEY NOT NULL,
                    Left REAL NOT NULL,
                    Top REAL NOT NULL,
                    Width REAL NOT NULL,
                    Height REAL NOT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        var sut = new NotesDatabase(_dbPath);

        using var check = sut.OpenConnection();
        using var pragma = check.CreateCommand();
        pragma.CommandText = "SELECT COUNT(*) FROM pragma_table_info('NotePlacement') WHERE name = 'MonitorKey';";
        Assert.Equal(1L, (long)pragma.ExecuteScalar()!);
    }
}
