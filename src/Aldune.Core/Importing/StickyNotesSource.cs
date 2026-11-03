using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace Aldune.Core.Importing;

/// <summary>
/// Microsoft Sticky Notes (the Windows 10/11 app): <c>plum.sqlite</c> in the app's package folder. The schema is the one the app is known to
/// use — a <c>Note</c> table with <c>Text</c>, <c>Theme</c>, <c>CreatedAt</c>/<c>UpdatedAt</c>/<c>DeletedAt</c> (.NET ticks) and, in other tables,
/// <c>Media</c> and <c>Stroke</c> rows pointing at a note by <c>ParentId</c> — but nothing is assumed beyond <c>Text</c>: every other column is
/// optional and read only if it exists. The text carries a <c>\id=GUID </c> prefix on each paragraph.
///
/// The live database is never opened in place: it is copied (with its -wal / -shm) to a private temp folder, read there and the copy is deleted,
/// so Sticky Notes can be running and the original is never locked or touched. The pre-2018 <c>StickyNotes.snt</c> format is not supported.
/// </summary>
public sealed partial class StickyNotesSource : IImportSource
{
    public const string PackageFolder = "Microsoft.MicrosoftStickyNotes_8wekyb3d8bbwe";

    [GeneratedRegex(@"^\\id=\S*[ ]?")]
    private static partial Regex IdPrefix();

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreak();

    private static readonly byte[] SqliteHeader = Encoding.ASCII.GetBytes("SQLite format 3\0");

    /// <summary>Where Windows keeps the database, or null when the app was never used on this account.</summary>
    public static string? DefaultPath()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (local.Length == 0) return null;
        var path = Path.Combine(local, "Packages", PackageFolder, "LocalState", "plum.sqlite");
        return File.Exists(path) ? path : null;
    }

    public ImportPreview Read(string path)
    {
        if (Directory.Exists(path)) path = Path.Combine(path, "plum.sqlite");
        if (!File.Exists(path)) return ImportPreview.Failed(ImportFailure.NotFound);

        var temp = Path.Combine(Path.GetTempPath(), "aldune-import-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (new FileInfo(path).Length > ImportLimits.MaxDatabaseBytes) return ImportPreview.Failed(ImportFailure.TooLarge);
            if (!HasSqliteHeader(path)) return ImportPreview.Failed(ImportFailure.WrongFormat);

            Directory.CreateDirectory(temp);
            var copy = Path.Combine(temp, "plum.sqlite");
            CopyShared(path, copy);
            foreach (var suffix in new[] { "-wal", "-shm" })
                if (File.Exists(path + suffix)) CopyShared(path + suffix, copy + suffix);

            return ReadCopy(copy);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException)
        {
            return ImportPreview.Failed(ImportFailure.Unreadable);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { Directory.Delete(temp, recursive: true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static bool HasSqliteHeader(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var head = new byte[SqliteHeader.Length];
        return stream.Read(head, 0, head.Length) == head.Length && head.AsSpan().SequenceEqual(SqliteHeader);
    }

    // FileShare.ReadWrite: the app may have the file open.
    private static void CopyShared(string from, string to)
    {
        using var source = new FileStream(from, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var target = new FileStream(to, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(target);
    }

    private static ImportPreview ReadCopy(string copy)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = copy, Mode = SqliteOpenMode.ReadWrite, Pooling = false }.ToString();
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var columns = Columns(connection, "Note");
        if (!columns.Contains("Text")) return ImportPreview.Failed(ImportFailure.WrongFormat);

        var themeColumn = new[] { "Theme", "ThemeColor", "Color" }.FirstOrDefault(columns.Contains);
        var select = new List<string> { "Text" };
        foreach (var optional in new[] { "Id", "CreatedAt", "UpdatedAt", "DeletedAt" }.Concat(themeColumn is null ? [] : [themeColumn]))
            if (columns.Contains(optional)) select.Add(optional);

        var media = CountByParent(connection, "Media");
        var strokes = CountByParent(connection, "Stroke");
        var builder = new ImportPreviewBuilder();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT " + string.Join(", ", select.Select(c => "\"" + c + "\"")) + " FROM \"Note\";";
        using var reader = command.ExecuteReader();
        int index = 0;
        while (reader.Read() && !builder.Exhausted)
        {
            index++;
            string Get(string column) => select.Contains(column) && !reader.IsDBNull(select.IndexOf(column)) ? Convert.ToString(reader.GetValue(select.IndexOf(column)), System.Globalization.CultureInfo.InvariantCulture) ?? "" : "";

            var id = Get("Id");
            var name = id.Length > 0 ? id : $"#{index}";
            var rawText = Get("Text");
            if (rawText.Length > ImportLimits.MaxNoteChars * 2) { builder.Skip(name, ImportSkipReason.TooLarge); continue; }

            var text = Clean(rawText);
            if (text.Length == 0) { builder.Skip(name, ImportSkipReason.Empty); continue; }
            if (text.Length > ImportLimits.MaxNoteChars) { builder.Skip(name, ImportSkipReason.TooLarge); continue; }

            var deleted = Ticks(Get("DeletedAt"));
            int attachments = id.Length > 0 ? media.GetValueOrDefault(id) + strokes.GetValueOrDefault(id) : 0;
            builder.Add(new ImportedNote(text, themeColumn is null ? null : ColorOf(Get(themeColumn)), [],
                deleted is not null ? NoteState.Trashed : NoteState.Active, false, Ticks(Get("CreatedAt")), Ticks(Get("UpdatedAt"))), attachments);
        }

        return builder.Build();
    }

    private static ImportHue? ColorOf(string value) =>
        value.StartsWith('#') || value.Length == 8 && value.All(char.IsAsciiHexDigit) ? ImportHues.FromHex(value.Length == 8 ? "#" + value[2..] : value)
        : ImportHues.FromName(value);

    // Paragraphs are separated by CR and each starts with "\id=GUID ": drop the prefix, keep everything else (a backslash elsewhere is text).
    private static string Clean(string raw)
    {
        var lines = LineBreak().Split(raw).Select(l => IdPrefix().Replace(l, "").TrimEnd()).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        while (lines.Count > 0 && lines[0].Length == 0) lines.RemoveAt(0);
        return string.Join("\r\n", lines);
    }

    private static DateTimeOffset? Ticks(string value)
    {
        if (!long.TryParse(value, out var ticks) || ticks <= 0 || ticks > DateTime.MaxValue.Ticks) return null;
        return new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc));
    }

    private static HashSet<string> Columns(SqliteConnection connection, string table)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        using var reader = command.ExecuteReader();
        while (reader.Read()) set.Add(reader.GetString(1));
        return set;
    }

    private static Dictionary<string, int> CountByParent(SqliteConnection connection, string table)
    {
        var counts = new Dictionary<string, int>();
        if (!Columns(connection, table).Contains("ParentId")) return counts;
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT \"ParentId\", COUNT(*) FROM \"{table}\" WHERE \"ParentId\" IS NOT NULL GROUP BY \"ParentId\";";
        using var reader = command.ExecuteReader();
        while (reader.Read()) counts[reader.GetString(0)] = reader.GetInt32(1);
        return counts;
    }
}
