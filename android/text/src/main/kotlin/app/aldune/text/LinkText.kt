package app.aldune.text

import java.net.URI
import java.net.URISyntaxException

/**
 * Web links inside the plain text of a note (port of `Aldune.Core.LinkText`; the C# tests have a twin in `LinkTextTest`).
 *
 * A link is not a markup: the note stays plain text, so sync, search, export and the list preview are untouched. A run that starts with
 * `http://` or `https://` is recognised, and the editor only ever opens it when the user asks. Only those two schemes qualify, never
 * `file:`, `javascript:`, `content:` or anything else that could reach a local resource. A URL with user info (`https://good.com@evil.tld`)
 * is rejected too: it is the classic way to make a link look like it points somewhere else.
 */
object LinkText {
    /** A run longer than this is not a link someone typed; it keeps the matcher cheap on pasted blobs. */
    const val MAX_LENGTH = 2048

    /** Where a link sits in a note's text: `[start, end)`. */
    data class Span(val start: Int, val end: Int) {
        val length: Int get() = end - start
    }

    /** The link that contains [index], or null. */
    fun findAt(text: String, index: Int): Span? {
        if (text.isEmpty() || index < 0 || index >= text.length || isEnd(text[index])) return null

        // The run of non-space characters around the index (bounded: MAX_LENGTH each way).
        var start = index
        while (start > 0 && !isEnd(text[start - 1]) && index - start <= MAX_LENGTH) start--
        var end = index + 1
        while (end < text.length && !isEnd(text[end]) && end - start <= MAX_LENGTH) end++
        if (end - start > MAX_LENGTH) return null

        // A link can start inside the run (after an opening bracket or a quote).
        var schemeAt = indexOfScheme(text, start, end)
        while (schemeAt >= 0) {
            val linkEnd = trimTrailing(text, schemeAt, end)
            if (index >= schemeAt && index < linkEnd && toUri(text.substring(schemeAt, linkEnd)) != null) return Span(schemeAt, linkEnd)
            if (index < schemeAt) return null
            schemeAt = indexOfScheme(text, schemeAt + 1, end)
        }
        return null
    }

    /** Every link of [text], in order. Used to underline them while editing. */
    fun findAll(text: String): List<Span> {
        val spans = ArrayList<Span>()
        var i = 0
        while (i < text.length) {
            val c = text[i]
            if ((c == 'h' || c == 'H') && i + 7 < text.length) {
                val span = findAt(text, i)
                if (span != null && span.start == i) {
                    spans.add(span)
                    i = span.end
                    continue
                }
            }
            i++
        }
        return spans
    }

    /** The address to open for [candidate]: absolute http(s), no user info, short enough; null otherwise. This is the only thing the app opens. */
    fun toUri(candidate: String): String? {
        if (candidate.length > MAX_LENGTH) return null
        val uri = try { URI(candidate) } catch (_: URISyntaxException) { return null }
        val scheme = uri.scheme?.lowercase() ?: return null
        if (scheme != "http" && scheme != "https") return null
        if (uri.host.isNullOrEmpty() || uri.userInfo != null) return null
        return candidate
    }

    /** The host to show next to the «Open» action, so the user sees where the link goes. */
    fun hostOf(candidate: String): String? = toUri(candidate)?.let { URI(it).host }

    // Whitespace ends a link, and so do the task glyphs (a checkbox is never part of an address).
    private fun isEnd(c: Char) = c.isWhitespace() || c == '☐' || c == '☑' || c == '☒'

    private fun indexOfScheme(text: String, from: Int, to: Int): Int {
        for (i in from until to) {
            val c = text[i]
            if (c != 'h' && c != 'H') continue
            if (matches(text, i, to, "https://") || matches(text, i, to, "http://")) return i
        }
        return -1
    }

    private fun matches(text: String, at: Int, to: Int, scheme: String) =
        at + scheme.length < to && text.regionMatches(at, scheme, 0, scheme.length, ignoreCase = true)

    /** Drops sentence punctuation and a closing bracket/quote that has no opening one inside the link. */
    private fun trimTrailing(text: String, start: Int, endIn: Int): Int {
        var end = endIn
        while (end > start) {
            val c = text[end - 1]
            if (c in ".,;:!?'\"»”’") { end--; continue }
            if (c == ')' || c == ']' || c == '}') {
                val open = when (c) { ')' -> '('; ']' -> '['; else -> '{' }
                var opens = 0
                var closes = 0
                for (i in start until end) { if (text[i] == open) opens++ else if (text[i] == c) closes++ }
                if (closes > opens) { end--; continue }
            }
            break
        }
        return end
    }
}
