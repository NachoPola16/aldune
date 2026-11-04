namespace Aldune.Core;

/// <summary>
/// One row of the quick-find palette. <see cref="Title"/> is empty for a locked note without a visible name: Core has no
/// translations, so the UI names it «Protected note».
/// </summary>
public sealed record QuickFindHit(Note Note, string Title, string Snippet, IReadOnlyList<string> Tags, bool IsLocked);

/// <summary>
/// Filtering and ranking for the global quick-find palette, kept apart from the window so it can be tested: the palette must not be able
/// to list, rank or copy something its rules would not.
/// </summary>
public static class QuickFind
{
    public const int DefaultLimit = 30;

    // The body is only scanned this far: the tolerant matcher allocates per word, and a note pasted from a log can be megabytes.
    private const int BodyScanLimit = 4000;
    private const int SnippetLimit = 200;

    /// <summary>
    /// The active notes that answer <paramref name="query"/>, best first. Ranking is by tier and then by recency: a title that contains the
    /// query, then a title that matches with a typo or in another word order, then a body or tag match. A blank query lists the most
    /// recent notes. Locked notes take part only through their visible name, never through their (empty) text or their tags.
    /// </summary>
    public static IReadOnlyList<QuickFindHit> Search(IEnumerable<Note> notes, string? query, int limit = DefaultLimit)
    {
        string q = (query ?? "").Trim();
        var ranked = new List<(QuickFindHit Hit, int Tier, DateTimeOffset At)>();

        foreach (var note in notes)
        {
            if (note.State != NoteState.Active) continue;

            bool locked = note.IsProtected;
            string title = locked ? ProtectedTitleRules.Visible(note) ?? "" : NoteTitleHelper.GetTitle(note.Text);
            IReadOnlyList<string> tags = locked ? [] : note.Tags;
            string usualSnippet = locked ? "" : Clip(NoteTitleHelper.GetTabPreview(note.Text));

            int tier;
            string snippet = usualSnippet;
            if (q.Length == 0 || (title.Length > 0 && NoteSearch.Matches(title, q))) tier = 0;
            else if (title.Length > 0 && FuzzySearch.Score(q, title) >= FuzzySearch.MinimumScore) tier = 1;
            else if (!locked && BodyOrTagMatch(note, q, out var line))
            {
                tier = 2;
                snippet = line ?? usualSnippet;
            }
            else continue;

            ranked.Add((new QuickFindHit(note, title, snippet, tags, locked), tier, note.UpdatedAt));
        }

        return ranked
            .OrderBy(r => r.Tier)
            .ThenByDescending(r => r.At)
            .Take(Math.Max(0, limit))
            .Select(r => r.Hit)
            .ToList();
    }

    /// <summary>
    /// What Ctrl+Enter puts on the clipboard: the note's text, or null for any protected note. Even one unlocked in memory is refused:
    /// the palette is reachable from any program, so a copy there would put content the person locked on a clipboard that history
    /// tools read.
    /// </summary>
    public static string? CopyText(Note note) => note.IsProtected ? null : note.Text;

    private static bool BodyOrTagMatch(Note note, string query, out string? matchingLine)
    {
        matchingLine = null;
        string body = note.Text.Length > BodyScanLimit ? note.Text[..BodyScanLimit] : note.Text;

        // Show the line that matched, not the start of the note: that is the reason the note is in the list.
        foreach (var rawLine in body.Split('\n'))
        {
            if (!NoteSearch.Matches(rawLine, query)) continue;
            string line = Collapse(rawLine);
            if (line.Length == 0) continue;
            matchingLine = Clip(line);
            return true;
        }

        if (note.Tags.Any(tag => NoteSearch.Matches(tag, query))) return true;
        return FuzzySearch.Score(query, body) >= FuzzySearch.MinimumScore
            || FuzzySearch.Score(query, string.Join(' ', note.Tags)) >= FuzzySearch.MinimumScore;
    }

    private static string Collapse(string line)
    {
        var parts = line.Replace(TaskLines.Unchecked.ToString(), "").Replace(TaskLines.Checked.ToString(), "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static string Clip(string text) => text.Length <= SnippetLimit ? text : text[..SnippetLimit].TrimEnd() + "…";
}
