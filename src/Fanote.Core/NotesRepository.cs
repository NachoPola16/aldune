using Microsoft.Data.Sqlite;

namespace Fanote.Core;

public sealed class NotesRepository
{
    private readonly NotesDatabase _database;
    private readonly ContentCipher _cipher;

    public NotesRepository(NotesDatabase database, ContentCipher cipher)
    {
        _database = database;
        _cipher = cipher;
    }

    public Note Create(string initialText, string color, string screenOrigin)
    {
        var now = DateTimeOffset.UtcNow;
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Text = initialText,
            Color = color,
            CreatedAt = now,
            UpdatedAt = now,
            State = NoteState.Active,
            ScreenOrigin = screenOrigin
        };

        var encrypted = _cipher.Encrypt(note.Text);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Note (Id, EncryptedText, Nonce, Tag, Color, CreatedAt, UpdatedAt, State, ScreenOrigin)
            VALUES ($id, $text, $nonce, $tag, $color, $createdAt, $updatedAt, $state, $screenOrigin);
            """;
        command.Parameters.AddWithValue("$id", note.Id.ToString());
        command.Parameters.AddWithValue("$text", encrypted.CipherText);
        command.Parameters.AddWithValue("$nonce", encrypted.Nonce);
        command.Parameters.AddWithValue("$tag", encrypted.Tag);
        command.Parameters.AddWithValue("$color", note.Color);
        command.Parameters.AddWithValue("$createdAt", note.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", note.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$state", note.State.ToString());
        command.Parameters.AddWithValue("$screenOrigin", note.ScreenOrigin);
        command.ExecuteNonQuery();

        return note;
    }

    public IReadOnlyList<Note> GetByState(NoteState state)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, EncryptedText, Nonce, Tag, Color, CreatedAt, UpdatedAt, State, ScreenOrigin
            FROM Note WHERE State = $state ORDER BY CreatedAt;
            """;
        command.Parameters.AddWithValue("$state", state.ToString());

        var results = new List<Note>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(ReadNote(reader));
        }
        return results;
    }

    public void UpdateText(Guid id, string newText)
    {
        var encrypted = _cipher.Encrypt(newText);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Note SET EncryptedText = $text, Nonce = $nonce, Tag = $tag, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        command.Parameters.AddWithValue("$text", encrypted.CipherText);
        command.Parameters.AddWithValue("$nonce", encrypted.Nonce);
        command.Parameters.AddWithValue("$tag", encrypted.Tag);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    public void SetState(Guid id, NoteState state)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Note SET State = $state, UpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private Note ReadNote(SqliteDataReader reader)
    {
        var cipherText = (byte[])reader["EncryptedText"];
        var nonce = (byte[])reader["Nonce"];
        var tag = (byte[])reader["Tag"];
        var text = _cipher.Decrypt(new EncryptedContent(cipherText, nonce, tag));

        return new Note
        {
            Id = Guid.Parse((string)reader["Id"]),
            Text = text,
            Color = (string)reader["Color"],
            CreatedAt = DateTimeOffset.Parse(
                (string)reader["CreatedAt"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind),
            UpdatedAt = DateTimeOffset.Parse(
                (string)reader["UpdatedAt"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind),
            State = Enum.Parse<NoteState>((string)reader["State"]),
            ScreenOrigin = (string)reader["ScreenOrigin"]
        };
    }
}
