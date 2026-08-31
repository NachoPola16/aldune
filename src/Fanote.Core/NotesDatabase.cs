using Microsoft.Data.Sqlite;

namespace Fanote.Core;

public sealed class NotesDatabase
{
    private readonly string _connectionString;

    public NotesDatabase(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
        Initialize();
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Initialize()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Note (
                Id TEXT PRIMARY KEY NOT NULL,
                EncryptedText BLOB NOT NULL,
                Nonce BLOB NOT NULL,
                Tag BLOB NOT NULL,
                Color TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                State TEXT NOT NULL,
                ScreenOrigin TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }
}
