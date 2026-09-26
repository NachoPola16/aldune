using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Aldune.Core;

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
            , n.IsProtected, n.ProtectionSalt, n.ProtectionCipherText, n.ProtectionNonce, n.ProtectionTag
            , o.Position AS DockPosition
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
        HydrateTags(results);
        return results;
    }

    /// <summary>Todas las notas actuales, incluidos archivadas y papelera, para exportación/sync.</summary>
    public IReadOnlyList<Note> GetAllForSync()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.Id, n.EncryptedText, n.Nonce, n.Tag, n.Color, n.CreatedAt, n.UpdatedAt, n.State, n.ScreenOrigin
            , n.IsProtected, n.ProtectionSalt, n.ProtectionCipherText, n.ProtectionNonce, n.ProtectionTag
            , o.Position AS DockPosition
            FROM Note n LEFT JOIN NoteOrder o ON o.NoteId = n.Id ORDER BY n.CreatedAt;
            """;

        var results = new List<Note>();
        using var reader = command.ExecuteReader();
        while (reader.Read()) results.Add(ReadNote(reader));
        HydrateTags(results);
        return results;
    }

    public IReadOnlyList<string> GetAllTags()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM Tag ORDER BY Name COLLATE NOCASE;";
        using var reader = command.ExecuteReader();
        var result = new List<string>();
        while (reader.Read()) result.Add((string)reader["Name"]);
        return result;
    }

    public bool CreateTag(string name)
    {
        var normalized = name.Trim();
        if (normalized.Length == 0) return false;

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Tag (Id, Name) VALUES ($id, $name);";
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$name", normalized);
        return command.ExecuteNonQuery() > 0;
    }

    public bool DeleteTag(string name)
    {
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var deleteLinks = connection.CreateCommand();
        deleteLinks.Transaction = transaction;
        // Las notas que la llevaban cambian: se les actualiza la fecha para que la sincronización
        // lleve el cambio al resto de dispositivos (si no, allí conservaban la etiqueta borrada).
        deleteLinks.CommandText = """
            UPDATE Note SET UpdatedAt = $now
            WHERE Id IN (SELECT NoteId FROM NoteTag
                         WHERE TagId IN (SELECT Id FROM Tag WHERE Name = $name COLLATE NOCASE));
            DELETE FROM NoteTag
            WHERE TagId IN (SELECT Id FROM Tag WHERE Name = $name COLLATE NOCASE);
            """;
        deleteLinks.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        deleteLinks.Parameters.AddWithValue("$name", name.Trim());
        deleteLinks.ExecuteNonQuery();

        using var deleteTag = connection.CreateCommand();
        deleteTag.Transaction = transaction;
        deleteTag.CommandText = "DELETE FROM Tag WHERE Name = $name COLLATE NOCASE;";
        deleteTag.Parameters.AddWithValue("$name", name.Trim());
        bool deleted = deleteTag.ExecuteNonQuery() > 0;
        transaction.Commit();
        return deleted;
    }

    /// <summary>
    /// Las etiquetas de una nota, cambiadas por el usuario: actualiza su fecha para que el cambio se
    /// sincronice. Antes no lo hacía y cambiar solo las etiquetas nunca llegaba a los otros equipos.
    /// </summary>
    public void SetTags(Guid noteId, IEnumerable<string> tags) => SetTags(noteId, tags, touch: true);

    /// <summary>
    /// <paramref name="touch"/> es false al aplicar una nota que llega por sincronización: esa nota ya
    /// trae su fecha, y cambiarla haría que los dispositivos se la pasaran de uno a otro sin fin.
    /// </summary>
    private void SetTags(Guid noteId, IEnumerable<string> tags, bool touch)
    {
        var normalized = tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToList();

        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM NoteTag WHERE NoteId = $noteId;";
            delete.Parameters.AddWithValue("$noteId", noteId.ToString());
            delete.ExecuteNonQuery();
        }

        foreach (var name in normalized)
        {
            using var add = connection.CreateCommand();
            add.Transaction = transaction;
            add.CommandText = """
                INSERT OR IGNORE INTO Tag (Id, Name) VALUES ($tagId, $name);
                INSERT OR IGNORE INTO NoteTag (NoteId, TagId)
                SELECT $noteId, Id FROM Tag WHERE Name = $name COLLATE NOCASE;
                """;
            add.Parameters.AddWithValue("$tagId", Guid.NewGuid().ToString());
            add.Parameters.AddWithValue("$name", name);
            add.Parameters.AddWithValue("$noteId", noteId.ToString());
            add.ExecuteNonQuery();
        }

        if (touch)
        {
            using var stamp = connection.CreateCommand();
            stamp.Transaction = transaction;
            stamp.CommandText = "UPDATE Note SET UpdatedAt = $now WHERE Id = $noteId;";
            stamp.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            stamp.Parameters.AddWithValue("$noteId", noteId.ToString());
            stamp.ExecuteNonQuery();
        }

        // Sin limpiar las etiquetas que se quedan sin notas: se crean y se borran en el gestor, y una
        // creada para usarla más adelante desaparecía en cuanto se guardaban las de cualquier nota.
        transaction.Commit();
    }

    public IReadOnlyList<Note> GetByTag(string tag, NoteState state)
    {
        var notes = new List<Note>();
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.Id, n.EncryptedText, n.Nonce, n.Tag, n.Color, n.CreatedAt, n.UpdatedAt, n.State, n.ScreenOrigin
            , n.IsProtected, n.ProtectionSalt, n.ProtectionCipherText, n.ProtectionNonce, n.ProtectionTag
            , o.Position AS DockPosition
            FROM Note n
            JOIN NoteTag nt ON nt.NoteId = n.Id
            JOIN Tag t ON t.Id = nt.TagId
            LEFT JOIN NoteOrder o ON o.NoteId = n.Id
            WHERE n.State = $state AND t.Name = $tag COLLATE NOCASE
            ORDER BY COALESCE(o.Position, 1e18), n.CreatedAt;
            """;
        command.Parameters.AddWithValue("$state", state.ToString());
        command.Parameters.AddWithValue("$tag", tag);
        using var reader = command.ExecuteReader();
        while (reader.Read()) notes.Add(ReadNote(reader));
        HydrateTags(notes);
        return notes;
    }

    private void HydrateTags(IReadOnlyList<Note> notes)
    {
        if (notes.Count == 0) return;
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT nt.NoteId, t.Name FROM NoteTag nt JOIN Tag t ON t.Id = nt.TagId;";
        var byId = notes.ToDictionary(note => note.Id, _ => new List<string>());
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (byId.TryGetValue(Guid.Parse((string)reader["NoteId"]), out var tags))
                tags.Add((string)reader["Name"]);
        }
        foreach (var note in notes) note.Tags = byId[note.Id];
    }

    public IReadOnlyList<SyncTombstone> GetSyncTombstones()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT NoteId, DeletedAt, DeviceId FROM SyncTombstone;";

        var results = new List<SyncTombstone>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add(new SyncTombstone(
                Guid.Parse((string)reader["NoteId"]),
                DateTimeOffset.Parse((string)reader["DeletedAt"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind),
                (string)reader["DeviceId"]));
        }
        return results;
    }

    public IReadOnlyList<SyncConflict> GetSyncConflicts()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, NoteId, OccurredAt, WinnerUpdatedAt, WinnerDeviceId,
                   LosingUpdatedAt, LosingDeviceId, LosingTombstone,
                   EncryptedNote, NoteNonce, NoteTag
            FROM SyncConflict ORDER BY OccurredAt DESC;
            """;

        var results = new List<SyncConflict>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Note? losingNote = null;
            if (reader["EncryptedNote"] is byte[] encrypted &&
                reader["NoteNonce"] is byte[] nonce &&
                reader["NoteTag"] is byte[] tag)
            {
                var json = _cipher.Decrypt(new EncryptedContent(encrypted, nonce, tag));
                losingNote = JsonSerializer.Deserialize<Note>(json);
            }

            results.Add(new SyncConflict(
                Guid.Parse((string)reader["Id"]),
                Guid.Parse((string)reader["NoteId"]),
                ParseDate(reader["OccurredAt"]),
                new SyncConflictVersion(ParseDate(reader["WinnerUpdatedAt"]),
                    (string)reader["WinnerDeviceId"], false, null),
                new SyncConflictVersion(ParseDate(reader["LosingUpdatedAt"]),
                    (string)reader["LosingDeviceId"], Convert.ToInt32(reader["LosingTombstone"]) != 0,
                    losingNote)));
        }

        return results;
    }

    public void SaveSyncConflict(SyncConflict conflict)
    {
        EncryptedContent? encryptedNote = null;
        if (conflict.Losing.Note is not null)
        {
            var json = JsonSerializer.Serialize(conflict.Losing.Note);
            encryptedNote = _cipher.Encrypt(json);
        }

        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM SyncConflict WHERE NoteId = $noteId;
            INSERT OR REPLACE INTO SyncConflict
                (Id, NoteId, OccurredAt, WinnerUpdatedAt, WinnerDeviceId,
                 LosingUpdatedAt, LosingDeviceId, LosingTombstone,
                 EncryptedNote, NoteNonce, NoteTag)
            VALUES ($id, $noteId, $occurredAt, $winnerUpdatedAt, $winnerDeviceId,
                    $losingUpdatedAt, $losingDeviceId, $losingTombstone,
                    $encryptedNote, $noteNonce, $noteTag);
            """;
        command.Parameters.AddWithValue("$id", conflict.Id.ToString());
        command.Parameters.AddWithValue("$noteId", conflict.NoteId.ToString());
        command.Parameters.AddWithValue("$occurredAt", conflict.OccurredAt.ToString("O"));
        command.Parameters.AddWithValue("$winnerUpdatedAt", conflict.Winner.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$winnerDeviceId", conflict.Winner.DeviceId);
        command.Parameters.AddWithValue("$losingUpdatedAt", conflict.Losing.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$losingDeviceId", conflict.Losing.DeviceId);
        command.Parameters.AddWithValue("$losingTombstone", conflict.Losing.Tombstone ? 1 : 0);
        command.Parameters.AddWithValue("$encryptedNote", (object?)encryptedNote?.CipherText ?? DBNull.Value);
        command.Parameters.AddWithValue("$noteNonce", (object?)encryptedNote?.Nonce ?? DBNull.Value);
        command.Parameters.AddWithValue("$noteTag", (object?)encryptedNote?.Tag ?? DBNull.Value);
        command.ExecuteNonQuery();
    }

    public void DeleteSyncConflict(Guid conflictId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SyncConflict WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", conflictId.ToString());
        command.ExecuteNonQuery();
    }

    /// <summary>Descarta todos los conflictos de golpe y devuelve cuántos había. Es la operación de
    /// "descartar todo" de la ventana de conflictos: vacía la cola de recuperación entera sin tocar
    /// las notas — en cada una sigue siendo la versión ganadora la que está activa.</summary>
    public int DeleteAllSyncConflicts()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SyncConflict;";
        return command.ExecuteNonQuery();
    }

    private static DateTimeOffset ParseDate(object value) => DateTimeOffset.Parse(
        (string)value,
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.RoundtripKind);

    public void UpdateText(Guid id, string newText)
    {
        var encrypted = _cipher.Encrypt(newText);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Note SET EncryptedText = $text, Nonce = $nonce, Tag = $tag, UpdatedAt = $updatedAt
            WHERE Id = $id AND IsProtected = 0;
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
        using (var existsCommand = connection.CreateCommand())
        {
            existsCommand.CommandText = "SELECT EXISTS (SELECT 1 FROM Note WHERE Id = $id);";
            existsCommand.Parameters.AddWithValue("$id", id.ToString());
            if (Convert.ToInt32(existsCommand.ExecuteScalar()) == 0) return false;
        }

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO SyncTombstone (NoteId, DeletedAt, DeviceId)
            VALUES ($id, $deletedAt, 'local');
            DELETE FROM Note WHERE Id = $id;
            DELETE FROM NotePlacement WHERE NoteId = $id;
            DELETE FROM TaskCompletion WHERE NoteId = $id;
            DELETE FROM NoteReminder WHERE NoteId = $id;
            DELETE FROM NoteTag WHERE NoteId = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());
        command.Parameters.AddWithValue("$deletedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        return true;
    }

    /// <summary>Aplica una nota remota ya resuelta por el motor de sincronización.</summary>
    public void ApplySyncNote(Note note)
    {
        if (note.IsProtected && note.ProtectedContent is null)
            throw new FormatException("A protected note is missing its encrypted content.");
        var encrypted = _cipher.Encrypt(note.IsProtected ? string.Empty : note.Text);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Note (Id, EncryptedText, Nonce, Tag, Color, CreatedAt, UpdatedAt, State, ScreenOrigin,
                              IsProtected, ProtectionSalt, ProtectionCipherText, ProtectionNonce, ProtectionTag)
            VALUES ($id, $text, $nonce, $tag, $color, $createdAt, $updatedAt, $state, $screenOrigin,
                    $isProtected, $salt, $protectedText, $protectedNonce, $protectedTag)
            ON CONFLICT(Id) DO UPDATE SET
                EncryptedText = excluded.EncryptedText,
                Nonce = excluded.Nonce,
                Tag = excluded.Tag,
                Color = excluded.Color,
                CreatedAt = excluded.CreatedAt,
                UpdatedAt = excluded.UpdatedAt,
                State = excluded.State,
                ScreenOrigin = excluded.ScreenOrigin,
                IsProtected = excluded.IsProtected,
                ProtectionSalt = excluded.ProtectionSalt,
                ProtectionCipherText = excluded.ProtectionCipherText,
                ProtectionNonce = excluded.ProtectionNonce,
                ProtectionTag = excluded.ProtectionTag;
            DELETE FROM SyncTombstone WHERE NoteId = $id;
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
        command.Parameters.AddWithValue("$isProtected", note.IsProtected ? 1 : 0);
        command.Parameters.AddWithValue("$salt", (object?)note.ProtectedContent?.Salt ?? DBNull.Value);
        command.Parameters.AddWithValue("$protectedText", (object?)note.ProtectedContent?.CipherText ?? DBNull.Value);
        command.Parameters.AddWithValue("$protectedNonce", (object?)note.ProtectedContent?.Nonce ?? DBNull.Value);
        command.Parameters.AddWithValue("$protectedTag", (object?)note.ProtectedContent?.Tag ?? DBNull.Value);
        command.ExecuteNonQuery();

        // El orden del mazo viaja dentro de la nota. Si el sobre lo trae, se aplica; si no (un sobre
        // de una versión anterior a que el orden se sincronizara), se conserva el orden local en
        // vez de dejar la nota sin posición.
        if (note.DockPosition is { } position)
        {
            using var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = """
                INSERT OR REPLACE INTO NoteOrder (NoteId, Position) VALUES ($noteId, $position);
                """;
            orderCommand.Parameters.AddWithValue("$noteId", note.Id.ToString());
            orderCommand.Parameters.AddWithValue("$position", position);
            orderCommand.ExecuteNonQuery();
        }

        SetTags(note.Id, note.Tags, touch: false);
    }

    /// <summary>Aplica una eliminación remota sin generar una segunda tombstone local.</summary>
    public void ApplySyncTombstone(SyncTombstone tombstone)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM Note WHERE Id = $id;
            DELETE FROM NotePlacement WHERE NoteId = $id;
            DELETE FROM NoteOrder WHERE NoteId = $id;
            DELETE FROM TaskCompletion WHERE NoteId = $id;
            DELETE FROM NoteReminder WHERE NoteId = $id;
            DELETE FROM NoteTag WHERE NoteId = $id;
            INSERT OR REPLACE INTO SyncTombstone (NoteId, DeletedAt, DeviceId)
            VALUES ($id, $deletedAt, $deviceId);
            """;
        command.Parameters.AddWithValue("$id", tombstone.NoteId.ToString());
        command.Parameters.AddWithValue("$deletedAt", tombstone.DeletedAt.ToString("O"));
        command.Parameters.AddWithValue("$deviceId", tombstone.DeviceId);
        command.ExecuteNonQuery();
    }

    public ProtectedNoteContent Protect(Guid id, string password)
    {
        var note = GetById(id) ?? throw new InvalidOperationException("The note no longer exists.");
        if (note.IsProtected) throw new InvalidOperationException("The note is already protected.");
        var protectedContent = ProtectedNoteContent.Protect(note.Text, password);
        UpdateProtectionColumns(id, protectedContent, _cipher.Encrypt(string.Empty));
        return protectedContent;
    }

    public bool TryUnlock(Guid id, string password, out Note? unlocked)
    {
        unlocked = GetById(id);
        if (unlocked is null || !unlocked.IsProtected || unlocked.ProtectedContent is null) return false;
        if (!unlocked.ProtectedContent.TryUnprotect(password, out var plaintext)) return false;
        unlocked.Text = plaintext;
        unlocked.IsUnlocked = true;
        return true;
    }

    public bool RemoveProtection(Guid id, string password)
    {
        if (!TryUnlock(id, password, out var unlocked) || unlocked is null) return false;
        var encrypted = _cipher.Encrypt(unlocked.Text);
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Note SET EncryptedText = $text, Nonce = $nonce, Tag = $tag,
                IsProtected = 0, ProtectionSalt = NULL, ProtectionCipherText = NULL,
                ProtectionNonce = NULL, ProtectionTag = NULL, UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        AddEncryptedParameters(command, encrypted);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
        return true;
    }

    public void UpdateProtectedText(Guid id, string newText, string password)
    {
        var note = GetById(id) ?? throw new InvalidOperationException("The note no longer exists.");
        if (!note.IsProtected || note.ProtectedContent is null ||
            !note.ProtectedContent.TryUnprotect(password, out _))
            throw new UnauthorizedAccessException("The protected note password is incorrect.");

        UpdateProtectionColumns(id, note.ProtectedContent.Reprotect(newText, password),
            _cipher.Encrypt(string.Empty));
    }

    private void UpdateProtectionColumns(Guid id, ProtectedNoteContent protectedContent,
        EncryptedContent normalContent)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Note SET EncryptedText = $text, Nonce = $nonce, Tag = $tag,
                IsProtected = 1, ProtectionSalt = $salt, ProtectionCipherText = $protectedText,
                ProtectionNonce = $protectedNonce, ProtectionTag = $protectedTag,
                UpdatedAt = $updatedAt
            WHERE Id = $id;
            """;
        AddEncryptedParameters(command, normalContent);
        command.Parameters.AddWithValue("$salt", protectedContent.Salt);
        command.Parameters.AddWithValue("$protectedText", protectedContent.CipherText);
        command.Parameters.AddWithValue("$protectedNonce", protectedContent.Nonce);
        command.Parameters.AddWithValue("$protectedTag", protectedContent.Tag);
        command.Parameters.AddWithValue("$updatedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$id", id.ToString());
        command.ExecuteNonQuery();
    }

    private static void AddEncryptedParameters(SqliteCommand command, EncryptedContent encrypted)
    {
        command.Parameters.AddWithValue("$text", encrypted.CipherText);
        command.Parameters.AddWithValue("$nonce", encrypted.Nonce);
        command.Parameters.AddWithValue("$tag", encrypted.Tag);
    }

    /// <summary>
    /// Permanently deletes trashed notes whose <c>UpdatedAt</c> is older than <paramref name="retention"/>.
    /// Never touches Active or Archived notes. Returns the number of notes removed.
    /// </summary>
    public int PurgeExpiredTrash(TimeSpan retention)
    {
        var cutoff = DateTimeOffset.UtcNow - retention;
        using var connection = _database.OpenConnection();
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = """
            SELECT COUNT(*) FROM Note WHERE State = $state AND UpdatedAt < $cutoff;
            """;
        countCommand.Parameters.AddWithValue("$state", NoteState.Trashed.ToString());
        countCommand.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        int removed = Convert.ToInt32(countCommand.ExecuteScalar());
        if (removed == 0) return 0;

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO SyncTombstone (NoteId, DeletedAt, DeviceId)
            SELECT Id, $deletedAt, 'local' FROM Note
            WHERE State = $state AND UpdatedAt < $cutoff;
            DELETE FROM NotePlacement WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM NoteReminder WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM NoteTag WHERE NoteId IN (SELECT Id FROM Note WHERE State = $state AND UpdatedAt < $cutoff);
            DELETE FROM Note WHERE State = $state AND UpdatedAt < $cutoff;
            """;
        command.Parameters.AddWithValue("$state", NoteState.Trashed.ToString());
        command.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));
        command.Parameters.AddWithValue("$deletedAt", DateTimeOffset.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
        return removed;
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
            TouchUpdatedAt(reordered);
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
            TouchUpdatedAt(reordered);
            return;
        }

        SetOrder(noteId, target.Value);
        TouchUpdatedAt(new[] { noteId });
    }

    /// <summary>Marca notas como recién cambiadas sin tocar su contenido. El orden del mazo no vive
    /// en la fila de la nota, pero sí viaja con ella (<see cref="Note.DockPosition"/>) dentro del
    /// sobre: reordenar tiene que refrescar el momento de la última edición, porque si no el sobre
    /// de la nota no se volvería a publicar y el otro dispositivo conservaría el orden antiguo.</summary>
    private void TouchUpdatedAt(IReadOnlyList<Guid> noteIds)
    {
        var now = DateTimeOffset.UtcNow.ToString("O");
        using var connection = _database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var id in noteIds)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Note SET UpdatedAt = $updatedAt WHERE Id = $id;";
            command.Parameters.AddWithValue("$updatedAt", now);
            command.Parameters.AddWithValue("$id", id.ToString());
            command.ExecuteNonQuery();
        }
        transaction.Commit();
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

    /// <summary>
    /// Pone (o reemplaza) el recordatorio puntual de una nota. Se guarda siempre convertido a UTC,
    /// aunque quien llama trabaje en hora local (el selector de la nota, los atajos de
    /// <see cref="ReminderPresets"/>) — misma convención que <c>CreatedAt</c>/<c>UpdatedAt</c>.
    /// </summary>
    public void SetReminder(Guid noteId, DateTimeOffset dueAt)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO NoteReminder (NoteId, DueAt) VALUES ($noteId, $dueAt);
            """;
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$dueAt", dueAt.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>Quita el recordatorio de una nota, si tenía uno. No falla si no tenía ninguno.</summary>
    public void ClearReminder(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM NoteReminder WHERE NoteId = $noteId;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.ExecuteNonQuery();
    }

    /// <summary>El recordatorio activo de una nota, o null si no tiene ninguno — para pintar el
    /// estado actual en el menú "⋯" de la nota abierta.</summary>
    public DateTimeOffset? GetReminder(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT DueAt FROM NoteReminder WHERE NoteId = $noteId;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());

        var result = command.ExecuteScalar();
        return result is null
            ? null
            : DateTimeOffset.Parse(
                (string)result,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
    }

    /// <summary>Recordatorios vencidos a <paramref name="now"/> (inclusive) — usado tanto por el
    /// sondeo periódico como por el catch-up al arrancar (ver Aldune.Windowing.ReminderScheduler).
    /// No los borra: quien llama decide cuándo limpiarlos (ClearReminder), después de avisar.
    /// Excluye recordatorios de notas archivadas o en la papelera: el dock solo lista notas activas
    /// (GetByState(Active)), así que un aviso de una nota que ya no aparece en ningún sitio sería
    /// invisible salvo por el globo mismo, y al hacer clic llevaría a un sitio confuso.</summary>
    public IReadOnlyList<(Guid NoteId, DateTimeOffset DueAt)> GetDueReminders(DateTimeOffset now)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NoteId, DueAt FROM NoteReminder
            WHERE DueAt <= $now AND NoteId IN (SELECT Id FROM Note WHERE State = $activeState);
            """;
        command.Parameters.AddWithValue("$now", now.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$activeState", NoteState.Active.ToString());

        var results = new List<(Guid, DateTimeOffset)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results.Add((
                Guid.Parse((string)reader["NoteId"]),
                DateTimeOffset.Parse(
                    (string)reader["DueAt"],
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind)));
        }
        return results;
    }

    /// <summary>Todos los recordatorios activos, para pintar el indicador en el dock sin una consulta
    /// por nota (ver EdgeDockWindow.SetNotes). Excluye recordatorios de notas archivadas o en la
    /// papelera, mismo motivo que <see cref="GetDueReminders"/>: el dock solo lista notas activas.</summary>
    public IReadOnlyDictionary<Guid, DateTimeOffset> GetPendingReminders()
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NoteId, DueAt FROM NoteReminder
            WHERE NoteId IN (SELECT Id FROM Note WHERE State = $activeState);
            """;
        command.Parameters.AddWithValue("$activeState", NoteState.Active.ToString());

        var results = new Dictionary<Guid, DateTimeOffset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[Guid.Parse((string)reader["NoteId"])] = DateTimeOffset.Parse(
                (string)reader["DueAt"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }
        return results;
    }

    /// <summary>Una nota por Id, descifrada — para cuando solo se tiene el Guid (p. ej.
    /// ReminderScheduler, que solo conoce el NoteId de un recordatorio vencido) y no la lista
    /// completa que ya da GetByState.</summary>
    public Note? GetById(Guid id)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT n.Id, n.EncryptedText, n.Nonce, n.Tag, n.Color, n.CreatedAt, n.UpdatedAt, n.State, n.ScreenOrigin
            , n.IsProtected, n.ProtectionSalt, n.ProtectionCipherText, n.ProtectionNonce, n.ProtectionTag
            , o.Position AS DockPosition
            FROM Note n LEFT JOIN NoteOrder o ON o.NoteId = n.Id WHERE n.Id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString());

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        var note = ReadNote(reader);
        HydrateTags(new[] { note });
        return note;
    }

    /// <summary>Guarda o actualiza cuándo se marcó como hecha la tarea identificada por <paramref name="lineHash"/>
    /// (ver <see cref="TaskCompletion.HashLine"/>). Volver a marcar una tarea ya registrada reemplaza el
    /// momento anterior, no lo acumula.</summary>
    public void RecordTaskCompletion(Guid noteId, string lineHash, DateTimeOffset completedAt)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR REPLACE INTO TaskCompletion (NoteId, LineHash, CompletedAt)
            VALUES ($noteId, $lineHash, $completedAt);
            """;
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$lineHash", lineHash);
        command.Parameters.AddWithValue("$completedAt", completedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    /// <summary>Borra el registro de cuándo se marcó una tarea (al desmarcarla, o al descubrir que ya no
    /// corresponde a ninguna tarea marcada de verdad — ver <see cref="PruneExpiredCompletedTasks"/>).</summary>
    public void ClearTaskCompletion(Guid noteId, string lineHash)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM TaskCompletion WHERE NoteId = $noteId AND LineHash = $lineHash;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());
        command.Parameters.AddWithValue("$lineHash", lineHash);
        command.ExecuteNonQuery();
    }

    public IReadOnlyDictionary<string, DateTimeOffset> GetTaskCompletions(Guid noteId)
    {
        using var connection = _database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT LineHash, CompletedAt FROM TaskCompletion WHERE NoteId = $noteId;";
        command.Parameters.AddWithValue("$noteId", noteId.ToString());

        var results = new Dictionary<string, DateTimeOffset>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            results[(string)reader["LineHash"]] = DateTimeOffset.Parse(
                (string)reader["CompletedAt"],
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind);
        }
        return results;
    }

    /// <summary>
    /// Aplica el ajuste "borrar tareas completadas solas" al cuerpo de una nota (nunca al título, igual
    /// que la ventana): quita las líneas marcadas cuyo plazo haya vencido
    /// (<see cref="TaskCompletion.Prune"/>), guarda el resultado si cambió algo, empieza el reloj de las
    /// marcadas que no lo tenían y limpia los registros que ya no correspondan a ninguna tarea marcada.
    /// Devuelve si se borró alguna línea.
    /// </summary>
    public bool PruneExpiredCompletedTasks(Guid noteId, string currentText, TimeSpan delay)
    {
        var now = DateTimeOffset.UtcNow;
        var (title, body) = NoteText.Split(currentText);
        var result = TaskCompletion.Prune(body, GetTaskCompletions(noteId), now, delay);

        foreach (var hash in result.HashesToClear) ClearTaskCompletion(noteId, hash);
        foreach (var hash in result.HashesToStart) RecordTaskCompletion(noteId, hash, now);

        if (result.Changed)
        {
            UpdateText(noteId, NoteText.Join(title, result.Text));
        }
        return result.Changed;
    }

    /// <summary>
    /// Barrido de todas las notas activas que no estén en <paramref name="skip"/> (las abiertas, que se
    /// podan desde su ventana: podarlas aquí dejaría la ventana con el texto viejo, que volvería a
    /// guardarse encima). Se salta las protegidas: se leen sin texto, y podarlas con él borraría sus
    /// relojes. Devuelve cuántas notas cambiaron.
    /// </summary>
    public int PruneExpiredCompletedTasksInActiveNotes(TimeSpan delay, IReadOnlySet<Guid> skip)
    {
        int changed = 0;
        foreach (var note in GetByState(NoteState.Active))
        {
            if (note.IsProtected || skip.Contains(note.Id)) continue;
            if (PruneExpiredCompletedTasks(note.Id, note.Text, delay)) changed++;
        }
        return changed;
    }

    private Note ReadNote(SqliteDataReader reader)
    {
        var cipherText = (byte[])reader["EncryptedText"];
        var nonce = (byte[])reader["Nonce"];
        var tag = (byte[])reader["Tag"];
        bool isProtected = Convert.ToInt32(reader["IsProtected"]) != 0;
        var protectedContent = isProtected ? new ProtectedNoteContent
        {
            Salt = (string)reader["ProtectionSalt"],
            CipherText = (string)reader["ProtectionCipherText"],
            Nonce = (string)reader["ProtectionNonce"],
            Tag = (string)reader["ProtectionTag"]
        } : null;
        var text = isProtected ? string.Empty : _cipher.Decrypt(new EncryptedContent(cipherText, nonce, tag));

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
            ScreenOrigin = (string)reader["ScreenOrigin"],
            DockPosition = ReadDockPosition(reader),
            IsProtected = isProtected,
            ProtectedContent = protectedContent,
            IsUnlocked = !isProtected
        };
    }

    /// <summary>
    /// Posición del mazo si la consulta la trajo (los cuatro SELECT de notas la traen). Es null si
    /// la nota no tiene orden manual o si viene de un sobre de una versión anterior a que el orden
    /// se sincronizara: en ese caso conserva su orden local en vez de perderlo.
    /// </summary>
    private static double? ReadDockPosition(SqliteDataReader reader)
    {
        int index = reader.GetOrdinal("DockPosition");
        return reader.IsDBNull(index) ? null : reader.GetDouble(index);
    }
}
