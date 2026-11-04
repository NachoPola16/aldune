namespace Aldune.Core;

[Flags]
public enum MarkdownStyle
{
    None = 0,
    /// <summary>Syntax characters (the stars, the hashes, the brackets and the URL of a link): drawn dimmed, never removed, so the text stays what was typed.</summary>
    Marker = 1,
    Bold = 2,
    Italic = 4,
    Strike = 8,
    Code = 16,
    Link = 32,
    Heading = 64,
}

/// <summary>A stretch of a note and how to draw it. The spans of one text are contiguous, in order, and cover all of it.</summary>
public readonly record struct MarkdownSpan(int Start, int Length, MarkdownStyle Style);

/// <summary>
/// The scanner behind live Markdown: it only says which stretches of the text to draw bold, italic and so on, and never changes the text.
/// The editor stays a plain text box (a note is still plain text for sync, search and linked files); a layer on top draws these spans.
/// That layer cannot change glyph widths without moving the caret, so everything here is meant to be drawn with the same advance
/// widths: emphasis is a style of the same glyphs, not another font size.
/// </summary>
public static class MarkdownLive
{
    // Past this a line is left plain: looking for a closing delimiter is quadratic in the worst case, and a line this long is a pasted log, not prose.
    private const int MaxLineLength = 4000;
    private const int MaxDepth = 4;

    public static IReadOnlyList<MarkdownSpan> Parse(string? text)
    {
        var spans = new List<MarkdownSpan>();
        if (string.IsNullOrEmpty(text)) return spans;

        bool inFence = false;
        int pos = 0;
        while (pos < text.Length)
        {
            int newline = text.IndexOf('\n', pos);
            int lineEnd = newline < 0 ? text.Length : newline;
            int next = newline < 0 ? text.Length : newline + 1;
            // A CRLF note: the \r belongs to the terminator, not to the line (it must not count as a trailing space of a delimiter).
            int contentEnd = lineEnd > pos && text[lineEnd - 1] == '\r' ? lineEnd - 1 : lineEnd;

            bool fence = IsFence(text, pos, contentEnd);
            if (fence)
            {
                Add(spans, pos, contentEnd - pos, MarkdownStyle.Marker | MarkdownStyle.Code);
                Add(spans, contentEnd, next - contentEnd, MarkdownStyle.Code);
                inFence = !inFence;
            }
            else if (inFence)
            {
                Add(spans, pos, next - pos, MarkdownStyle.Code);
            }
            else
            {
                Line(text, pos, contentEnd, spans);
                Add(spans, contentEnd, next - contentEnd, MarkdownStyle.None);
            }
            pos = next;
        }
        return spans;
    }

    /// <summary>
    /// The address of the <c>[text](url)</c> link that contains <paramref name="index"/> (on its text, its brackets or its address), or null.
    /// Code and escaped brackets are not links, as in <see cref="Parse"/>. The caller still has to check that it is an http(s) address
    /// before opening it: this only says what the note spells.
    /// </summary>
    public static string? LinkUrlAt(string? text, int index)
    {
        if (string.IsNullOrEmpty(text) || index < 0 || index >= text.Length) return null;

        int lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        int lineEnd = text.IndexOf('\n', index);
        if (lineEnd < 0) lineEnd = text.Length;
        if (lineEnd - lineStart > MaxLineLength) return null;

        // Only the opening bracket can start a link, and none can start after the index.
        IReadOnlyList<MarkdownSpan>? spans = null;
        for (int open = text.LastIndexOf('[', index, index - lineStart + 1); open >= lineStart; open = open == lineStart ? -1 : text.LastIndexOf('[', open - 1, open - lineStart))
        {
            if (!TryLink(text, open, lineEnd, out int textEnd, out int linkEnd)) continue;
            if (index >= linkEnd) continue;

            spans ??= Parse(text);
            if (IsEscaped(text, open) || InCode(spans, open)) return null;
            int urlStart = text.IndexOf('(', textEnd) + 1;
            return text.Substring(urlStart, linkEnd - 1 - urlStart);
        }
        return null;
    }

    private static bool IsEscaped(string t, int i)
    {
        int slashes = 0;
        while (i - 1 - slashes >= 0 && t[i - 1 - slashes] == '\\') slashes++;
        return slashes % 2 == 1;
    }

    private static bool InCode(IReadOnlyList<MarkdownSpan> spans, int at)
    {
        foreach (var span in spans)
            if (at >= span.Start && at < span.Start + span.Length) return span.Style.HasFlag(MarkdownStyle.Code) && !span.Style.HasFlag(MarkdownStyle.Marker) || span.Style == (MarkdownStyle.Marker | MarkdownStyle.Code);
        return false;
    }

    private static bool IsFence(string t, int start, int end) => end - start >= 3 && t[start] == '`' && t[start + 1] == '`' && t[start + 2] == '`';

    private static void Line(string t, int start, int end, List<MarkdownSpan> o)
    {
        if (end <= start) return;
        if (end - start > MaxLineLength) { Add(o, start, end - start, MarkdownStyle.None); return; }

        int hashes = 0;
        while (start + hashes < end && t[start + hashes] == '#') hashes++;
        if (hashes is >= 1 and <= 6 && start + hashes < end && t[start + hashes] == ' ')
        {
            Add(o, start, hashes + 1, MarkdownStyle.Marker);
            Inline(t, start + hashes + 1, end, MarkdownStyle.Bold | MarkdownStyle.Heading, o, 0);
            return;
        }
        Inline(t, start, end, MarkdownStyle.None, o, 0);
    }

    private static void Inline(string t, int start, int end, MarkdownStyle style, List<MarkdownSpan> o, int depth)
    {
        int plainFrom = start;
        int i = start;
        while (i < end)
        {
            char c = t[i];

            if (c == '\\' && i + 1 < end && IsEscapable(t[i + 1]))
            {
                Flush(o, plainFrom, i, style);
                Add(o, i, 1, MarkdownStyle.Marker);
                plainFrom = i + 1;          // the escaped character is plain text again
                i += 2;
                continue;
            }

            if (c == '`')
            {
                int n = Run(t, i, end);
                int close = FindExactRun(t, '`', n, i + n, end);
                if (close > i + n)
                {
                    Flush(o, plainFrom, i, style);
                    Add(o, i, n, MarkdownStyle.Marker);
                    Add(o, i + n, close - i - n, style | MarkdownStyle.Code);
                    Add(o, close, n, MarkdownStyle.Marker);
                    i = plainFrom = close + n;
                }
                else i += n;
                continue;
            }

            if (c == '[' && depth < MaxDepth && TryLink(t, i, end, out int textEnd, out int linkEnd))
            {
                Flush(o, plainFrom, i, style);
                Add(o, i, 1, MarkdownStyle.Marker);
                Inline(t, i + 1, textEnd, style | MarkdownStyle.Link, o, depth + 1);
                Add(o, textEnd, linkEnd - textEnd, MarkdownStyle.Marker);
                i = plainFrom = linkEnd;
                continue;
            }

            if (c is '*' or '_' or '~')
            {
                int n = Run(t, i, end);
                var added = c == '~' ? (n == 2 ? MarkdownStyle.Strike : MarkdownStyle.None)
                    : n switch { 1 => MarkdownStyle.Italic, 2 => MarkdownStyle.Bold, 3 => MarkdownStyle.Bold | MarkdownStyle.Italic, _ => MarkdownStyle.None };

                if (added != MarkdownStyle.None && depth < MaxDepth && CanOpen(t, c, i, n, end) && FindCloser(t, c, n, i + n, end) is var close && close > 0)
                {
                    Flush(o, plainFrom, i, style);
                    Add(o, i, n, MarkdownStyle.Marker);
                    Inline(t, i + n, close, style | added, o, depth + 1);
                    Add(o, close, n, MarkdownStyle.Marker);
                    i = plainFrom = close + n;
                }
                else i += n;
                continue;
            }

            i++;
        }
        Flush(o, plainFrom, end, style);
    }

    // ASCII punctuation and symbols, as in CommonMark: the backtick and the tilde are symbols for .NET, not punctuation.
    private static bool IsEscapable(char c) => c < 128 && (char.IsPunctuation(c) || char.IsSymbol(c));

    private static int Run(string t, int i, int end)
    {
        int n = 1;
        while (i + n < end && t[i + n] == t[i]) n++;
        return n;
    }

    private static int FindExactRun(string t, char c, int n, int from, int end)
    {
        for (int p = from; p < end;)
        {
            if (t[p] != c) { p++; continue; }
            int m = Run(t, p, end);
            if (m == n) return p;
            p += m;
        }
        return -1;
    }

    // An opener is followed by something visible; an underscore inside a word (snake_case) is not emphasis.
    private static bool CanOpen(string t, char c, int i, int n, int end)
    {
        if (i + n >= end || char.IsWhiteSpace(t[i + n])) return false;
        return c != '_' || i == 0 || !char.IsLetterOrDigit(t[i - 1]);
    }

    // The closer matches the opener's run length exactly, follows something visible and, for an underscore, does not touch a letter.
    private static int FindCloser(string t, char c, int n, int from, int end)
    {
        for (int p = from; p < end;)
        {
            if (t[p] == '\\') { p += 2; continue; }
            if (t[p] == '`')
            {
                // Skip a code span: its stars are literal.
                int m2 = Run(t, p, end);
                int close = FindExactRun(t, '`', m2, p + m2, end);
                p = close > 0 ? close + m2 : p + m2;
                continue;
            }
            if (t[p] != c) { p++; continue; }

            int m = Run(t, p, end);
            if (m == n && p > from && !char.IsWhiteSpace(t[p - 1])
                && (c != '_' || p + m >= end || !char.IsLetterOrDigit(t[p + m])))
                return p;
            p += m;
        }
        return -1;
    }

    // [text](url): no brackets inside the text, no whitespace in the url.
    private static bool TryLink(string t, int i, int end, out int textEnd, out int linkEnd)
    {
        textEnd = linkEnd = -1;
        int j = i + 1;
        while (j < end && t[j] != ']' && t[j] != '[') j++;
        if (j >= end || t[j] != ']' || j == i + 1 || j + 2 >= end || t[j + 1] != '(') return false;

        int k = j + 2;
        while (k < end && t[k] != ')' && !char.IsWhiteSpace(t[k])) k++;
        if (k >= end || t[k] != ')' || k == j + 2) return false;

        textEnd = j;
        linkEnd = k + 1;
        return true;
    }

    private static void Flush(List<MarkdownSpan> o, int from, int to, MarkdownStyle style)
    {
        if (to > from) Add(o, from, to - from, style);
    }

    // Adjacent stretches with the same style are one span: fewer pieces for the layer to draw.
    private static void Add(List<MarkdownSpan> o, int start, int length, MarkdownStyle style)
    {
        if (length <= 0) return;
        if (o.Count > 0 && o[^1].Style == style && o[^1].Start + o[^1].Length == start)
            o[^1] = o[^1] with { Length = o[^1].Length + length };
        else o.Add(new MarkdownSpan(start, length, style));
    }
}
