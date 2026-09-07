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

    /// <summary>
    /// Las notas de un estado, en el orden del mazo: primero el orden manual del usuario
    /// (<c>NoteOrder</c>, ver <see cref="MoveNote"/>) y, para las que aún no lo tienen, por fecha de
    /// creación como siempre. El <c>LEFT JOIN</c> con <c>COALESCE</c> es lo que deja convivir a las
    /// dos sin tener que numerar nada por adelantado.
    /// </summary>
    public IReadOnlyList<Note> GetByState(NoteState state)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.Id, n.EncryptedText, n.Nonce, n.Tag, n.Color, n.CreatedAt, n.UpdatedAt, n.State, n.ScreenOrigin
            FROM Note n LEFT JOIN NoteOrder o ON o.NoteId = n.Id
            WHERE n.State = $state
            ORDER BY COALESCE(o.Position, 1e18), n.CreatedAt;
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
        command.CommandText = """
            DELETE FROM Note WHERE Id = $id;
            DELETE FROM NotePlacement WHERE NoteId = $id;
            """;
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
        command.CommandText = """
            DELETE FROM NotePlacement WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM Note WHERE State = $state AND UpdatedAt < $cutoff;
            """;
        command.Parameters.AddWithValue("$state", NoteState.Trashed.ToString());
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        return command.ExecuteNonQuery();
    }

    /// <summary>
    /// Guarda dónde y de qué tamaño estaba una nota en <paramref name="monitorKey"/> (el
    /// <c>MonitorInfo.DeviceName</c> del monitor donde estaba, no un identificador de la nota
    /// sola): una nota puede recordar una posición distinta por cada pantalla en la que se ha
    /// dejado alguna vez.
    /// </summary>
    public void SavePlacement(Guid noteId, string monitorKey, double left, double top, double width, double height)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO NotePlacement (NoteId, MonitorKey, Left, Top, Width, Height)
            VALUES ($noteId, $monitorKey, $left, $top, $width, $height);
            """;
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$monitorKey", monitorKey);
        command.Parameters.AddWithValue("$left", left);
        command.Parameters.AddWithValue("$top", top);
        command.Parameters.AddWithValue("$width", width);
        command.Parameters.AddWithValue("$height", height);
        command.ExecuteNonQuery();
    }

    public NotePlacement? GetPlacement(Guid noteId, string monitorKey)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NoteId, MonitorKey, Left, Top, Width, Height FROM NotePlacement
            WHERE NoteId = $noteId AND MonitorKey = $monitorKey;
            """;
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$monitorKey", monitorKey);

        using var reader = command.ExecuteReader();
        if (reader.Read())
        {
            return new NotePlacement(
                Guid.Parse((string)reader["NoteId"]),
                (string)reader["MonitorKey"],
                reader.GetDouble(reader.GetOrdinal("Left")),
                reader.GetDouble(reader.GetOrdinal("Top")),
                reader.GetDouble(reader.GetOrdinal("Width")),
                reader.GetDouble(reader.GetOrdinal("Height"))
            );
        }

        return null;
    }

    /// <summary>
    /// Mueve <paramref name="noteId"/> al índice <paramref name="targetIndex"/> dentro de
    /// <paramref name="currentOrder"/> (la lista tal y como se ve ahora mismo en el mazo, incluida
    /// la nota que se mueve).
    ///
    /// Escribe **una sola fila** en el caso normal: la posición nueva es el punto medio entre las
    /// dos vecinas de destino (ver <see cref="NoteOrdering.Between"/>). Solo cuando ese hueco se ha
    /// agotado —partir el intervalo por la mitad muchísimas veces seguidas en el mismo sitio— se
    /// renumera la lista entera y se reintenta; es barato y no pasa en la práctica, pero dejarlo sin
    /// cubrir significaría que a partir de cierto momento arrastrar deja de hacer nada.
    /// </summary>
    public void MoveNote(Guid noteId, int targetIndex, IReadOnlyList<Guid> currentOrder)
    {
        var reordered = currentOrder.Where(id => id != noteId).ToList();
        targetIndex = Math.Clamp(targetIndex, 0, reordered.Count);
        reordered.Insert(targetIndex, noteId);

        var positions = GetOrder();

        // Basta con que las vecinas estén numeradas; la que se mueve va a recibir posición nueva de
        // todas formas. Si alguna no lo está —notas creadas antes de que existiera el orden manual—
        // se numera la lista entera: es la primera vez que se arrastra y deja todo consistente.
        if (!reordered.Where(id => id != noteId).All(positions.ContainsKey))
        {
            SetOrderPositions(reordered);
            return;
        }

        double? PositionOf(int index) =>
            index >= 0 && index < reordered.Count && positions.TryGetValue(reordered[index], out var p)
                ? p
                : null;

        var target = NoteOrdering.Between(PositionOf(targetIndex - 1), PositionOf(targetIndex + 1));
        if (target is null)
        {
            // El hueco se ha agotado de tanto partirlo por la mitad en el mismo sitio.
            SetOrderPositions(reordered);
            return;
        }

        SetOrder(noteId, target.Value);
    }

    /// <summary>Posiciones limpias y espaciadas para toda la lista, en el orden dado.</summary>
    private void SetOrderPositions(IReadOnlyList<Guid> orderedIds)
    {
        var positions = NoteOrdering.Spaced(orderedIds.Count);

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        for (int i = 0; i < orderedIds.Count; i++)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR REPLACE INTO NoteOrder (NoteId, Position) VALUES ($noteId, $position);
                """;
            command.Parameters.AddWithValue("$noteId", orderedIds[i].ToString());
            command.Parameters.AddWithValue("$position", positions[i]);
            command.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    public IReadOnlyDictionary<Guid, double> GetOrder()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NoteId, Position FROM NoteOrder;";

        var results = new Dictionary<Guid, double>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[Guid.Parse((string)reader["NoteId"])] = reader.GetDouble(reader.GetOrdinal("Position"));
        }
        return results;
    }

    public void SetOrder(Guid noteId, double position)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR REPLACE INTO NoteOrder (NoteId, Position) VALUES ($noteId, $position);";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$position", position);
        command.ExecuteNonQuery();
    }

    public void DeletePlacement(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM NotePlacement WHERE NoteId = $noteId;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
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
