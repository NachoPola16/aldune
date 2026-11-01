package app.aldune.text

import java.text.Normalizer

/**
 * Port of `FuzzySearch` in Aldune.Core: forgiving text search for the settings. The same words found whatever the case or accents, with a
 * typo or two, half-typed, or in another order. Every word of the query has to match some word of the text; the score is the mean of the
 * best match of each. Pinned by the same cases as the C# tests.
 */
object FuzzySearch {
    /** What a text must score to count as a match. */
    const val MINIMUM_SCORE = 0.5

    /** Lower case, no accents, anything that is not a letter or a digit turned into a space. */
    fun normalize(text: String?): String {
        if (text.isNullOrEmpty()) return ""
        val builder = StringBuilder(text.length)
        for (c in Normalizer.normalize(text, Normalizer.Form.NFD)) {
            if (Character.getType(c) == Character.NON_SPACING_MARK.toInt()) continue
            builder.append(if (c.isLetterOrDigit()) c.lowercaseChar() else ' ')
        }
        return builder.toString()
    }

    private fun words(text: String?): List<String> = normalize(text).split(' ').filter { it.isNotEmpty() }

    /** 0 when the text does not answer the query, up to 1 for an exact match. */
    fun score(query: String?, text: String?): Double {
        val queryWords = words(query)
        val textWords = words(text)
        if (queryWords.isEmpty() || textWords.isEmpty()) return 0.0

        var total = 0.0
        for (queryWord in queryWords) {
            var best = 0.0
            for (textWord in textWords) best = maxOf(best, wordScore(queryWord, textWord))
            if (best < MINIMUM_SCORE) return 0.0
            total += best
        }
        return total / queryWords.size
    }

    /** The items that match, best first; items with the same score keep their order. */
    fun <T> rank(items: Iterable<T>, textOf: (T) -> String?, query: String?): List<T> {
        if (query.isNullOrBlank()) return emptyList()
        return items.withIndex()
            .map { Triple(it.value, it.index, score(query, textOf(it.value))) }
            .filter { it.third >= MINIMUM_SCORE }
            .sortedWith(compareByDescending<Triple<T, Int, Double>> { it.third }.thenBy { it.second })
            .map { it.first }
    }

    private fun wordScore(query: String, word: String): Double {
        if (query == word) return 1.0
        if (query.length < 2) return 0.0
        if (word.startsWith(query)) return 0.9
        if (query.length >= 3 && word.contains(query)) return 0.75
        // The query is the plural (or a longer form) of the word: "shadows" for "shadow".
        if (word.length >= 4 && query.startsWith(word)) return 0.8

        val allowed = if (query.length <= 3) 0 else if (query.length <= 6) 1 else 2
        if (allowed == 0) return 0.0
        val distance = distance(query, word)
        if (distance <= allowed) return 0.7 - 0.1 * (distance - 1)
        // A typo in a word that is still being typed: compare with the start of the word.
        // (Also against a start one letter longer or shorter: a missing letter is one edit from the start of the word that has it.)
        var best = Int.MAX_VALUE
        for (length in maxOf(1, query.length - 1)..minOf(word.length - 1, query.length + 1)) best = minOf(best, distance(query, word.substring(0, length)))
        if (best <= allowed) return 0.55 - 0.05 * (best - 1)
        return 0.0
    }

    /** Edit distance with adjacent swaps counted as one edit (optimal string alignment). */
    internal fun distance(a: String, b: String): Int {
        val d = Array(a.length + 1) { IntArray(b.length + 1) }
        for (i in 0..a.length) d[i][0] = i
        for (j in 0..b.length) d[0][j] = j
        for (i in 1..a.length) {
            for (j in 1..b.length) {
                val cost = if (a[i - 1] == b[j - 1]) 0 else 1
                d[i][j] = minOf(d[i - 1][j] + 1, d[i][j - 1] + 1, d[i - 1][j - 1] + cost)
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) d[i][j] = minOf(d[i][j], d[i - 2][j - 2] + 1)
            }
        }
        return d[a.length][b.length]
    }
}
