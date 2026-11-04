using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Aldune.Core;

/// <summary>What a backup contains, written into the zip of a manual backup (and synthesised for a daily folder).</summary>
public sealed record BackupManifest
{
    public int Format { get; init; }
    public string AppVersion { get; init; } = "";
    public DateTime CreatedUtc { get; init; }
    public int NoteCount { get; init; }
    /// <summary>PRAGMA user_version of the database inside; lets an older build refuse a newer file.</summary>
    public int SchemaVersion { get; init; }
}

public enum BackupProblem
{
    None,
    /// <summary>Not a readable zip or folder.</summary>
    Unreadable,
    MissingManifest,
    MissingDatabase,
    /// <summary>Notes are encrypted with a key that only lives in settings.json: without it the database is useless.</summary>
    MissingSettings,
    DatabaseDamaged,
    /// <summary>Written by a newer Aldune (manifest format or database schema): restoring it here could damage it.</summary>
    TooNew,
    /// <summary>The database key in the settings was wrapped for another Windows user or computer (DPAPI): the notes could never be opened here.</summary>
    KeyNotUsable,
}

public sealed record BackupCheck(BackupProblem Problem, BackupManifest? Manifest)
{
    public bool IsOk => Problem == BackupProblem.None;
}

public static partial class LocalBackup
{
    public const int ManifestFormat = 1;
    /// <summary>The highest PRAGMA user_version this build understands (the schema is still unstamped, so 0).</summary>
    public const int SupportedSchemaVersion = 0;
    public const string PendingFolderName = "restore-pending";
    public const string BeforeRestoreSuffix = ".before-restore";

    private const string ManifestName = "manifest.json";
    private const string SettingsName = "settings.json";
    private const long MaxDatabaseBytes = 4L * 1024 * 1024 * 1024;
    private const long MaxSettingsBytes = 16L * 1024 * 1024;
    private static readonly string[] RequiredNoteColumns =
        ["Id", "EncryptedText", "Nonce", "Tag", "Color", "CreatedAt", "UpdatedAt", "State", "ScreenOrigin"];

    /// <summary>
    /// One portable .zip with a hot copy of the database, settings.json and a manifest. It is written to
    /// a sibling .tmp and moved into place, so a failure never leaves half a zip under the chosen name
    /// (nor destroys the previous file of that name).
    /// </summary>
    public static BackupManifest CreateManual(string appDataDir, string zipPath, string appVersion, DateTimeOffset now)
    {
        var databasePath = Path.Combine(appDataDir, BrandIdentity.DatabaseFileName);
        var settingsPath = Path.Combine(appDataDir, SettingsName);
        if (!File.Exists(databasePath)) throw new FileNotFoundException("No database to back up.", databasePath);
        if (!File.Exists(settingsPath)) throw new FileNotFoundException("No settings to back up.", settingsPath);

        var temp = NewTempDir();
        var partial = zipPath + ".tmp";
        try
        {
            var copy = Path.Combine(temp, BrandIdentity.DatabaseFileName);
            // SQLite's online backup, not File.Copy: the app has the file open and may be mid-write.
            using (var source = Open(databasePath, SqliteOpenMode.ReadOnly))
            using (var target = Open(copy, SqliteOpenMode.ReadWriteCreate))
                source.BackupDatabase(target);

            var (problem, notes, schema) = CheckDatabase(copy);
            if (problem != BackupProblem.None) throw new IOException("The database could not be copied cleanly.");
            var manifest = new BackupManifest
            {
                Format = ManifestFormat, AppVersion = appVersion, CreatedUtc = now.UtcDateTime, NoteCount = notes, SchemaVersion = schema,
            };

            // The settings file is copied to the temp folder first: it may be rewritten by the app while we zip it.
            var settingsCopy = Path.Combine(temp, SettingsName);
            File.Copy(settingsPath, settingsCopy);

            if (File.Exists(partial)) File.Delete(partial);
            using (var archive = ZipFile.Open(partial, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(copy, BrandIdentity.DatabaseFileName, CompressionLevel.Optimal);
                archive.CreateEntryFromFile(settingsCopy, SettingsName, CompressionLevel.Optimal);
                using var writer = new StreamWriter(archive.CreateEntry(ManifestName).Open(), new System.Text.UTF8Encoding(false));
                writer.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
            }
            File.Move(partial, zipPath, overwrite: true);
            return manifest;
        }
        finally
        {
            if (File.Exists(partial)) File.Delete(partial);
            DeleteTemp(temp);
        }
    }

    /// <summary>Checks a manual backup (.zip) or a daily folder without touching any live data.</summary>
    public static BackupCheck Verify(string source)
    {
        var temp = NewTempDir();
        try { return ExtractAndCheck(source, temp); }
        finally { DeleteTemp(temp); }
    }

    public static bool HasPendingRestore(string appDataDir) =>
        Directory.Exists(Path.Combine(appDataDir, PendingFolderName));

    /// <summary>
    /// First half of a restore: verifies the backup and copies it into <c>restore-pending</c>. The live
    /// files are NOT touched: the app keeps the database open (pooled connections, sync, linked files),
    /// so the swap is done by <see cref="ApplyPendingRestore"/> at the next start, before anything opens it.
    /// </summary>
    public static BackupCheck StageRestore(string appDataDir, string source)
    {
        Directory.CreateDirectory(appDataDir);
        var staging = Path.Combine(appDataDir, PendingFolderName + ".tmp");
        var pending = Path.Combine(appDataDir, PendingFolderName);
        // Only staged copies are deleted here, never user data.
        DeleteTemp(staging);
        Directory.CreateDirectory(staging);
        BackupCheck check;
        try { check = ExtractAndCheck(source, staging); }
        catch
        {
            DeleteTemp(staging);
            throw;
        }
        if (!check.IsOk)
        {
            DeleteTemp(staging);
            return check;
        }
        DeleteTemp(pending);
        Directory.Move(staging, pending);
        return check;
    }

    /// <summary>
    /// Second half: if a restore is staged, re-verifies it and swaps it in. Each live file is renamed to
    /// <c>*.before-restore</c> (a numbered variant if that name is taken), never deleted or overwritten.
    /// Returns true when the swap happened. Must run before the database is opened.
    /// </summary>
    public static bool ApplyPendingRestore(string appDataDir)
    {
        var pending = Path.Combine(appDataDir, PendingFolderName);
        if (!Directory.Exists(pending)) return false;

        var stagedDb = Path.Combine(pending, BrandIdentity.DatabaseFileName);
        var stagedSettings = Path.Combine(pending, SettingsName);
        // The staged copy sat on disk between two runs: trust it only after checking it again, settings included (a database restored with
        // settings that do not parse would start the app with a key it cannot read).
        if (!File.Exists(stagedSettings) || !File.Exists(stagedDb) || !SettingsAreJson(stagedSettings) || !KeyCanBeUnwrapped(stagedSettings)
            || CheckDatabase(stagedDb).Problem != BackupProblem.None)
        {
            DeleteTemp(pending);
            return false;
        }

        var liveDb = Path.Combine(appDataDir, BrandIdentity.DatabaseFileName);
        var liveSettings = Path.Combine(appDataDir, SettingsName);

        // One suffix for the whole set so that a kept database stays paired with its own -wal/-shm.
        var suffix = BeforeRestoreSuffix;
        for (int n = 2; AnyExists(suffix); n++) suffix = BeforeRestoreSuffix + "-" + n.ToString(CultureInfo.InvariantCulture);

        bool AnyExists(string s) => new[] { liveDb, liveDb + "-wal", liveDb + "-shm", liveSettings }.Any(f => File.Exists(f + s));

        // Both new files are copied next to the live ones BEFORE anything is touched: if that fails the live data and the staged copy are
        // exactly as they were. Then the live files are renamed aside, the new ones renamed in, and any failure puts the originals back.
        const string incoming = ".restore-new";
        File.Copy(stagedDb, liveDb + incoming, overwrite: true);
        File.Copy(stagedSettings, liveSettings + incoming, overwrite: true);

        var moved = new List<(string Live, string Kept)>();
        var placed = new List<string>();
        try
        {
            // A leftover journal of the old database must not be replayed onto the restored one.
            foreach (var live in new[] { liveDb, liveDb + "-wal", liveDb + "-shm", liveSettings })
            {
                if (!File.Exists(live)) continue;
                File.Move(live, live + suffix);
                moved.Add((live, live + suffix));
            }
            foreach (var live in new[] { liveDb, liveSettings })
            {
                File.Move(live + incoming, live);
                placed.Add(live);
            }
        }
        catch
        {
            foreach (var live in placed) TryDelete(live);
            foreach (var (live, kept) in moved)
            {
                try { File.Move(kept, live); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            TryDelete(liveDb + incoming);
            TryDelete(liveSettings + incoming);
            throw;
        }

        DeleteTemp(pending);
        return true;
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>
    /// False when the settings carry a database key that this Windows user cannot unwrap (the backup came from another account or computer).
    /// A settings file without a key is left to the existing checks: it is the same as before.
    /// </summary>
    private static bool KeyCanBeUnwrapped(string settingsPath)
    {
        byte[]? wrapped;
        try { wrapped = new SettingsService(settingsPath).Load().WrappedDatabaseKey; }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return true; }
        if (wrapped is null) return true;
        try { CryptographicOperations.ZeroMemory(DatabaseKeyProvider.Unwrap(wrapped)); return true; }
        catch (CryptographicException) { return false; }
    }

    private static bool SettingsAreJson(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>Extracts the database and settings of <paramref name="source"/> into <paramref name="dest"/> and checks them.</summary>
    private static BackupCheck ExtractAndCheck(string source, string dest)
    {
        var dbPath = Path.Combine(dest, BrandIdentity.DatabaseFileName);
        var settingsPath = Path.Combine(dest, SettingsName);
        BackupManifest? manifest;
        try
        {
            if (Directory.Exists(source))
            {
                var sourceDb = Path.Combine(source, BrandIdentity.DatabaseFileName);
                var sourceSettings = Path.Combine(source, SettingsName);
                if (!File.Exists(sourceDb)) return new(BackupProblem.MissingDatabase, null);
                if (!File.Exists(sourceSettings)) return new(BackupProblem.MissingSettings, null);
                File.Copy(sourceDb, dbPath);
                File.Copy(sourceSettings, settingsPath);
                // A daily folder has no manifest; its name is its date.
                manifest = new BackupManifest
                {
                    Format = ManifestFormat,
                    CreatedUtc = DateTime.TryParseExact(Path.GetFileName(source), DayFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal | DateTimeStyles.AdjustToUniversal, out var day) ? day : default,
                };
            }
            else
            {
                using var archive = ZipFile.OpenRead(source);
                // Entries are looked up by exact name and written to fixed paths: a crafted entry name can never choose where a file lands.
                var manifestEntry = archive.GetEntry(ManifestName);
                if (manifestEntry is null) return new(BackupProblem.MissingManifest, null);
                try
                {
                    using var stream = manifestEntry.Open();
                    manifest = JsonSerializer.Deserialize<BackupManifest>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch (JsonException) { manifest = null; }
                if (manifest is null) return new(BackupProblem.MissingManifest, null);
                if (manifest.Format > ManifestFormat || manifest.Format < 1) return new(BackupProblem.TooNew, manifest);

                var dbEntry = archive.GetEntry(BrandIdentity.DatabaseFileName);
                if (dbEntry is null) return new(BackupProblem.MissingDatabase, manifest);
                var settingsEntry = archive.GetEntry(SettingsName);
                if (settingsEntry is null) return new(BackupProblem.MissingSettings, manifest);
                // A tiny zip can claim gigabytes: refuse what no real backup looks like before writing anything.
                if (dbEntry.Length > MaxDatabaseBytes || settingsEntry.Length > MaxSettingsBytes) return new(BackupProblem.Unreadable, manifest);
                // The sizes above are the zip's own claim: what is actually written is bounded too.
                using (var db = dbEntry.Open()) BoundedCopy.ToFile(db, dbPath, MaxDatabaseBytes);
                using (var settings = settingsEntry.Open()) BoundedCopy.ToFile(settings, settingsPath, MaxSettingsBytes);
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new(BackupProblem.Unreadable, null);
        }

        if (!SettingsAreJson(settingsPath)) return new(BackupProblem.MissingSettings, manifest);
        if (!KeyCanBeUnwrapped(settingsPath)) return new(BackupProblem.KeyNotUsable, manifest);
        var (problem, notes, schema) = CheckDatabase(dbPath);
        // The counts come from the database itself, not from what the manifest claims.
        var verified = manifest with { NoteCount = notes, SchemaVersion = schema };
        return new(problem, problem == BackupProblem.None || problem == BackupProblem.TooNew ? verified : manifest);
    }

    private static (BackupProblem Problem, int NoteCount, int Schema) CheckDatabase(string path)
    {
        try
        {
            using var connection = Open(path, SqliteOpenMode.ReadOnly);
            int schema = Scalar(connection, "PRAGMA user_version;");
            if (schema > SupportedSchemaVersion) return (BackupProblem.TooNew, 0, schema);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA integrity_check;";
                if (!string.Equals(command.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase))
                    return (BackupProblem.DatabaseDamaged, 0, schema);
            }
            if (Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Note';") != 1)
                return (BackupProblem.DatabaseDamaged, 0, schema);
            // A valid SQLite file with a Note table of another shape would start the app on something it cannot read.
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var info = connection.CreateCommand())
            {
                info.CommandText = "SELECT name FROM pragma_table_info('Note');";
                using var reader = info.ExecuteReader();
                while (reader.Read()) columns.Add(reader.GetString(0));
            }
            if (!RequiredNoteColumns.All(columns.Contains)) return (BackupProblem.DatabaseDamaged, 0, schema);
            return (BackupProblem.None, Scalar(connection, "SELECT COUNT(*) FROM Note;"), schema);
        }
        catch (SqliteException)
        {
            return (BackupProblem.DatabaseDamaged, 0, 0);
        }
    }

    private static int Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "aldune-backup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteTemp(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
