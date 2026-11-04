package app.aldune.domain.importing

import app.aldune.domain.NoteState
import org.xml.sax.Attributes
import org.xml.sax.InputSource
import org.xml.sax.SAXException
import org.xml.sax.SAXParseException
import org.xml.sax.helpers.DefaultHandler
import java.io.BufferedInputStream
import java.io.IOException
import java.io.InputStream
import java.io.StringReader
import java.time.Instant
import java.time.LocalDateTime
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter
import javax.xml.parsers.SAXParserFactory

/**
 * Evernote's .enex export, read as a stream (attachments, which can be huge base64, are skipped without being kept). A DOCTYPE that points at
 * Evernote's DTD is normal and is ignored without being fetched; one with an internal subset (`[ ... ]`) is where entities are defined, so the
 * file is refused before the parser sees it: a hostile file can neither read local files nor blow up in memory. The ENML body is inside CDATA
 * and is converted with [HtmlText], which never parses it as XML. SAX is used because it exists both on the JVM and on Android (StAX does not).
 */
object EvernoteSource : ImportSource {
    private val dateFormat = DateTimeFormatter.ofPattern("yyyyMMdd'T'HHmmss'Z'")
    // The whole prolog (everything before the root element) is inspected, not just its first bytes: padding it with comments must not hide a
    // DOCTYPE from the check. A prolog longer than this is refused.
    private const val PEEK = 1024 * 1024

    private class Stop : SAXException("stop")
    private class WrongRoot : SAXException("not an enex")

    override fun read(inputs: List<ImportInput>): ImportPreview {
        if (inputs.isEmpty()) return ImportPreview.failed(ImportFailure.NOT_FOUND)
        val builder = ImportPreviewBuilder()
        for (input in inputs) {
            if (builder.exhausted) break
            if (input.size > ImportLimits.MAX_ENEX_BYTES) { builder.lastFailure = ImportFailure.TOO_LARGE; continue }
            try {
                input.open().use { readOne(it, input.name, builder) }
            } catch (_: IOException) {
                builder.lastFailure = ImportFailure.UNREADABLE
                if (builder.count > 0) builder.skip(input.name, ImportSkipReason.UNREADABLE)
            }
        }
        return builder.build()
    }

    private fun readOne(raw: InputStream, name: String, builder: ImportPreviewBuilder) {
        val stream = BufferedInputStream(raw, PEEK)
        stream.mark(PEEK)
        val head = ByteArray(PEEK)
        var n = 0
        while (n < PEEK) { val r = stream.read(head, n, PEEK - n); if (r < 0) break; n += r }
        stream.reset()
        if (!prologIsSafe(head, n)) { builder.lastFailure = ImportFailure.UNREADABLE; return }

        val handler = Handler(builder)
        try {
            val factory = SAXParserFactory.newInstance().apply {
                isNamespaceAware = false
                // Each flag exists on some parsers only; the resolver below and the pre-check are what is relied upon.
                for (f in listOf("http://xml.org/sax/features/external-general-entities", "http://xml.org/sax/features/external-parameter-entities", "http://apache.org/xml/features/nonvalidating/load-external-dtd"))
                    try { setFeature(f, false) } catch (_: Exception) {}
            }
            val reader = factory.newSAXParser().xmlReader
            reader.entityResolver = org.xml.sax.EntityResolver { _, _ -> InputSource(StringReader("")) }
            reader.contentHandler = handler
            reader.errorHandler = handler
            reader.parse(InputSource(stream))
        } catch (_: Stop) {
        } catch (_: WrongRoot) {
            builder.lastFailure = ImportFailure.WRONG_FORMAT
        } catch (e: SAXParseException) {
            if (!handler.sawRoot) builder.lastFailure = ImportFailure.WRONG_FORMAT
            else {
                builder.lastFailure = ImportFailure.UNREADABLE
                // Notes read before the damage are kept; the rest is reported as one unreadable item.
                if (builder.count > 0) builder.skip(name, ImportSkipReason.UNREADABLE)
            }
        } catch (e: SAXException) {
            builder.lastFailure = ImportFailure.UNREADABLE
        }
    }

    /**
     * Walks the prolog the way an XML parser would (comments and processing instructions skipped, quotes honoured) up to the first start tag,
     * where no DOCTYPE can follow. Refused: a DOCTYPE with an internal subset (`[`), any entity declaration, a prolog that does not end
     * inside the inspected bytes, and encodings that hide ASCII from this scan (a UTF-16/32 byte order mark, or NUL bytes up front).
     */
    private fun prologIsSafe(head: ByteArray, length: Int): Boolean {
        if (length >= 2 && ((head[0] == 0xFF.toByte() && head[1] == 0xFE.toByte()) || (head[0] == 0xFE.toByte() && head[1] == 0xFF.toByte()))) return false
        if ((0 until minOf(length, 4)).any { head[it] == 0.toByte() }) return false
        val s = String(head, 0, length, Charsets.ISO_8859_1)
        var i = 0
        while (true) {
            val lt = s.indexOf('<', i)
            if (lt < 0) return length < PEEK   // no tag at all: the parser will say it is not an enex (unless the prolog was cut short)
            when {
                s.startsWith("<!--", lt) -> { val end = s.indexOf("-->", lt + 4); if (end < 0) return false; i = end + 3 }
                s.startsWith("<?", lt) -> { val end = s.indexOf("?>", lt + 2); if (end < 0) return false; i = end + 2 }
                s.startsWith("<!DOCTYPE", lt) -> {
                    var j = lt + 9
                    var quote = 0.toChar()
                    while (j < s.length) {
                        val c = s[j]
                        if (quote != 0.toChar()) { if (c == quote) quote = 0.toChar() }
                        else if (c.code == 34 || c.code == 39) quote = c   // a double or a single quote
                        else if (c == '[') return false
                        else if (c == '>') break
                        j++
                    }
                    if (j >= s.length) return false
                    i = j + 1
                }
                s.startsWith("<!ENTITY", lt) -> return false
                else -> return true   // the root start tag: a DOCTYPE can no longer appear
            }
        }
    }

    private class Handler(private val builder: ImportPreviewBuilder) : DefaultHandler() {
        var sawRoot = false
        private var depth = 0
        private var field: String? = null
        private val text = StringBuilder()
        private var tooBig = false
        private var resourceDepth = 0

        private var title: String? = null
        private var enml: String? = null
        private var created: Instant? = null
        private var updated: Instant? = null
        private var tags = ArrayList<String>()
        private var resources = 0

        override fun startElement(uri: String?, localName: String?, qName: String, attributes: Attributes?) {
            depth++
            if (depth == 1) {
                if (qName != "en-export") throw WrongRoot()
                sawRoot = true
                return
            }
            if (resourceDepth > 0) { resourceDepth++; return }
            when {
                depth == 2 && qName == "note" -> {
                    title = null; enml = null; created = null; updated = null; tags = ArrayList(); resources = 0
                }
                depth == 3 -> when (qName) {
                    "title", "content", "created", "updated", "tag" -> { field = qName; text.setLength(0); tooBig = false }
                    // Attachment data is never accumulated: only counted.
                    "resource" -> { resources++; resourceDepth = 1 }
                }
            }
        }

        override fun characters(ch: CharArray, start: Int, length: Int) {
            if (field == null) return
            val max = if (field == "content") ImportLimits.MAX_NOTE_CHARS * 2 else 4096
            if (text.length + length > max) tooBig = true else text.append(ch, start, length)
        }

        override fun endElement(uri: String?, localName: String?, qName: String) {
            depth--
            if (resourceDepth > 0) { resourceDepth--; return }
            if (depth == 2 && field == qName) {
                val value = text.toString()
                when (qName) {
                    "title" -> title = value
                    "content" -> enml = if (tooBig) null else value
                    "created" -> created = parseDate(value)
                    "updated" -> updated = parseDate(value)
                    "tag" -> value.trim().takeIf { it.isNotEmpty() }?.let { tags += it }
                }
                if (qName != "content") tooBig = false
                if (qName == "content") contentTooBig = tooBig
                field = null
            } else if (depth == 1 && qName == "note") finishNote()
            if (builder.exhausted) throw Stop()
        }

        private var contentTooBig = false

        private fun finishNote() {
            val name = title?.takeIf { it.isNotBlank() }?.trim() ?: "(untitled)"
            if (contentTooBig) { contentTooBig = false; builder.skip(name, ImportSkipReason.TOO_LARGE); return }
            val body = enml?.let { HtmlText.toNoteText(it) } ?: ""
            val composed = ImportPreviewBuilder.compose(title, body)
            if (composed == null) { builder.skip(name, ImportSkipReason.EMPTY); return }
            if (composed.length > ImportLimits.MAX_NOTE_CHARS) { builder.skip(name, ImportSkipReason.TOO_LARGE); return }
            builder.add(ImportedNote(composed, null, tags, NoteState.ACTIVE, false, created, updated ?: created), resources)
        }

        override fun error(e: SAXParseException) = throw e
        override fun fatalError(e: SAXParseException) = throw e

        private fun parseDate(value: String): Instant? = try {
            LocalDateTime.parse(value.trim(), dateFormat).toInstant(ZoneOffset.UTC)
        } catch (_: Exception) { null }
    }
}
