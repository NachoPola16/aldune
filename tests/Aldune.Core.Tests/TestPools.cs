using Microsoft.Data.Sqlite;

namespace Aldune.Core.Tests;

/// <summary>
/// Releases the pooled SQLite connections of one test's databases so Windows lets the temp files go. Never use
/// <c>SqliteConnection.ClearAllPools()</c> in a test: xUnit runs classes in parallel and it cuts the connection another class is in the
/// middle of using ("Cannot access a disposed object: SQLitePCL.sqlite3"), which made random tests fail on a full run.
/// </summary>
internal static class TestPools
{
    public static void ReleaseFile(string database)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database }.ToString());
        SqliteConnection.ClearPool(connection);
    }

    /// <summary>Every file under a folder: clearing the pool of a file that is not a database is a no-op.</summary>
    public static void ReleaseDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)) ReleaseFile(file);
    }
}
