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
        DropOutdatedNotePlacementTable(connection);

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
                ScreenOrigin TEXT NOT NULL,
                IsProtected INTEGER NOT NULL DEFAULT 0,
                ProtectionSalt TEXT,
                ProtectionCipherText TEXT,
                ProtectionNonce TEXT,
                ProtectionTag TEXT
            );

            CREATE TABLE IF NOT EXISTS NotePlacement (
                NoteId TEXT NOT NULL,
                MonitorKey TEXT NOT NULL,
                Left REAL NOT NULL,
                Top REAL NOT NULL,
                Width REAL NOT NULL,
                Height REAL NOT NULL,
                PRIMARY KEY (NoteId, MonitorKey)
            );

            -- El orden manual del mazo. Aparte de Note por el mismo motivo que NotePlacement: esa
            -- tabla tiene el contenido real del usuario y no hay migraciones. Position es REAL para
            -- poder insertar entre dos notas sin renumerar (ver Fanote.Core.NoteOrdering).
            CREATE TABLE IF NOT EXISTS NoteOrder (
                NoteId TEXT PRIMARY KEY NOT NULL,
                Position REAL NOT NULL
            );

            -- Cuándo se marcó cada tarea como hecha, para el ajuste opcional "borrar tareas
            -- completadas solas" (ver Fanote.Core.TaskCompletion). Aparte de Note por el mismo
            -- motivo que NoteOrder/NotePlacement. LineHash identifica la tarea por su contenido, no
            -- por su posición, porque la posición cambia con cualquier edición alrededor.
            CREATE TABLE IF NOT EXISTS TaskCompletion (
                NoteId TEXT NOT NULL,
                LineHash TEXT NOT NULL,
                CompletedAt TEXT NOT NULL,
                PRIMARY KEY (NoteId, LineHash)
            );

            -- Recordatorio puntual (no recurrente) de una nota, como mucho uno activo a la vez —
            -- de ahí NoteId como clave primaria en vez de una compuesta o un Id propio: poner uno
            -- nuevo reemplaza cualquiera anterior. Aparte de Note por el mismo motivo que las demás:
            -- esa tabla tiene el contenido real del usuario y no hay migraciones. Se borra la fila
            -- al dispararse (ver Fanote.Windowing.ReminderScheduler) o al cancelarse a mano.
            CREATE TABLE IF NOT EXISTS NoteReminder (
                NoteId TEXT PRIMARY KEY NOT NULL,
                DueAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Tag (
                Id TEXT PRIMARY KEY NOT NULL,
                Name TEXT NOT NULL COLLATE NOCASE UNIQUE
            );

            CREATE TABLE IF NOT EXISTS NoteTag (
                NoteId TEXT NOT NULL,
                TagId TEXT NOT NULL,
                PRIMARY KEY (NoteId, TagId)
            );

            -- Las eliminaciones permanentes se conservan como tombstones para que una copia antigua
            -- de otro dispositivo no haga reaparecer la nota al sincronizar.
            CREATE TABLE IF NOT EXISTS SyncTombstone (
                NoteId TEXT PRIMARY KEY NOT NULL,
                DeletedAt TEXT NOT NULL,
                DeviceId TEXT NOT NULL
            );

            -- A conflict keeps only the losing version, encrypted with the local database key.
            -- It is a recovery queue, not a second sync source: dismissing it never changes the
            -- canonical remote version, while restoring it deliberately applies that version.
            CREATE TABLE IF NOT EXISTS SyncConflict (
                Id TEXT PRIMARY KEY NOT NULL,
                NoteId TEXT NOT NULL,
                OccurredAt TEXT NOT NULL,
                WinnerUpdatedAt TEXT NOT NULL,
                WinnerDeviceId TEXT NOT NULL,
                LosingUpdatedAt TEXT NOT NULL,
                LosingDeviceId TEXT NOT NULL,
                LosingTombstone INTEGER NOT NULL,
                EncryptedNote BLOB,
                NoteNonce BLOB,
                NoteTag BLOB
            );
            """;
        command.ExecuteNonQuery();
        EnsureNoteProtectionColumns(connection);
    }

    private static void EnsureNoteProtectionColumns(SqliteConnection connection)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var check = connection.CreateCommand())
        {
            check.CommandText = "SELECT name FROM pragma_table_info('Note');";
            using var reader = check.ExecuteReader();
            while (reader.Read()) existing.Add((string)reader[0]);
        }

        var columns = new Dictionary<string, string>
        {
            ["IsProtected"] = "INTEGER NOT NULL DEFAULT 0",
            ["ProtectionSalt"] = "TEXT",
            ["ProtectionCipherText"] = "TEXT",
            ["ProtectionNonce"] = "TEXT",
            ["ProtectionTag"] = "TEXT"
        };
        foreach (var column in columns)
        {
            if (existing.Contains(column.Key)) continue;
            using var alter = connection.CreateCommand();
            alter.CommandText = $"ALTER TABLE Note ADD COLUMN {column.Key} {column.Value};";
            alter.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Esta app no tiene sistema de migraciones (ver docs/STATUS.md): cada tabla se crea una vez
    /// con <c>CREATE TABLE IF NOT EXISTS</c> y ya está, lo que basta mientras nunca cambie una
    /// existente. <c>NotePlacement</c> sí cambió — ganó <c>MonitorKey</c> como parte de su clave
    /// primaria (recordar posición pasó de ser global a por pantalla), y una base de datos ya
    /// creada con el esquema viejo se queda con él para siempre si no se hace algo aquí, porque
    /// <c>CREATE TABLE IF NOT EXISTS</c> no toca una tabla que ya existe.
    ///
    /// La solución no es una migración fila a fila: <c>NotePlacement</c> es caché de UI (dónde
    /// estaba una ventana), no contenido del usuario como <c>Note</c> — perder las posiciones
    /// recordadas de una versión anterior no pierde ninguna nota, así que basta con recrear la
    /// tabla entera si le falta la columna nueva.
    /// </summary>
    private static void DropOutdatedNotePlacementTable(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('NotePlacement') WHERE name = 'MonitorKey';";
        var hasMonitorKey = Convert.ToInt64(check.ExecuteScalar()!) > 0;
        if (hasMonitorKey) return;

        using var drop = connection.CreateCommand();
        drop.CommandText = "DROP TABLE IF EXISTS NotePlacement;";
        drop.ExecuteNonQuery();
    }
}
