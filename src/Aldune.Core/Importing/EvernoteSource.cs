using System.Globalization;
using System.Text;
using System.Xml;

namespace Aldune.Core.Importing;

/// <summary>
/// Evernote's .enex export. The XML is read as a stream (attachments, which can be huge base64, are skipped without being loaded) with the DTD
/// ignored and no resolver, so a hostile file cannot read local files or fetch anything. The note body is ENML inside a CDATA block; it is
/// converted with <see cref="HtmlText"/>, which never parses it as XML.
/// </summary>
public sealed class EvernoteSource : IImportSource
{
    public ImportPreview Read(string path)
    {
        if (!File.Exists(path)) return ImportPreview.Failed(ImportFailure.NotFound);
        try
        {
            if (new FileInfo(path).Length > ImportLimits.MaxEnexBytes) return ImportPreview.Failed(ImportFailure.TooLarge);

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            // Real exports start with a DOCTYPE that points at Evernote's DTD, so it cannot be prohibited; Ignore skips it without ever resolving
            // it, and entities it would define stay undefined (an &x; in the text is an error, not a file read).
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
                MaxCharactersInDocument = ImportLimits.MaxEnexBytes,
                MaxCharactersFromEntities = 0,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            var builder = new ImportPreviewBuilder();
            bool sawRoot = false;
            try
            {
                using var reader = XmlReader.Create(stream, settings);
                reader.MoveToContent();
                if (reader.NodeType != XmlNodeType.Element || reader.Name != "en-export") return ImportPreview.Failed(ImportFailure.WrongFormat);
                sawRoot = true;

                reader.Read();
                while (!reader.EOF && !builder.Exhausted)
                {
                    if (reader.NodeType == XmlNodeType.Element && reader.Name == "note") ReadNote(reader, builder);
                    else reader.Read();
                }
            }
            catch (XmlException)
            {
                if (builder.Count == 0) return ImportPreview.Failed(sawRoot ? ImportFailure.Unreadable : ImportFailure.WrongFormat);
                builder.Skip("", ImportSkipReason.Unreadable);
            }

            return builder.Build();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ImportPreview.Failed(ImportFailure.Unreadable);
        }
    }

    private static void ReadNote(XmlReader outer, ImportPreviewBuilder builder)
    {
        string? title = null, enml = null;
        bool tooBig = false;
        DateTimeOffset? created = null, updated = null;
        var tags = new List<string>();
        int resources = 0;

        using (var note = outer.ReadSubtree())
        {
            note.Read();
            note.Read();
            while (!note.EOF)
            {
                if (note.NodeType != XmlNodeType.Element || note.Depth != 1) { note.Read(); continue; }
                switch (note.Name)
                {
                    case "title": title = note.ReadElementContentAsString(); break;
                    case "content": enml = ReadBounded(note, ImportLimits.MaxNoteChars * 2, out tooBig); break;
                    case "created": created = ParseDate(note.ReadElementContentAsString()); break;
                    case "updated": updated = ParseDate(note.ReadElementContentAsString()); break;
                    case "tag":
                        if (note.ReadElementContentAsString().Trim() is { Length: > 0 } tag) tags.Add(tag);
                        break;
                    case "resource": resources++; note.Skip(); break;
                    default: note.Skip(); break;
                }
            }
        }

        // ReadSubtree leaves the outer reader on the closing tag of the note.
        outer.Read();

        var name = string.IsNullOrWhiteSpace(title) ? "(untitled)" : title.Trim();
        if (tooBig) { builder.Skip(name, ImportSkipReason.TooLarge); return; }

        var body = enml is null ? "" : HtmlText.ToNoteText(enml);
        var composed = ImportPreviewBuilder.Compose(title, body);
        if (composed is null) { builder.Skip(name, ImportSkipReason.Empty); return; }
        if (composed.Length > ImportLimits.MaxNoteChars) { builder.Skip(name, ImportSkipReason.TooLarge); return; }

        builder.Add(new ImportedNote(composed, null, tags, NoteState.Active, false, created, updated ?? created), resources);
    }

    // The text of an element read in chunks, so one enormous note cannot take the memory of the whole file.
    private static string ReadBounded(XmlReader reader, int max, out bool over)
    {
        over = false;
        var sb = new StringBuilder();
        if (reader.IsEmptyElement) { reader.Read(); return ""; }

        reader.Read();
        var chunk = new char[4096];
        while (reader.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.Whitespace or XmlNodeType.SignificantWhitespace)
        {
            int read;
            while ((read = reader.ReadValueChunk(chunk, 0, chunk.Length)) > 0)
            {
                if (sb.Length + read > max) over = true;
                else sb.Append(chunk, 0, read);
            }

            reader.Read();
        }

        // Now on the end tag of the element.
        reader.Read();
        return sb.ToString();
    }

    private static DateTimeOffset? ParseDate(string value) =>
        DateTimeOffset.TryParseExact(value.Trim(), "yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date) ? date : null;
}
