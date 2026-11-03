namespace Aldune.Core.Importing;

/// <summary>The colour family a note had in the app it comes from. It is a hue, not a hex: the importer picks the closest colour of the
/// active note theme, so an imported note never carries a loose colour that no theme has.</summary>
public enum ImportHue { Yellow, Green, Cyan, Blue, Pink, Orange }

/// <summary>One note read from another app, in the common shape every <see cref="IImportSource"/> produces.</summary>
/// <param name="Text">Aldune note text (first line is the title), CRLF line breaks.</param>
/// <param name="Hue">The colour family, or null when the source has none or it is a neutral (grey, white, "default").</param>
/// <param name="CreatedAt">Original dates when the source has them; null otherwise.</param>
public sealed record ImportedNote(
    string Text,
    ImportHue? Hue,
    IReadOnlyList<string> Tags,
    NoteState State,
    bool Pinned,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? UpdatedAt);

public enum ImportSkipReason
{
    Empty,
    Protected,
    TooLarge,
    NotText,
    Unreadable,
    LimitReached,
}

/// <summary>A note (or file) that will not be imported, with the reason, so the preview can say why.</summary>
public sealed record ImportSkip(string Name, ImportSkipReason Reason);

public enum ImportFailure
{
    None,
    NotFound,
    TooLarge,
    Unreadable,
    WrongFormat,
    NothingFound,
}

/// <summary>What a source found, without writing anything.</summary>
public sealed class ImportPreview
{
    public IReadOnlyList<ImportedNote> Notes { get; init; } = [];
    public IReadOnlyList<ImportSkip> Skipped { get; init; } = [];

    /// <summary>Images, audio, files and drawings that are not imported (Aldune is text only).</summary>
    public int AttachmentsIgnored { get; init; }

    /// <summary>How many of the importable notes had at least one attachment.</summary>
    public int NotesWithAttachments { get; init; }

    public ImportFailure Failure { get; init; }

    public static ImportPreview Failed(ImportFailure failure) => new() { Failure = failure };
}

/// <summary>A place notes can be imported from. <see cref="Read"/> only reads: nothing is written to Aldune.</summary>
public interface IImportSource
{
    /// <param name="path">A file or a folder, depending on the source.</param>
    ImportPreview Read(string path);
}

/// <summary>The size limits that keep an import from exhausting memory or disk.</summary>
public static class ImportLimits
{
    public const int MaxNotes = 5000;
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const long MaxDatabaseBytes = 256L * 1024 * 1024;
    public const long MaxEnexBytes = 1024L * 1024 * 1024;
    public const long MaxTotalBytes = 512L * 1024 * 1024;
    public const int MaxNoteChars = 1024 * 1024;
    public const int MaxFiles = 20000;
    public const int MaxFolderDepth = 6;
}
