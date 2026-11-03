using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Aldune.Core.Importing;

/// <summary>
/// Google Keep through Google Takeout: a folder or a .zip with one .json per note (and, next to it, an .html twin and the attachments).
/// The JSON is the source of truth; an .html with no .json next to it is read as a best-effort fallback (no dates, state or pin).
/// Nothing is extracted to disk: zip entries are read in memory with a size cap.
/// </summary>
public sealed partial class KeepTakeoutSource : IImportSource
{
    /// <summary>A Keep note is text; 8 MB of JSON is far beyond any real one and bounds what a single entry can cost.</summary>
    private const int MaxEntryBytes = 8 * 1024 * 1024;

    [GeneratedRegex(@"<div[^>]*class=""title""[^>]*>(?<v>.*?)</div>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTitle();

    [GeneratedRegex(@"<div[^>]*class=""content""[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlContentStart();

    [GeneratedRegex(@"class=""label-name""[^>]*>(?<v>[^<]*)<", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLabel();

    [GeneratedRegex(@"class=""note (?<v>[A-Za-z]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlColor();

    private sealed record Entry(string Name, long Length, Func<byte[]?> Read);

    public ImportPreview Read(string path)
    {
        ZipArchive? zip = null;
        try
        {
            List<Entry> entries;
            if (Directory.Exists(path)) entries = FolderEntries(path);
            else if (File.Exists(path) && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                zip = ZipFile.OpenRead(path);
                entries = ZipEntries(zip);
            }
            else if (File.Exists(path)) entries = [FileEntry(path, Path.GetFileName(path))];
            else return ImportPreview.Failed(ImportFailure.NotFound);

            return Process(entries);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return ImportPreview.Failed(ImportFailure.Unreadable);
        }
        finally
        {
            zip?.Dispose();
        }
    }

    private static List<Entry> FolderEntries(string folder)
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = ImportLimits.MaxFolderDepth, IgnoreInaccessible = true };
        return Directory.EnumerateFiles(folder, "*", options)
            .Where(IsCandidate)
            .Take(ImportLimits.MaxFiles)
            .Select(file => FileEntry(file, Path.GetRelativePath(folder, file)))
            .ToList();
    }

    private static Entry FileEntry(string file, string name) =>
        new(name, new FileInfo(file).Length, () => File.ReadAllBytes(file));

    private static List<Entry> ZipEntries(ZipArchive zip)
    {
        var list = new List<Entry>();
        long total = 0;
        foreach (var entry in zip.Entries)
        {
            if (!IsCandidate(entry.FullName)) continue;
            if (list.Count >= ImportLimits.MaxFiles) break;
            // The declared size is checked up front and the real read is bounded again, because a crafted archive can lie.
            total += entry.Length;
            if (total > ImportLimits.MaxTotalBytes) break;
            var e = entry;
            list.Add(new Entry(entry.FullName, entry.Length, () => ReadBounded(e)));
        }

        return list;
    }

    private static byte[]? ReadBounded(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            if (buffer.Length + read > MaxEntryBytes) return null;
            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static bool IsCandidate(string name) =>
        name.EndsWith(".json", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".html", StringComparison.OrdinalIgnoreCase);

    private static ImportPreview Process(List<Entry> entries)
    {
        var builder = new ImportPreviewBuilder();
        var jsonStems = entries.Where(e => e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Select(e => Stem(e.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in entries)
        {
            if (builder.Exhausted) break;
            bool isJson = entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
            if (!isJson && jsonStems.Contains(Stem(entry.Name))) continue;

            if (entry.Length > MaxEntryBytes) { builder.Skip(entry.Name, ImportSkipReason.TooLarge); continue; }
            var bytes = entry.Read();
            if (bytes is null) { builder.Skip(entry.Name, ImportSkipReason.TooLarge); continue; }

            if (isJson) ReadJson(entry.Name, bytes, builder);
            else ReadHtml(entry.Name, bytes, builder);
        }

        return builder.Build();
    }

    private static string Stem(string name) => name[..name.LastIndexOf('.')];

    private static void ReadJson(string name, byte[] bytes, ImportPreviewBuilder builder)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16, AllowTrailingCommas = true });
        }
        catch (JsonException)
        {
            // Takeout also ships other JSON (labels, settings): anything that is not a readable note is simply not one.
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !LooksLikeNote(root)) return;

            var title = Str(root, "title");
            var body = new StringBuilder();
            var text = Str(root, "textContent");
            if (!string.IsNullOrWhiteSpace(text)) body.Append(ImportPreviewBuilder.Crlf(text));

            if (root.TryGetProperty("listContent", out var list) && list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    var itemText = Str(item, "text")?.Replace("\r", " ").Replace("\n", " ").Trim();
                    if (string.IsNullOrEmpty(itemText)) continue;
                    if (body.Length > 0) body.Append("\r\n");
                    bool done = item.TryGetProperty("isChecked", out var c) && c.ValueKind == JsonValueKind.True;
                    body.Append(done ? TaskLines.Checked : TaskLines.Unchecked).Append(' ').Append(itemText);
                }
            }

            var composed = ImportPreviewBuilder.Compose(title, body.ToString());
            if (composed is null) { builder.Skip(name, ImportSkipReason.Empty); return; }
            if (composed.Length > ImportLimits.MaxNoteChars) { builder.Skip(name, ImportSkipReason.TooLarge); return; }

            var tags = new List<string>();
            if (root.TryGetProperty("labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
                foreach (var label in labels.EnumerateArray())
                    if (Str(label, "name")?.Trim() is { Length: > 0 } labelName) tags.Add(labelName);

            int attachments = root.TryGetProperty("attachments", out var att) && att.ValueKind == JsonValueKind.Array ? att.GetArrayLength() : 0;
            var state = Flag(root, "isTrashed") ? NoteState.Trashed : Flag(root, "isArchived") ? NoteState.Archived : NoteState.Active;

            builder.Add(new ImportedNote(composed, ImportHues.FromName(Str(root, "color")), tags, state, Flag(root, "isPinned"),
                Usec(root, "createdTimestampUsec"), Usec(root, "userEditedTimestampUsec")), attachments);
        }
    }

    private static bool LooksLikeNote(JsonElement root) =>
        root.TryGetProperty("textContent", out _) || root.TryGetProperty("listContent", out _)
        || (root.TryGetProperty("title", out _) && root.TryGetProperty("userEditedTimestampUsec", out _));

    private static void ReadHtml(string name, byte[] bytes, ImportPreviewBuilder builder)
    {
        var html = Encoding.UTF8.GetString(bytes);
        var titleMatch = HtmlTitle().Match(html);
        var title = titleMatch.Success ? HtmlText.ToNoteText(titleMatch.Groups["v"].Value) : null;
        var content = HtmlContentStart().Match(html);
        var body = content.Success ? HtmlText.ToNoteText(html[(content.Index + content.Length)..]) : null;

        // Only Keep's own pages have a title or content block; any other HTML in the archive is not a note.
        if (!titleMatch.Success && !content.Success) return;

        var composed = ImportPreviewBuilder.Compose(title, body);
        if (composed is null) { builder.Skip(name, ImportSkipReason.Empty); return; }
        if (composed.Length > ImportLimits.MaxNoteChars) { builder.Skip(name, ImportSkipReason.TooLarge); return; }

        var tags = HtmlLabel().Matches(html).Select(m => m.Groups["v"].Value.Trim()).Where(t => t.Length > 0).ToList();
        builder.Add(new ImportedNote(composed, ImportHues.FromName(HtmlColor().Match(html) is { Success: true } m ? m.Groups["v"].Value : null),
            tags, NoteState.Active, false, null, null));
    }

    private static string? Str(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static bool Flag(JsonElement element, string property) =>
        element.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.True;

    // Keep stamps are microseconds since the Unix epoch.
    private static DateTimeOffset? Usec(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var v) || !v.TryGetInt64(out var usec) || usec <= 0) return null;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(usec / 1000); }
        catch (ArgumentOutOfRangeException) { return null; }
    }
}
