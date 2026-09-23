using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Aldune.Core;

/// <summary>
/// Copia diaria de <c>notes.db</c> y <c>settings.json</c> en <c>backups/aaaa-mm-dd</c> dentro de la
/// carpeta de datos, conservando las últimas <see cref="KeepDays"/>. Las dos juntas: las notas van
/// cifradas con una clave que solo vive (envuelta con DPAPI) en settings.json, así que una sin la
/// otra no sirve. Por eso mismo la copia solo se restaura con el mismo usuario de Windows.
/// </summary>
public static class LocalBackup
{
    public const string FolderName = "backups";
    public const int KeepDays = 7;

    private const string DayFormat = "yyyy-MM-dd";

    /// <summary>
    /// Hace la copia del día si todavía no existe y poda las antiguas. Devuelve la carpeta creada, o
    /// null si ya había copia de hoy o no hay base de datos que copiar.
    /// </summary>
    public static string? CreateDaily(string appDataDir, DateTimeOffset now, int keep = KeepDays)
    {
        var databasePath = Path.Combine(appDataDir, BrandIdentity.DatabaseFileName);
        if (!File.Exists(databasePath)) return null;

        var root = Path.Combine(appDataDir, FolderName);
        var folder = Path.Combine(root, now.ToLocalTime().ToString(DayFormat, CultureInfo.InvariantCulture));
        if (Directory.Exists(folder)) return null;

        // Se escribe en una carpeta temporal y se renombra al final: una copia a medias (sin espacio,
        // o la app cerrándose) no debe quedar con el nombre de un día válido. Las que dejara un fallo
        // anterior, de cualquier día, se borran: si no, ocuparían disco para siempre.
        if (Directory.Exists(root))
        {
            foreach (var leftover in Directory.EnumerateDirectories(root, "*.tmp"))
                Directory.Delete(leftover, recursive: true);
        }
        var staging = folder + ".tmp";
        Directory.CreateDirectory(staging);

        // La API de copia en caliente de SQLite, no File.Copy: el fichero puede estar abierto y a
        // mitad de una escritura, y una copia byte a byte en ese momento puede salir corrupta.
        using (var source = Open(databasePath, SqliteOpenMode.ReadOnly))
        using (var target = Open(Path.Combine(staging, BrandIdentity.DatabaseFileName), SqliteOpenMode.ReadWriteCreate))
        {
            source.BackupDatabase(target);
        }

        var settingsPath = Path.Combine(appDataDir, "settings.json");
        if (File.Exists(settingsPath)) File.Copy(settingsPath, Path.Combine(staging, "settings.json"));

        Directory.Move(staging, folder);
        Prune(root, keep);
        return folder;
    }

    /// <summary>Las carpetas de copia, de la más reciente a la más antigua.</summary>
    public static IReadOnlyList<string> ListNewestFirst(string appDataDir)
    {
        var root = Path.Combine(appDataDir, FolderName);
        if (!Directory.Exists(root)) return Array.Empty<string>();

        return Directory.EnumerateDirectories(root)
            .Where(path => DateTime.TryParseExact(Path.GetFileName(path), DayFormat,
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
            .ToList();
    }

    private static void Prune(string root, int keep)
    {
        foreach (var old in ListNewestFirst(Path.GetDirectoryName(root)!).Skip(keep))
            Directory.Delete(old, recursive: true);
    }

    private static SqliteConnection Open(string path, SqliteOpenMode mode)
    {
        // Sin pool: una conexión que se queda abierta en el pool mantendría bloqueado el fichero de
        // la copia y no se podría podar ni mover.
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }
}

/// <summary>
/// Qué hacer cuando settings.json no trae la clave de la base de datos pero sí hay notas: generar
/// una clave nueva las dejaría ilegibles para siempre. En su lugar se busca la clave en las copias
/// de <see cref="LocalBackup"/> y solo se acepta la que de verdad descifra esta base de datos.
/// </summary>
public static class DatabaseKeyRecovery
{
    public static bool DatabaseHasNotes(string databasePath)
    {
        if (!File.Exists(databasePath)) return false;
        try
        {
            using var connection = OpenReadOnly(databasePath);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM Note);";
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }
        catch (SqliteException)
        {
            // Sin tabla Note (base de datos recién creada o ajena) no hay notas que proteger.
            return false;
        }
    }

    /// <summary>La clave envuelta de la copia más reciente que descifra una nota de esta base de
    /// datos, o null si ninguna lo consigue.</summary>
    public static byte[]? FindWrappedKey(string appDataDir, string databasePath) =>
        FindSettingsBackup(appDataDir, databasePath)?.WrappedKey;

    /// <summary>El settings.json de la copia más reciente cuya clave descifra esta base de datos,
    /// con esa clave y el día de la copia; o null si ninguna lo consigue.</summary>
    public static (string Path, byte[] WrappedKey, string Day)? FindSettingsBackup(string appDataDir, string databasePath)
    {
        foreach (var folder in LocalBackup.ListNewestFirst(appDataDir))
        {
            var settingsPath = Path.Combine(folder, "settings.json");
            if (!File.Exists(settingsPath)) continue;

            byte[]? wrapped;
            try
            {
                wrapped = new SettingsService(settingsPath).Load().WrappedDatabaseKey;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException)
            {
                continue;
            }

            if (wrapped is not null && Opens(databasePath, wrapped))
                return (settingsPath, wrapped, System.IO.Path.GetFileName(folder));
        }

        return null;
    }

    private static bool Opens(string databasePath, byte[] wrappedKey)
    {
        byte[]? key = null;
        try
        {
            key = DatabaseKeyProvider.Unwrap(wrappedKey);
            using var connection = OpenReadOnly(databasePath);
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT EncryptedText, Nonce, Tag FROM Note LIMIT 1;";
            using var reader = command.ExecuteReader();
            if (!reader.Read()) return false;

            using var cipher = new ContentCipher(key);
            cipher.Decrypt(new EncryptedContent(
                (byte[])reader["EncryptedText"], (byte[])reader["Nonce"], (byte[])reader["Tag"]));
            return true;
        }
        catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or SqliteException
                                   or ArgumentException)
        {
            return false;
        }
        finally
        {
            if (key is not null) System.Security.Cryptography.CryptographicOperations.ZeroMemory(key);
        }
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }
}
