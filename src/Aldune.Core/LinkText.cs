namespace Aldune.Core;

/// <summary>Where a link sits in a note's text: <see cref="Start"/> and <see cref="Length"/> are character indexes.</summary>
public readonly record struct LinkSpan(int Start, int Length);

/// <summary>
/// Clickable web links inside the plain text of a note.
///
/// Links are not a markup of their own: the note keeps being plain text (so sync, search, export and the dock preview are
/// untouched), and a run that starts with <c>http://</c> or <c>https://</c> is simply recognised when the user Ctrl+clicks it.
/// Nothing is ever opened by itself, and only those two schemes qualify: never <c>file:</c>, UNC paths, <c>javascript:</c> or
/// anything else that could run code or reach a local resource. A URL with user info (<c>https://good.com@evil.tld</c>) is
/// rejected too, because it is the classic way to make a link look like it points somewhere else.
/// </summary>
public static class LinkText
{
    /// <summary>A run longer than this is not a link someone typed; it keeps the matcher cheap on pasted blobs.</summary>
    public const int MaxLength = 2048;

    /// <summary>The link that contains <paramref name="index"/>, or null.</summary>
    public static LinkSpan? FindAt(string text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length || IsEnd(text[index])) return null;

        // The run of non-space characters around the index (bounded: MaxLength each way).
        int start = index;
        while (start > 0 && !IsEnd(text[start - 1]) && index - start <= MaxLength) start--;
        int end = index + 1;
        while (end < text.Length && !IsEnd(text[end]) && end - start <= MaxLength) end++;
        if (end - start > MaxLength) return null;

        // A link can start inside the run (after an opening bracket or a quote).
        int schemeAt = IndexOfScheme(text, start, end);
        while (schemeAt >= 0)
        {
            int linkEnd = TrimTrailing(text, schemeAt, end);
            if (index >= schemeAt && index < linkEnd && TryGetUri(text.Substring(schemeAt, linkEnd - schemeAt), out _))
                return new LinkSpan(schemeAt, linkEnd - schemeAt);
            if (index < schemeAt) return null;
            schemeAt = IndexOfScheme(text, schemeAt + 1, end);
        }
        return null;
    }

    /// <summary>True for an absolute http(s) address without user info and short enough; the only thing the app ever opens.</summary>
    public static bool TryGetUri(string candidate, out Uri? uri)
    {
        uri = null;
        if (candidate.Length > MaxLength) return false;
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)) return false;
        if (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps) return false;
        if (parsed.Host.Length == 0 || parsed.UserInfo.Length > 0) return false;
        uri = parsed;
        return true;
    }

    // Whitespace ends a link, and so do the task glyphs (a checkbox is never part of an address).
    private static bool IsEnd(char c) => char.IsWhiteSpace(c) || c == '☐' || c == '☑' || c == '☒';

    private static int IndexOfScheme(string text, int from, int to)
    {
        for (int i = from; i < to; i++)
        {
            if (text[i] != 'h' && text[i] != 'H') continue;
            if (Matches(text, i, to, "https://") || Matches(text, i, to, "http://")) return i;
        }
        return -1;
    }

    private static bool Matches(string text, int at, int to, string scheme) =>
        at + scheme.Length < to && string.Compare(text, at, scheme, 0, scheme.Length, StringComparison.OrdinalIgnoreCase) == 0;

    /// <summary>Drops sentence punctuation and a closing bracket/quote that has no opening one inside the link.</summary>
    private static int TrimTrailing(string text, int start, int end)
    {
        while (end > start)
        {
            char c = text[end - 1];
            if (c is '.' or ',' or ';' or ':' or '!' or '?' or '\'' or '"' or '»' or '”' or '’') { end--; continue; }
            if (c is ')' or ']' or '}')
            {
                char open = c == ')' ? '(' : c == ']' ? '[' : '{';
                int opens = 0, closes = 0;
                for (int i = start; i < end; i++) { if (text[i] == open) opens++; else if (text[i] == c) closes++; }
                if (closes > opens) { end--; continue; }
            }
            break;
        }
        return end;
    }
}
