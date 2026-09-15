using Aldune.Core;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Aldune.Core.Tests;

public class DatabaseCorruptionGuardTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"aldune-corrupt-test-{Guid.NewGuid()}.db");

    public void Dispose()
    {
        // Clean up the main test file and any backup files.
        foreach (var file in Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_path)}*"))
            File.Delete(file);
    }

    [Fact]
    public void IsValidSqliteFile_WhenFileDoesNotExist_ReturnsFalse()
    {
        Assert.False(DatabaseCorruptionGuard.IsValidSqliteFile(_path));
    }

    [Fact]
    public void IsValidSqliteFile_WhenFileIsGarbage_ReturnsFalse()
    {
        File.WriteAllText(_path, "esto no es una base de datos SQLite");
        Assert.False(DatabaseCorruptionGuard.IsValidSqliteFile(_path));
    }

    [Fact]
    public void IsValidSqliteFile_WhenFileIsRealSqliteDatabase_ReturnsTrue()
    {
        _ = new NotesDatabase(_path); // creates a real, valid SQLite file

        // Clear the connection pool to release the file handle before reading it.
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path }.ToString());
        SqliteConnection.ClearPool(connection);

        Assert.True(DatabaseCorruptionGuard.IsValidSqliteFile(_path));
    }

    [Fact]
    public void BackupAndRemove_MovesTheOriginalFileAside()
    {
        File.WriteAllText(_path, "datos corruptos");

        DatabaseCorruptionGuard.BackupAndRemove(_path);

        Assert.False(File.Exists(_path));
        var backups = Directory.GetFiles(Path.GetTempPath(), $"{Path.GetFileName(_path)}.corrupt-*");
        Assert.Single(backups);
        Assert.Equal("datos corruptos", File.ReadAllText(backups[0]));
    }
}
