namespace Aldune.Core;

public sealed record LineMergeResult(string Text, bool Conflict);

/// <summary>
/// Three-way merge of a text by lines (diff3). Both sides are diffed against the common base; changes that do not
/// overlap are applied together, an identical change on both sides counts once, and two insertions at the same
/// spot are both kept in ordinal order so the result does not depend on which side is "local". Only overlapping,
/// different changes are a conflict. Line endings are normalized to <c>\n</c>.
/// </summary>
public static class LineMerge
{
    // Above this many cells the quadratic diff is skipped and the middle is treated as one replaced block
    // (it can only produce a conflict, never a wrong merge).
    private const long MaxDiffCells = 4_000_000;

    private sealed record Hunk(int Start, int End, string[] Lines)
    {
        public bool IsInsert => Start == End;
        public bool SameAs(Hunk other) => Start == other.Start && End == other.End && Lines.AsSpan().SequenceEqual(other.Lines);
    }

    public static LineMergeResult Merge(string baseText, string localText, string remoteText)
    {
        var baseLines = Split(baseText);
        var local = Diff(baseLines, Split(localText));
        var remote = Diff(baseLines, Split(remoteText));

        var output = new List<string>();
        int position = 0, i = 0, j = 0;

        void CopyBaseUpTo(int end)
        {
            for (; position < end; position++) output.Add(baseLines[position]);
        }

        while (i < local.Count || j < remote.Count)
        {
            var l = i < local.Count ? local[i] : null;
            var r = j < remote.Count ? remote[j] : null;

            if (l is not null && r is not null)
            {
                if (l.IsInsert && r.IsInsert && l.Start == r.Start)
                {
                    CopyBaseUpTo(l.Start);
                    if (l.SameAs(r)) output.AddRange(l.Lines);
                    else
                    {
                        var first = string.CompareOrdinal(string.Join("\n", l.Lines), string.Join("\n", r.Lines)) <= 0 ? l : r;
                        var second = ReferenceEquals(first, l) ? r : l;
                        output.AddRange(first.Lines);
                        output.AddRange(second.Lines);
                    }
                    i++; j++;
                    continue;
                }
                if (Overlap(l, r))
                {
                    if (!l.SameAs(r)) return new LineMergeResult(string.Join("\n", Split(localText)), true);
                    CopyBaseUpTo(l.Start);
                    output.AddRange(l.Lines);
                    position = l.End;
                    i++; j++;
                    continue;
                }
            }

            // The one that starts first goes first; at the same start an insertion goes before a replacement.
            bool takeLocal = r is null || (l is not null && (l.Start < r.Start || (l.Start == r.Start && l.IsInsert)));
            var next = takeLocal ? l! : r!;
            CopyBaseUpTo(next.Start);
            output.AddRange(next.Lines);
            position = next.End;
            if (takeLocal) i++; else j++;
        }

        CopyBaseUpTo(baseLines.Length);
        return new LineMergeResult(string.Join("\n", output), false);
    }

    private static bool Overlap(Hunk a, Hunk b)
    {
        if (a.IsInsert && b.IsInsert) return a.Start == b.Start;
        if (a.IsInsert) return b.Start < a.Start && a.Start < b.End;
        if (b.IsInsert) return a.Start < b.Start && b.Start < a.End;
        return a.Start < b.End && b.Start < a.End;
    }

    private static string[] Split(string text) => text.Replace("\r\n", "\n").Split('\n');

    /// <summary>The runs of base lines (possibly empty) that became other lines.</summary>
    private static List<Hunk> Diff(string[] baseLines, string[] other)
    {
        var hunks = new List<Hunk>();
        int prefix = 0;
        while (prefix < baseLines.Length && prefix < other.Length && baseLines[prefix] == other[prefix]) prefix++;
        int suffix = 0;
        while (suffix < baseLines.Length - prefix && suffix < other.Length - prefix
               && baseLines[baseLines.Length - 1 - suffix] == other[other.Length - 1 - suffix]) suffix++;

        int n = baseLines.Length - prefix - suffix;
        int m = other.Length - prefix - suffix;
        if (n == 0 && m == 0) return hunks;
        if (n == 0 || m == 0 || (long)n * m > MaxDiffCells)
        {
            hunks.Add(new Hunk(prefix, prefix + n, other[prefix..(prefix + m)]));
            return hunks;
        }

        // Longest common subsequence of the differing middle.
        var table = new int[n + 1, m + 1];
        for (int a = n - 1; a >= 0; a--)
            for (int b = m - 1; b >= 0; b--)
                table[a, b] = baseLines[prefix + a] == other[prefix + b]
                    ? table[a + 1, b + 1] + 1
                    : Math.Max(table[a + 1, b], table[a, b + 1]);

        int x = 0, y = 0, runBase = 0, runOther = 0;
        bool open = false;
        void Close(int baseEnd, int otherEnd)
        {
            if (!open) return;
            hunks.Add(new Hunk(prefix + runBase, prefix + baseEnd, other[(prefix + runOther)..(prefix + otherEnd)]));
            open = false;
        }
        while (x < n || y < m)
        {
            if (x < n && y < m && baseLines[prefix + x] == other[prefix + y])
            {
                Close(x, y);
                x++; y++;
                continue;
            }
            if (!open) { open = true; runBase = x; runOther = y; }
            if (y < m && (x == n || table[x, y + 1] >= table[x + 1, y])) y++;
            else x++;
        }
        Close(x, y);
        return hunks;
    }
}
