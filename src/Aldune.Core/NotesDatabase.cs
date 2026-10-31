using Microsoft.Data.Sqlite;

namespace Aldune.Core;

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
        AddManualHeightColumn(connection);
        LegacySyncBasesMigrated = MigrateSyncBaseTable(connection);

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
                ManualHeight INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (NoteId, MonitorKey)
            );

            -- El orden manual del mazo. Aparte de Note por el mismo motivo que NotePlacement: esa
            -- tabla tiene el contenido real del usuario y no hay migraciones. Position es REAL para
            -- poder insertar entre dos notas sin renumerar (ver Aldune.Core.NoteOrdering).
            CREATE TABLE IF NOT EXISTS NoteOrder (
                NoteId TEXT PRIMARY KEY NOT NULL,
                Position REAL NOT NULL
            );

            -- Cuándo se marcó cada tarea como hecha, para el ajuste opcional "borrar tareas
            -- completadas solas" (ver Aldune.Core.TaskCompletion). Aparte de Note por el mismo
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
            -- al dispararse (ver Aldune.Windowing.ReminderScheduler) o al cancelarse a mano.
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

            -- Última versión en la que este dispositivo y el almacén coincidieron, por nota. Un
            -- conflicto real exige que los dos lados hayan cambiado desde ella; sin esta base, cada
            -- edición normal hecha en otro dispositivo se guardaba como conflicto.
            -- ProfileKey separa los almacenes: con varios perfiles sincronizando a la vez, la versión acordada con
            -- uno no vale para otro (ver SyncProfileSettings.BaseKey). La vacía es la del primer perfil.
            CREATE TABLE IF NOT EXISTS SyncBase (
                ProfileKey TEXT NOT NULL DEFAULT '',
                NoteId TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                PRIMARY KEY (ProfileKey, NoteId)
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

            -- Notas vinculadas a un archivo del disco (spec 2026-10-04). Local: nunca viaja por la sync. La
            -- ruta va cifrada como el texto de las notas.
            CREATE TABLE IF NOT EXISTS NoteFileLink (
                NoteId TEXT PRIMARY KEY NOT NULL,
                EncryptedPath BLOB NOT NULL,
                PathNonce BLOB NOT NULL,
                PathTag BLOB NOT NULL,
                SyncEnabled INTEGER NOT NULL DEFAULT 0,
                KnownHash TEXT,
                KnownWriteTime TEXT,
                KnownTextHash TEXT
            );

            -- "Siempre encima" por nota: toda nota va fijada por defecto y aquí solo están las que se soltaron.
            -- Local, no viaja por la sync, y aparte de Note (que tiene datos reales y no se migra).
            CREATE TABLE IF NOT EXISTS NoteUnpinned (
                NoteId TEXT PRIMARY KEY NOT NULL
            );
            """;
        command.ExecuteNonQuery();
        EnsureNoteProtectionColumns(connection);
        EnsureSyncConflictProfileKey(connection);
    }

    /// <summary>
    /// <c>SyncBase</c> pasó a clave (ProfileKey, NoteId). Aquí sí se conservan las filas (pasan a la clave vacía, la del
    /// primer perfil): perder las bases no pierde notas, pero una base que falta guarda como conflicto lo que era una
    /// edición normal, y con la sync de todos los perfiles eso saldría una vez por cada nota.
    /// Una versión anterior que abra esta base sigue funcionando: ProfileKey tiene valor por defecto.
    /// </summary>
    /// <summary>Esta apertura migró la tabla SyncBase de antes de los perfiles (ver <see cref="SyncBaseMigration"/>): solo entonces sus filas son del perfil que sincronizaba.</summary>
    public bool LegacySyncBasesMigrated { get; private set; }

    private static bool MigrateSyncBaseTable(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT " +
            "(SELECT COUNT(*) FROM pragma_table_info('SyncBase')), " +
            "(SELECT COUNT(*) FROM pragma_table_info('SyncBase') WHERE name = 'ProfileKey');";
        using (var reader = check.ExecuteReader())
        {
            reader.Read();
            if (reader.GetInt64(0) == 0 || reader.GetInt64(1) > 0) return false;
        }

        using var transaction = connection.BeginTransaction();
        using var migrate = connection.CreateCommand();
        migrate.Transaction = transaction;
        migrate.CommandText = """
            ALTER TABLE SyncBase RENAME TO SyncBaseOld;
            CREATE TABLE SyncBase (
                ProfileKey TEXT NOT NULL DEFAULT '',
                NoteId TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                DeviceId TEXT NOT NULL,
                PRIMARY KEY (ProfileKey, NoteId)
            );
            INSERT INTO SyncBase (ProfileKey, NoteId, UpdatedAt, DeviceId)
                SELECT '', NoteId, UpdatedAt, DeviceId FROM SyncBaseOld;
            DROP TABLE SyncBaseOld;
            """;
        migrate.ExecuteNonQuery();
        transaction.Commit();
        return true;
    }

    /// <summary>Los conflictos guardados antes de la sync de varios perfiles quedan en la clave vacía (primer perfil).</summary>
    private static void EnsureSyncConflictProfileKey(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('SyncConflict') WHERE name = 'ProfileKey';";
        if (Convert.ToInt64(check.ExecuteScalar()!) > 0) return;

        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE SyncConflict ADD COLUMN ProfileKey TEXT NOT NULL DEFAULT '';";
        alter.ExecuteNonQuery();
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
            ["ProtectionTag"] = "TEXT",
            // Nombre visible de una nota protegida (en claro, opcional): ver ProtectedTitleRules.
            ["ProtectedTitle"] = "TEXT",
            // Fijada arriba (ver Note.IsPinnedToTop). Una base anterior la recibe con 0: ninguna nota fijada.
            ["PinnedToTop"] = "INTEGER NOT NULL DEFAULT 0",
            // Dispositivo que escribió la versión actual, si llegó por sincronización (NULL si se
            // editó aquí). Sin él, la copia local se firmaba con el id de este equipo y nunca
            // coincidía con la del almacén: ver SyncService.CreateLocalEnvelopes.
            ["SyncAuthorDeviceId"] = "TEXT"
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

    /// <summary>
    /// <c>ManualHeight</c> llegó después (1.7.9). Se añade con ALTER en vez de recrear la tabla: aquí sí
    /// merece conservar las posiciones y tamaños ya guardados. Lo ya guardado queda como alto automático,
    /// que es lo que se hacía hasta ahora.
    /// </summary>
    private static void AddManualHeightColumn(SqliteConnection connection)
    {
        using var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM pragma_table_info('NotePlacement') WHERE name = 'ManualHeight';";
        // Si la tabla aún no existe no hay nada que añadir: CREATE TABLE la crea ya con la columna.
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'NotePlacement';";
        if (Convert.ToInt64(exists.ExecuteScalar()!) == 0) return;
        if (Convert.ToInt64(check.ExecuteScalar()!) > 0) return;

        using var alter = connection.CreateCommand();
        alter.CommandText = "ALTER TABLE NotePlacement ADD COLUMN ManualHeight INTEGER NOT NULL DEFAULT 0;";
        alter.ExecuteNonQuery();
    }
}
