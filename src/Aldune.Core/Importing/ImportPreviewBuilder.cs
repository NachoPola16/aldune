namespace Aldune.Core.Importing;

/// <summary>Collects what a source finds, enforcing the note limit in one place.</summary>
internal sealed class ImportPreviewBuilder
{
    private readonly List<ImportedNote> _notes = [];
    private readonly List<ImportSkip> _skipped = [];
    private int _attachments;
    private int _withAttachments;

    public int Count => _notes.Count;

    public bool Full => _notes.Count >= ImportLimits.MaxNotes;

    /// <summary>Past this many notes in total the source stops reading altogether (the rest are not even listed).</summary>
    public bool Exhausted => _notes.Count + _skipped.Count >= ImportLimits.MaxNotes + 1000;

    public void Add(ImportedNote note, int attachments = 0)
    {
        if (Full) { Skip("", ImportSkipReason.LimitReached); return; }
        _notes.Add(note);
        if (attachments <= 0) return;
        _attachments += attachments;
        _withAttachments++;
    }

    public void Skip(string name, ImportSkipReason reason) => _skipped.Add(new ImportSkip(name, reason));

    public ImportPreview Build() => new()
    {
        Notes = _notes,
        Skipped = _skipped,
        AttachmentsIgnored = _attachments,
        NotesWithAttachments = _withAttachments,
        Failure = _notes.Count == 0 && _skipped.Count == 0 ? ImportFailure.NothingFound : ImportFailure.None,
    };

    /// <summary>"Title, blank-free body" as the text of a note; null when there is nothing to import.</summary>
    public static string? Compose(string? title, string? body)
    {
        title = title?.Trim();
        body = body?.Trim('\r', '\n');
        var hasTitle = !string.IsNullOrWhiteSpace(title);
        var hasBody = !string.IsNullOrWhiteSpace(body);
        if (!hasTitle && !hasBody) return null;
        if (!hasBody) return title;
        return hasTitle ? title + "\r\n" + body : body;
    }

    public static string Crlf(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n').Replace("\n", "\r\n");
}
