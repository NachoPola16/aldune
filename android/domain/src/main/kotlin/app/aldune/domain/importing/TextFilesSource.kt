package app.aldune.domain.importing

import app.aldune.domain.NoteState
import app.aldune.text.MarkdownImport
import app.aldune.text.TextDecoding
import java.io.IOException
import java.time.Instant

/**
 * Markdown / text files picked one by one: each file is a note. Windows reads a folder and filters by extension; here the user picked every file
 * on purpose (the system picker cannot offer a folder with the same guarantees), so the extension is not required and what decides is the content:
 * the same strict decoding as linked files refuses a binary or an ANSI file instead of importing garbage. A file without a "# heading" is titled
 * with its name.
 */
object TextFilesSource : ImportSource {
    override fun read(inputs: List<ImportInput>): ImportPreview {
        if (inputs.isEmpty()) return ImportPreview.failed(ImportFailure.NOT_FOUND)
        val builder = ImportPreviewBuilder()
        var total = 0L
        for (input in inputs) {
            if (builder.exhausted) break
            val name = input.name
            if (input.size > TextDecoding.MAX_BYTES) { builder.skip(name, ImportSkipReason.TOO_LARGE); continue }
            total += maxOf(input.size, 0)
            if (total > ImportLimits.MAX_TOTAL_BYTES) { builder.skip(name, ImportSkipReason.LIMIT_REACHED); break }

            val bytes = try {
                input.open().use { it.readBounded(TextDecoding.MAX_BYTES) }
            } catch (_: IOException) {
                builder.skip(name, ImportSkipReason.UNREADABLE); continue
            }
            if (bytes == null) { builder.skip(name, ImportSkipReason.TOO_LARGE); continue }

            val decoded = TextDecoding.decode(bytes)
            if (decoded == null) { builder.skip(name, ImportSkipReason.NOT_TEXT); continue }
            if (decoded.text.isBlank()) { builder.skip(name, ImportSkipReason.EMPTY); continue }

            val markdown = decoded.text.trimStart()
            var text = MarkdownImport.toNoteText(markdown).trimEnd()
            if (!markdown.startsWith("# ")) text = name.substringBeforeLast('.') + "\n" + text
            if (text.length > ImportLimits.MAX_NOTE_CHARS) { builder.skip(name, ImportSkipReason.TOO_LARGE); continue }

            // The picker only knows the modification time, so it is also the creation date (Windows uses the older of the two).
            val modified = input.lastModified?.takeIf { it > 0 }?.let { Instant.ofEpochMilli(it) }
            builder.add(ImportedNote(text, null, emptyList(), NoteState.ACTIVE, false, modified, modified))
        }
        return builder.build()
    }
}
