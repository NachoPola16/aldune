package app.aldune.text

/** Bit flags of [MarkdownSpan.style]; the same set and meaning as `MarkdownStyle` in `Aldune.Core`. */
object MarkdownStyle {
    /** Syntax characters (stars, hashes, brackets, the URL of a link): drawn dimmed, never removed. */
    const val MARKER = 1
    const val BOLD = 2
    const val ITALIC = 4
    const val STRIKE = 8
    const val CODE = 16
    const val LINK = 32
    const val HEADING = 64
}

/** A stretch of a note and how to draw it. The spans of one text are contiguous, in order, and cover all of it. */
data class MarkdownSpan(val start: Int, val length: Int, val style: Int) {
    val end: Int get() = start + length
}

/**
 * Which stretches of a note to draw bold, italic and so on, without ever changing the text. A port of `MarkdownLive` in `Aldune.Core`
 * (same rules, same tests): the text of a note stays plain text for sync, search and linked files, and only the display changes.
 */
object MarkdownLive {
    // Past this a line is left plain: looking for a closing delimiter is quadratic in the worst case, and a line this long is a pasted log.
    private const val MAX_LINE_LENGTH = 4000
    private const val MAX_DEPTH = 4

    fun parse(text: String?): List<MarkdownSpan> {
        val spans = ArrayList<MarkdownSpan>()
        if (text.isNullOrEmpty()) return spans

        var inFence = false
        var pos = 0
        while (pos < text.length) {
            val newline = text.indexOf('\n', pos)
            val lineEnd = if (newline < 0) text.length else newline
            val next = if (newline < 0) text.length else newline + 1
            // A CRLF note: the \r belongs to the terminator, not to the line.
            val contentEnd = if (lineEnd > pos && text[lineEnd - 1] == '\r') lineEnd - 1 else lineEnd

            if (isFence(text, pos, contentEnd)) {
                add(spans, pos, contentEnd - pos, MarkdownStyle.MARKER or MarkdownStyle.CODE)
                add(spans, contentEnd, next - contentEnd, MarkdownStyle.CODE)
                inFence = !inFence
            } else if (inFence) {
                add(spans, pos, next - pos, MarkdownStyle.CODE)
            } else {
                line(text, pos, contentEnd, spans)
                add(spans, contentEnd, next - contentEnd, 0)
            }
            pos = next
        }
        return spans
    }

    private fun isFence(t: String, start: Int, end: Int) = end - start >= 3 && t[start] == '`' && t[start + 1] == '`' && t[start + 2] == '`'

    private fun line(t: String, start: Int, end: Int, o: MutableList<MarkdownSpan>) {
        if (end <= start) return
        if (end - start > MAX_LINE_LENGTH) { add(o, start, end - start, 0); return }

        var hashes = 0
        while (start + hashes < end && t[start + hashes] == '#') hashes++
        if (hashes in 1..6 && start + hashes < end && t[start + hashes] == ' ') {
            add(o, start, hashes + 1, MarkdownStyle.MARKER)
            inline(t, start + hashes + 1, end, MarkdownStyle.BOLD or MarkdownStyle.HEADING, o, 0)
            return
        }
        inline(t, start, end, 0, o, 0)
    }

    private fun inline(t: String, start: Int, end: Int, style: Int, o: MutableList<MarkdownSpan>, depth: Int) {
        var plainFrom = start
        var i = start
        while (i < end) {
            val c = t[i]

            if (c == '\\' && i + 1 < end && isEscapable(t[i + 1])) {
                flush(o, plainFrom, i, style)
                add(o, i, 1, MarkdownStyle.MARKER)
                plainFrom = i + 1          // the escaped character is plain text again
                i += 2
                continue
            }

            if (c == '`') {
                val n = run(t, i, end)
                val close = findExactRun(t, '`', n, i + n, end)
                if (close > i + n) {
                    flush(o, plainFrom, i, style)
                    add(o, i, n, MarkdownStyle.MARKER)
                    add(o, i + n, close - i - n, style or MarkdownStyle.CODE)
                    add(o, close, n, MarkdownStyle.MARKER)
                    i = close + n
                    plainFrom = i
                } else i += n
                continue
            }

            if (c == '[' && depth < MAX_DEPTH) {
                val link = tryLink(t, i, end)
                if (link != null) {
                    val (textEnd, linkEnd) = link
                    flush(o, plainFrom, i, style)
                    add(o, i, 1, MarkdownStyle.MARKER)
                    inline(t, i + 1, textEnd, style or MarkdownStyle.LINK, o, depth + 1)
                    add(o, textEnd, linkEnd - textEnd, MarkdownStyle.MARKER)
                    i = linkEnd
                    plainFrom = i
                    continue
                }
            }

            if (c == '*' || c == '_' || c == '~') {
                val n = run(t, i, end)
                val added = if (c == '~') { if (n == 2) MarkdownStyle.STRIKE else 0 } else when (n) {
                    1 -> MarkdownStyle.ITALIC
                    2 -> MarkdownStyle.BOLD
                    3 -> MarkdownStyle.BOLD or MarkdownStyle.ITALIC
                    else -> 0
                }
                val close = if (added != 0 && depth < MAX_DEPTH && canOpen(t, c, i, n, end)) findCloser(t, c, n, i + n, end) else -1
                if (close > 0) {
                    flush(o, plainFrom, i, style)
                    add(o, i, n, MarkdownStyle.MARKER)
                    inline(t, i + n, close, style or added, o, depth + 1)
                    add(o, close, n, MarkdownStyle.MARKER)
                    i = close + n
                    plainFrom = i
                } else i += n
                continue
            }

            i++
        }
        flush(o, plainFrom, end, style)
    }

    // ASCII punctuation and symbols, as in CommonMark (the backtick and the tilde are symbols, not punctuation, for the platform).
    private fun isEscapable(c: Char): Boolean = c.code < 128 && !c.isLetterOrDigit() && !c.isWhitespace() && !c.isISOControl()

    private fun run(t: String, i: Int, end: Int): Int {
        var n = 1
        while (i + n < end && t[i + n] == t[i]) n++
        return n
    }

    private fun findExactRun(t: String, c: Char, n: Int, from: Int, end: Int): Int {
        var p = from
        while (p < end) {
            if (t[p] != c) { p++; continue }
            val m = run(t, p, end)
            if (m == n) return p
            p += m
        }
        return -1
    }

    // An opener is followed by something visible; an underscore inside a word (snake_case) is not emphasis.
    private fun canOpen(t: String, c: Char, i: Int, n: Int, end: Int): Boolean {
        if (i + n >= end || t[i + n].isWhitespace()) return false
        return c != '_' || i == 0 || !t[i - 1].isLetterOrDigit()
    }

    // The closer matches the opener's run length exactly, follows something visible and, for an underscore, does not touch a letter.
    private fun findCloser(t: String, c: Char, n: Int, from: Int, end: Int): Int {
        var p = from
        while (p < end) {
            if (t[p] == '\\') { p += 2; continue }
            if (t[p] == '`') {
                // Skip a code span: its stars are literal.
                val m2 = run(t, p, end)
                val close = findExactRun(t, '`', m2, p + m2, end)
                p = if (close > 0) close + m2 else p + m2
                continue
            }
            if (t[p] != c) { p++; continue }

            val m = run(t, p, end)
            if (m == n && p > from && !t[p - 1].isWhitespace() && (c != '_' || p + m >= end || !t[p + m].isLetterOrDigit())) return p
            p += m
        }
        return -1
    }

    // [text](url): no brackets inside the text, no whitespace in the url. Returns where the text ends and where the whole link ends.
    private fun tryLink(t: String, i: Int, end: Int): Pair<Int, Int>? {
        var j = i + 1
        while (j < end && t[j] != ']' && t[j] != '[') j++
        if (j >= end || t[j] != ']' || j == i + 1 || j + 2 >= end || t[j + 1] != '(') return null

        var k = j + 2
        while (k < end && t[k] != ')' && !t[k].isWhitespace()) k++
        if (k >= end || t[k] != ')' || k == j + 2) return null
        return j to k + 1
    }

    private fun flush(o: MutableList<MarkdownSpan>, from: Int, to: Int, style: Int) {
        if (to > from) add(o, from, to - from, style)
    }

    // Adjacent stretches with the same style are one span: fewer pieces to style.
    private fun add(o: MutableList<MarkdownSpan>, start: Int, length: Int, style: Int) {
        if (length <= 0) return
        val last = o.lastOrNull()
        if (last != null && last.style == style && last.end == start) o[o.size - 1] = last.copy(length = last.length + length)
        else o.add(MarkdownSpan(start, length, style))
    }
}
