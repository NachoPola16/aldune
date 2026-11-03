package app.aldune.domain.importing

import app.aldune.text.BulletLines
import app.aldune.text.TaskLines
import java.net.URI

/**
 * HTML / ENML to the plain text of an Aldune note (port of `HtmlText` in Aldune.Core). A small tokenizer and not an HTML parser on purpose:
 * the input is untrusted and a note only needs lines, lists, check boxes and links. Nothing here fetches or resolves anything.
 */
object HtmlText {
    private val tokens = Regex("""<!--.*?-->|<!\[CDATA\[.*?\]\]>|<![^>]*>|<\?.*?\?>|<[^>]*>|[^<]+|<""", RegexOption.DOT_MATCHES_ALL)
    private val tag = Regex("""^<(?<close>/)?(?<name>[A-Za-z][A-Za-z0-9:_-]*)(?<attrs>[^>]*)>$""", RegexOption.DOT_MATCHES_ALL)
    private val href = Regex("""href\s*=\s*(?:"(?<a>[^"]*)"|'(?<b>[^']*)')""", RegexOption.IGNORE_CASE)
    private val checkedAttr = Regex("""checked\s*=\s*["']?true""", RegexOption.IGNORE_CASE)

    // Google Keep's HTML draws its check boxes as a span with a ballot-box character; they become real to-dos before tokenizing.
    private val keepBullet = Regex(
        """<span[^>]*class="[^"]*bullet[^"]*"[^>]*>\s*(?<box>&#9744;|&#9745;|&#9746;|☐|☑|☒)\s*</span>""", RegexOption.IGNORE_CASE,
    )

    private val blocks = setOf("div", "p", "h1", "h2", "h3", "h4", "h5", "h6", "blockquote", "pre", "table", "tr", "section", "article", "header", "footer", "hr", "body")
    private val paragraphs = setOf("p", "h1", "h2", "h3", "h4", "h5", "h6")
    private val skipped = setOf("script", "style", "head", "title")

    fun toNoteText(html: String): String {
        val prepared = keepBullet.replace(html) { m ->
            if (m.groups["box"]!!.value.let { it == "&#9744;" || it == "☐" }) "<en-todo/>" else "<en-todo checked=\"true\"/>"
        }
        return Converter().run(prepared)
    }

    // The entities a note realistically has; there is no java.* HTML decoder, and the numeric forms cover everything else.
    private val named = mapOf(
        "amp" to "&", "lt" to "<", "gt" to ">", "quot" to "\"", "apos" to "'", "nbsp" to " ", "copy" to "©", "reg" to "®",
        "hellip" to "…", "mdash" to "—", "ndash" to "–", "lsquo" to "‘", "rsquo" to "’", "ldquo" to "“", "rdquo" to "”", "bull" to "•",
        "middot" to "·", "euro" to "€", "laquo" to "«", "raquo" to "»", "iexcl" to "¡", "iquest" to "¿", "deg" to "°", "times" to "×",
        "aacute" to "á", "eacute" to "é", "iacute" to "í", "oacute" to "ó", "uacute" to "ú", "Aacute" to "Á", "Eacute" to "É",
        "Iacute" to "Í", "Oacute" to "Ó", "Uacute" to "Ú", "ntilde" to "ñ", "Ntilde" to "Ñ", "uuml" to "ü", "Uuml" to "Ü",
        "ccedil" to "ç", "Ccedil" to "Ç", "agrave" to "à", "egrave" to "è", "igrave" to "ì", "ograve" to "ò", "ugrave" to "ù",
        "acirc" to "â", "ecirc" to "ê", "icirc" to "î", "ocirc" to "ô", "ucirc" to "û", "auml" to "ä", "euml" to "ë", "iuml" to "ï",
        "ouml" to "ö", "szlig" to "ß", "atilde" to "ã", "otilde" to "õ", "Auml" to "Ä", "Ouml" to "Ö", "aring" to "å", "oslash" to "ø",
    )
    private val entity = Regex("&(?:#(\\d{1,7})|#[xX]([0-9a-fA-F]{1,6})|([A-Za-z][A-Za-z0-9]{1,10}));")

    internal fun decode(text: String): String {
        if ('&' !in text) return text
        return entity.replace(text) { m ->
            val code = m.groupValues[1].toIntOrNull() ?: m.groupValues[2].toIntOrNull(16)
            when {
                code != null -> if (Character.isValidCodePoint(code) && code != 0) String(Character.toChars(code)) else m.value
                else -> named[m.groupValues[3]] ?: m.value
            }
        }
    }

    private class Converter {
        private val sb = StringBuilder()
        private val lists = ArrayList<Pair<Boolean, Int>>()
        private val links = ArrayDeque<Pair<String, Int>>()
        private var skipUntil: String? = null
        private var pendingSpace = false
        private var noLead = false
        private var pendingBlank = false
        private var inPre = false
        private var bulletIndent: String? = null
        private var bulletMarker: String? = null

        fun run(html: String): String {
            for (token in tokens.findAll(html)) {
                val value = token.value
                if (value.startsWith("<!--") || value.startsWith("<?") || value.startsWith("<!")) continue
                if (value[0] == '<') {
                    val t = tag.find(value)
                    if (t != null) handleTag(t.groups["close"] != null, t.groups["name"]!!.value.lowercase(), t.groups["attrs"]!!.value)
                    else if (skipUntil == null) addText(value)
                    continue
                }
                if (skipUntil == null) addText(decode(value))
            }
            return finish()
        }

        private fun handleTag(closing: Boolean, name: String, attrs: String) {
            if (skipUntil != null) {
                if (closing && name == skipUntil) skipUntil = null
                return
            }
            if (!closing && name in skipped && !attrs.trimEnd().endsWith('/')) { skipUntil = name; return }

            when {
                name == "br" -> {
                    flush(); trimLineEnd(); sb.append('\n'); pendingSpace = false; noLead = false
                }
                name == "ul" || name == "ol" -> {
                    breakLine()
                    if (closing) { if (lists.isNotEmpty()) lists.removeAt(lists.size - 1); bulletMarker = null }
                    else lists += (name == "ol") to 0
                }
                name == "li" -> { breakLine(); bulletMarker = null; if (!closing) startItem() }
                name == "en-todo" && !closing -> {
                    breakLine()
                    // A to-do replaces the arrow of a list item (it keeps the indent), so the marker is dropped before the flush writes it.
                    val indent = bulletIndent ?: ""
                    bulletIndent = null; bulletMarker = null
                    flush()
                    sb.append(indent)
                    sb.append(if (checkedAttr.containsMatchIn(attrs)) TaskLines.CHECKED else TaskLines.UNCHECKED).append(' ')
                    noLead = true
                    pendingSpace = false
                }
                name == "a" -> {
                    if (!closing) {
                        val h = href.find(attrs)
                        val value = h?.let { (it.groups["a"] ?: it.groups["b"])?.value }.orEmpty()
                        links.addLast(decode(value).trim() to sb.length)
                    } else if (links.isNotEmpty()) endLink(links.removeLast())
                }
                name == "pre" -> { breakLine(); inPre = !closing; if (closing) pendingBlank = false }
                name in blocks -> {
                    breakLine()
                    if (closing && name in paragraphs && sb.isNotEmpty()) pendingBlank = true
                }
            }
        }

        private fun startItem() {
            val depth = maxOf(lists.size, 1)
            bulletIndent = " ".repeat(2 * (depth - 1))
            if (lists.isNotEmpty() && lists.last().first) {
                val (ordered, count) = lists.last()
                lists[lists.size - 1] = ordered to count + 1
                bulletMarker = "${count + 1}. "
            } else bulletMarker = BulletLines.GLYPH + " "
        }

        private fun endLink(link: Pair<String, Int>) {
            val (target, start) = link
            if (target.isEmpty()) return
            val scheme = try { URI(target).scheme?.lowercase() } catch (_: Exception) { null }
            if (scheme != "http" && scheme != "https" && scheme != "mailto") return
            val shown = sb.substring(start).trim()
            if (shown.isEmpty()) { flush(); sb.append(target); return }
            if (shown.contains(target, ignoreCase = true)) return
            sb.append(" (").append(target).append(')')
        }

        private fun addText(text: String) {
            for (c in text) {
                if (inPre) {
                    if (c == '\r') continue
                    flush(); sb.append(c); continue
                }
                if (c.isWhitespace()) { pendingSpace = true; continue }
                flush()
                if (pendingSpace && !atLineStart()) sb.append(' ')
                pendingSpace = false
                noLead = false
                sb.append(c)
            }
        }

        private fun atLineStart() = sb.isEmpty() || sb[sb.length - 1] == '\n' || noLead

        // Everything that must come before the next piece of content: a blank line after a paragraph, then the list marker of an item.
        private fun flush() {
            if (pendingBlank) {
                pendingBlank = false
                if (sb.isNotEmpty()) { trimLineEnd(); if (sb[sb.length - 1] != '\n') sb.append('\n'); sb.append('\n') }
            }
            if (bulletMarker != null) {
                sb.append(bulletIndent).append(bulletMarker)
                bulletIndent = null; bulletMarker = null
                noLead = true
            }
        }

        private fun breakLine() {
            if (pendingBlank) flush()
            trimLineEnd()
            if (sb.isNotEmpty() && sb[sb.length - 1] != '\n') sb.append('\n')
            pendingSpace = false
            noLead = false
        }

        private fun trimLineEnd() { while (sb.isNotEmpty() && sb[sb.length - 1] == ' ') sb.setLength(sb.length - 1) }

        private fun finish(): String {
            val result = ArrayList<String>()
            for (line in sb.toString().split('\n').map { it.trimEnd() }) {
                if (line.isEmpty() && (result.isEmpty() || result.last().isEmpty())) continue
                result += line
            }
            while (result.isNotEmpty() && result.last().isEmpty()) result.removeAt(result.size - 1)
            return result.joinToString("\n")
        }
    }
}
