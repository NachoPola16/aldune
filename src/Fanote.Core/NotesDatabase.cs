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
                ScreenOrigin TEXT NOT NULL
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
            """;
        command.ExecuteNonQuery();
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
