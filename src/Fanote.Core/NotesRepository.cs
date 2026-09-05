using Microsoft.Data.Sqlite;

namespace Fanote.Core;

public sealed class NotesRepository
{
    // No manual "permanently delete" action exists in the UI — trash empties itself after this
    // many days instead. Based on UpdatedAt (there's no dedicated "trashed at" column, and adding
    // one would need a schema migration this app doesn't have yet — see PurgeExpiredTrash).
    public const int DefaultTrashRetentionDays = 30;

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

    public void SetColor(Guid id, string color)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Note SET Color = $color, UpdatedAt = $updatedAt WHERE Id = $id;";
        command.Parameters.AddWithValue("$color", color);
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

    /// <summary>
    /// Permanently deletes a note, whatever its state. Returns whether a row was actually removed.
    ///
    /// Deliberately not exposed anywhere except the trash view: skipping the bin would remove the
    /// safety net that makes SetState(Trashed) a forgiving action rather than a destructive one.
    /// The 30-day purge is the automatic path; this is the manual one, for when you do not want to
    /// wait a month for something to actually be gone.
    /// </summary>
    public bool Delete(Guid id)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Note WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", id.ToString());
        return command.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Permanently deletes trashed notes whose <c>UpdatedAt</c> is older than <paramref name="retention"/>.
    /// Never touches Active or Archived notes. Returns the number of notes removed.
    /// </summary>
    public int PurgeExpiredTrash(TimeSpan retention)
    {
        var cutoff = DateTimeOffset.UtcNow - retention;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Note WHERE State = $state AND UpdatedAt < $cutoff;";
        command.Parameters.AddWithValue("$state", NoteState.Trashed.ToString());
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        return command.ExecuteNonQuery();
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
