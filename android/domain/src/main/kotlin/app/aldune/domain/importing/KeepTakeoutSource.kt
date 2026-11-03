package app.aldune.domain.importing

import app.aldune.domain.NoteState
import app.aldune.text.TaskLines
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.longOrNull
import java.io.IOException
import java.time.Instant
import java.util.zip.ZipException
import java.util.zip.ZipInputStream

/**
 * Google Keep through Google Takeout: the .zip of the export, or the .json files of the notes picked one by one (an .html twin next to a
 * .json is ignored; an .html alone is read as a best-effort fallback without dates, state or pin). Nothing is extracted to disk: the zip is
 * read as a stream, entry names are only labels (so a "../" in one cannot write anywhere) and every entry is read with a size cap.
 */
object KeepTakeoutSource : ImportSource {
    private val htmlTitle = Regex("""<div[^>]*class="title"[^>]*>(?<v>.*?)</div>""", setOf(RegexOption.DOT_MATCHES_ALL, RegexOption.IGNORE_CASE))
    private val htmlContentStart = Regex("""<div[^>]*class="content"[^>]*>""", RegexOption.IGNORE_CASE)
    private val htmlLabel = Regex("""class="label-name"[^>]*>(?<v>[^<]*)<""", RegexOption.IGNORE_CASE)
    private val htmlColor = Regex("""class="note (?<v>[A-Za-z]+)""", RegexOption.IGNORE_CASE)

    private class State {
        val builder = ImportPreviewBuilder()
        val jsonStems = HashSet<String>()
        val pendingHtml = ArrayList<Pair<String, ByteArray>>()
        var pendingHtmlBytes = 0L
        var totalBytes = 0L
        var entries = 0
    }

    override fun read(inputs: List<ImportInput>): ImportPreview {
        if (inputs.isEmpty()) return ImportPreview.failed(ImportFailure.NOT_FOUND)
        val s = State()
        for (input in inputs) {
            if (s.builder.exhausted) break
            try {
                if (input.name.endsWith(".zip", ignoreCase = true)) readZip(input, s) else readFile(input, s)
            } catch (_: IOException) {
                s.builder.lastFailure = ImportFailure.UNREADABLE
                s.builder.skip(input.name, ImportSkipReason.UNREADABLE)
            }
        }
        // An html is a fallback only for the notes that have no json of their own.
        for ((name, bytes) in s.pendingHtml) {
            if (s.builder.exhausted) break
            if (stem(name) !in s.jsonStems) readHtml(name, bytes, s.builder)
        }
        return s.builder.build()
    }

    private fun isCandidate(name: String) = name.endsWith(".json", true) || name.endsWith(".html", true)

    private fun stem(name: String) = name.substringBeforeLast('.')

    private fun readFile(input: ImportInput, s: State) {
        if (!isCandidate(input.name)) return
        if (input.size > ImportLimits.MAX_ENTRY_BYTES) { s.builder.skip(input.name, ImportSkipReason.TOO_LARGE); return }
        val bytes = input.open().use { it.readBounded(ImportLimits.MAX_ENTRY_BYTES) }
        accept(input.name, bytes, s)
    }

    private fun readZip(input: ImportInput, s: State) {
        if (input.size > ImportLimits.MAX_ZIP_BYTES) { s.builder.lastFailure = ImportFailure.TOO_LARGE; return }
        var seen = 0
        try {
            ZipInputStream(input.open().buffered()).use { zip ->
                while (!s.builder.exhausted) {
                    val entry = zip.nextEntry ?: break
                    seen++
                    if (entry.isDirectory || !isCandidate(entry.name)) continue
                    if (++s.entries > ImportLimits.MAX_FILES) break
                    // The declared size can lie, so the real read is bounded again.
                    val bytes = zip.readBounded(ImportLimits.MAX_ENTRY_BYTES)
                    if (bytes != null) {
                        s.totalBytes += bytes.size
                        if (s.totalBytes > ImportLimits.MAX_TOTAL_BYTES) { s.builder.skip(entry.name, ImportSkipReason.TOO_LARGE); break }
                    }
                    accept(entry.name, bytes, s)
                }
            }
        } catch (e: ZipException) {
            throw IOException(e)
        }
        // A file that is not a zip has no entries at all: ZipInputStream does not complain, it just finds nothing.
        if (seen == 0) s.builder.lastFailure = ImportFailure.UNREADABLE
    }

    private fun accept(name: String, bytes: ByteArray?, s: State) {
        if (bytes == null) { s.builder.skip(name, ImportSkipReason.TOO_LARGE); return }
        if (name.endsWith(".json", true)) {
            s.jsonStems += stem(name)
            readJson(name, bytes, s.builder)
        } else if (s.pendingHtmlBytes + bytes.size <= ImportLimits.MAX_PENDING_HTML_BYTES) {
            s.pendingHtmlBytes += bytes.size
            s.pendingHtml += name to bytes
        } else s.builder.skip(name, ImportSkipReason.TOO_LARGE)
    }

    private fun readJson(name: String, bytes: ByteArray, builder: ImportPreviewBuilder) {
        val root = try {
            Json.parseToJsonElement(String(bytes, Charsets.UTF_8))
        } catch (_: Exception) {
            // Takeout also ships other JSON (labels, settings): anything that is not a readable note is simply not one.
            return
        } as? JsonObject ?: return
        if (!looksLikeNote(root)) return

        val title = str(root, "title")
        val body = StringBuilder()
        str(root, "textContent")?.takeIf { it.isNotBlank() }?.let { body.append(ImportPreviewBuilder.lf(it)) }
        (root["listContent"] as? JsonArray)?.forEach { item ->
            val text = (item as? JsonObject)?.let { str(it, "text") }?.replace("\r", " ")?.replace("\n", " ")?.trim()
            if (text.isNullOrEmpty()) return@forEach
            if (body.isNotEmpty()) body.append('\n')
            val done = ((item as JsonObject)["isChecked"] as? JsonPrimitive)?.booleanOrNull == true
            // The space after the box is part of the task (TaskLines).
            body.append(if (done) TaskLines.CHECKED else TaskLines.UNCHECKED).append(' ').append(text)
        }

        val composed = ImportPreviewBuilder.compose(title, body.toString())
        if (composed == null) { builder.skip(name, ImportSkipReason.EMPTY); return }
        if (composed.length > ImportLimits.MAX_NOTE_CHARS) { builder.skip(name, ImportSkipReason.TOO_LARGE); return }

        val tags = (root["labels"] as? JsonArray)?.mapNotNull { (it as? JsonObject)?.let { l -> str(l, "name") }?.trim()?.takeIf { t -> t.isNotEmpty() } }.orEmpty()
        val attachments = (root["attachments"] as? JsonArray)?.size ?: 0
        val state = when {
            flag(root, "isTrashed") -> NoteState.TRASHED
            flag(root, "isArchived") -> NoteState.ARCHIVED
            else -> NoteState.ACTIVE
        }
        builder.add(
            ImportedNote(composed, ImportHues.fromName(str(root, "color")), tags, state, flag(root, "isPinned"),
                usec(root, "createdTimestampUsec"), usec(root, "userEditedTimestampUsec")),
            attachments,
        )
    }

    private fun looksLikeNote(root: JsonObject) =
        "textContent" in root || "listContent" in root || ("title" in root && "userEditedTimestampUsec" in root)

    private fun readHtml(name: String, bytes: ByteArray, builder: ImportPreviewBuilder) {
        val html = String(bytes, Charsets.UTF_8)
        val titleMatch = htmlTitle.find(html)
        val title = titleMatch?.let { HtmlText.toNoteText(it.groups["v"]!!.value) }
        val content = htmlContentStart.find(html)
        val body = content?.let { HtmlText.toNoteText(html.substring(it.range.last + 1)) }

        // Only Keep's own pages have a title or content block; any other HTML in the archive is not a note.
        if (titleMatch == null && content == null) return

        val composed = ImportPreviewBuilder.compose(title, body)
        if (composed == null) { builder.skip(name, ImportSkipReason.EMPTY); return }
        if (composed.length > ImportLimits.MAX_NOTE_CHARS) { builder.skip(name, ImportSkipReason.TOO_LARGE); return }

        val tags = htmlLabel.findAll(html).map { it.groups["v"]!!.value.trim() }.filter { it.isNotEmpty() }.toList()
        builder.add(ImportedNote(composed, ImportHues.fromName(htmlColor.find(html)?.groups?.get("v")?.value), tags, NoteState.ACTIVE, false, null, null))
    }

    private fun str(obj: JsonObject, key: String): String? = (obj[key] as? JsonPrimitive)?.takeIf { it.isString }?.content

    private fun flag(obj: JsonObject, key: String) = (obj[key] as? JsonPrimitive)?.booleanOrNull == true

    // Keep stamps are microseconds since the Unix epoch.
    private fun usec(obj: JsonObject, key: String): Instant? {
        val v = ((obj[key] as? JsonPrimitive) as? JsonElement)?.let { (it as JsonPrimitive).longOrNull } ?: return null
        if (v <= 0) return null
        return try { Instant.ofEpochMilli(v / 1000) } catch (_: Exception) { null }
    }
}
