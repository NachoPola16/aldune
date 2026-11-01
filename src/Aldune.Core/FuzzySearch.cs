using System.Globalization;
using System.Text;

namespace Aldune.Core;

/// <summary>
/// Forgiving text search for the settings (and anything else that lists options): the same words found whatever the case or accents,
/// with a typo or two, half-typed, or in a different order. Every word of the query has to match some word of the text; the score
/// is the mean of the best match of each. The Kotlin port (<c>FuzzySearch.kt</c>) is pinned by the same cases in the vectors.
/// </summary>
public static class FuzzySearch
{
    /// <summary>What a text must score to count as a match.</summary>
    public const double MinimumScore = 0.5;

    /// <summary>Lower case, no accents, anything that is not a letter or a digit turned into a space.</summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var builder = new StringBuilder(text.Length);
        foreach (char c in text.Normalize(NormalizationForm.FormD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark) continue;
            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return builder.ToString();
    }

    private static string[] Words(string? text) => Normalize(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>0 when the text does not answer the query, up to 1 for an exact match.</summary>
    public static double Score(string? query, string? text)
    {
        var queryWords = Words(query);
        var textWords = Words(text);
        if (queryWords.Length == 0 || textWords.Length == 0) return 0;

        double total = 0;
        foreach (var queryWord in queryWords)
        {
            double best = 0;
            foreach (var textWord in textWords) best = Math.Max(best, WordScore(queryWord, textWord));
            if (best < MinimumScore) return 0;
            total += best;
        }
        return total / queryWords.Length;
    }

    /// <summary>The items that match, best first; items with the same score keep their order.</summary>
    public static IReadOnlyList<T> Rank<T>(IEnumerable<T> items, Func<T, string?> textOf, string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        return items
            .Select((item, index) => (item, index, score: Score(query, textOf(item))))
            .Where(entry => entry.score >= MinimumScore)
            .OrderByDescending(entry => entry.score)
            .ThenBy(entry => entry.index)
            .Select(entry => entry.item)
            .ToList();
    }

    private static double WordScore(string query, string word)
    {
        if (query == word) return 1;
        if (query.Length < 2) return 0;
        if (word.StartsWith(query, StringComparison.Ordinal)) return 0.9;
        if (query.Length >= 3 && word.Contains(query, StringComparison.Ordinal)) return 0.75;
        // The query is the plural (or a longer form) of the word: "shadows" for "shadow".
        if (word.Length >= 4 && query.StartsWith(word, StringComparison.Ordinal)) return 0.8;

        int allowed = query.Length <= 3 ? 0 : query.Length <= 6 ? 1 : 2;
        if (allowed == 0) return 0;
        int distance = Distance(query, word);
        if (distance <= allowed) return 0.7 - 0.1 * (distance - 1);
        // A typo in a word that is still being typed: compare with the start of the word.
        // (Also against a start one letter longer or shorter: a missing letter is one edit from the start of the word that has it.)
        int best = int.MaxValue;
        for (int length = Math.Max(1, query.Length - 1); length <= Math.Min(word.Length - 1, query.Length + 1); length++)
            best = Math.Min(best, Distance(query, word[..length]));
        if (best <= allowed) return 0.55 - 0.05 * (best - 1);
        return 0;
    }

    /// <summary>Edit distance with adjacent swaps counted as one edit (optimal string alignment).</summary>
    internal static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        }
        return d[a.Length, b.Length];
    }
}
