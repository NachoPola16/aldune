namespace Aldune.Core.Importing;

/// <summary>
/// A folder of .md / .markdown / .txt files (or one such file): each file is a note. Reading uses the same limits and encodings as linked
/// files (<see cref="LinkedFileFormat"/>), so a binary or an ANSI file is refused instead of imported as garbage. A file without a
/// "# heading" is titled with its name.
/// </summary>
public sealed class TextFolderSource : IImportSource
{
    public ImportPreview Read(string path)
    {
        try
        {
            List<string> files;
            string root;
            if (Directory.Exists(path))
            {
                root = path;
                var options = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = ImportLimits.MaxFolderDepth, IgnoreInaccessible = true };
                files = Directory.EnumerateFiles(path, "*", options).Where(LinkedFileFormat.IsSupportedExtension).Take(ImportLimits.MaxFiles).ToList();
            }
            else if (File.Exists(path))
            {
                root = Path.GetDirectoryName(path) ?? "";
                files = [path];
            }
            else return ImportPreview.Failed(ImportFailure.NotFound);

            var builder = new ImportPreviewBuilder();
            long total = 0;
            foreach (var file in files)
            {
                if (builder.Exhausted) break;
                var name = Path.GetRelativePath(root, file);
                var info = new FileInfo(file);
                if (info.Length > LinkedFileFormat.MaxBytes) { builder.Skip(name, ImportSkipReason.TooLarge); continue; }
                total += info.Length;
                if (total > ImportLimits.MaxTotalBytes) { builder.Skip(name, ImportSkipReason.LimitReached); break; }

                byte[] bytes;
                try { bytes = File.ReadAllBytes(file); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { builder.Skip(name, ImportSkipReason.Unreadable); continue; }

                var (content, rejection) = LinkedFileFormat.Decode(bytes);
                if (content is null)
                {
                    builder.Skip(name, rejection == LinkedFileRejection.TooLarge ? ImportSkipReason.TooLarge : ImportSkipReason.NotText);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(content.Text)) { builder.Skip(name, ImportSkipReason.Empty); continue; }

                var markdown = content.Text.TrimStart();
                var text = MarkdownImport.ToNoteText(markdown).TrimEnd();
                if (!markdown.StartsWith("# ", StringComparison.Ordinal)) text = Path.GetFileNameWithoutExtension(file) + "\r\n" + text;

                var modified = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero);
                var born = new DateTimeOffset(info.CreationTimeUtc, TimeSpan.Zero);
                builder.Add(new ImportedNote(text, null, [], NoteState.Active, false, born < modified ? born : modified, modified));
            }

            return builder.Build();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ImportPreview.Failed(ImportFailure.Unreadable);
        }
    }
}
