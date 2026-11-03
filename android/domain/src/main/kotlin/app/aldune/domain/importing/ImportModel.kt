package app.aldune.domain.importing

import app.aldune.domain.NoteState
import java.io.ByteArrayInputStream
import java.io.InputStream
import java.time.Instant

/** The colour family a note had in the app it comes from: a hue, not a hex, so an imported note never carries a colour no theme has. */
enum class ImportHue { Yellow, Green, Cyan, Blue, Pink, Orange }

/** One note read from another app, in the common shape every source produces. [text] uses `\n` and its first line is the title. */
data class ImportedNote(
    val text: String,
    val hue: ImportHue?,
    val tags: List<String>,
    val state: NoteState,
    val pinned: Boolean,
    val createdAt: Instant?,
    val updatedAt: Instant?,
)

enum class ImportSkipReason { EMPTY, PROTECTED, TOO_LARGE, NOT_TEXT, UNREADABLE, LIMIT_REACHED }

/** A note (or file) that will not be imported, with the reason, so the preview can say why. */
data class ImportSkip(val name: String, val reason: ImportSkipReason)

enum class ImportFailure { NONE, NOT_FOUND, TOO_LARGE, UNREADABLE, WRONG_FORMAT, NOTHING_FOUND }

/** What a source found, without writing anything. */
data class ImportPreview(
    val notes: List<ImportedNote> = emptyList(),
    val skipped: List<ImportSkip> = emptyList(),
    /** Images, audio, files and drawings that are not imported (Aldune is text only). */
    val attachmentsIgnored: Int = 0,
    val notesWithAttachments: Int = 0,
    val failure: ImportFailure = ImportFailure.NONE,
) {
    companion object {
        fun failed(failure: ImportFailure) = ImportPreview(failure = failure)
    }
}

/**
 * A file the user picked with the system picker. The content is opened lazily and only once per read, so nothing is held in memory
 * until a source needs it. [lastModified] is epoch milliseconds when the provider knows it.
 */
class ImportInput(val name: String, val size: Long, val lastModified: Long?, val open: () -> InputStream) {
    companion object {
        fun ofBytes(name: String, bytes: ByteArray, lastModified: Long? = null) =
            ImportInput(name, bytes.size.toLong(), lastModified) { ByteArrayInputStream(bytes) }
    }
}

enum class ImportSourceKind { KEEP, EVERNOTE, TEXT }

/** A place notes can be imported from. [read] only reads: nothing is written to Aldune. */
interface ImportSource {
    fun read(inputs: List<ImportInput>): ImportPreview
}

object ImportSources {
    fun of(kind: ImportSourceKind): ImportSource = when (kind) {
        ImportSourceKind.KEEP -> KeepTakeoutSource
        ImportSourceKind.EVERNOTE -> EvernoteSource
        ImportSourceKind.TEXT -> TextFilesSource
    }
}

/** The size limits that keep an import from exhausting memory (phones have less of it than the PC, so the byte caps are lower). */
object ImportLimits {
    const val MAX_NOTES = 5000
    const val MAX_NOTE_CHARS = 1024 * 1024
    const val MAX_FILES = 20000
    const val MAX_ENEX_BYTES = 1024L * 1024 * 1024
    const val MAX_ZIP_BYTES = 512L * 1024 * 1024

    /** Total decompressed bytes read out of archives. */
    const val MAX_TOTAL_BYTES = 256L * 1024 * 1024

    /** A Keep note is text; 8 MB of JSON is far beyond a real one and bounds what one entry can cost. */
    const val MAX_ENTRY_BYTES = 8 * 1024 * 1024

    /** Keep's HTML twins held back until their JSON is known not to exist. */
    const val MAX_PENDING_HTML_BYTES = 64L * 1024 * 1024
}

/** Collects what a source finds, enforcing the note limit in one place. */
internal class ImportPreviewBuilder {
    private val notes = ArrayList<ImportedNote>()
    private val skipped = ArrayList<ImportSkip>()
    private var attachments = 0
    private var withAttachments = 0

    /** The failure to report if the whole read finds nothing at all. */
    var lastFailure: ImportFailure? = null

    val count get() = notes.size
    val full get() = notes.size >= ImportLimits.MAX_NOTES

    /** Past this many notes in total the source stops reading altogether (the rest are not even listed). */
    val exhausted get() = notes.size + skipped.size >= ImportLimits.MAX_NOTES + 1000

    fun add(note: ImportedNote, attachmentCount: Int = 0) {
        if (full) { skip("", ImportSkipReason.LIMIT_REACHED); return }
        notes += note
        if (attachmentCount <= 0) return
        attachments += attachmentCount
        withAttachments++
    }

    fun skip(name: String, reason: ImportSkipReason) { skipped += ImportSkip(name, reason) }

    fun build(): ImportPreview {
        val empty = notes.isEmpty() && skipped.isEmpty()
        return ImportPreview(
            notes, skipped, attachments, withAttachments,
            if (empty) lastFailure ?: ImportFailure.NOTHING_FOUND else ImportFailure.NONE,
        )
    }

    companion object {
        /** "Title\nbody" as the text of a note; null when there is nothing to import. */
        fun compose(title: String?, body: String?): String? {
            val t = title?.trim()
            val b = body?.trim('\r', '\n')
            val hasTitle = !t.isNullOrBlank()
            val hasBody = !b.isNullOrBlank()
            if (!hasTitle && !hasBody) return null
            if (!hasBody) return t
            return if (hasTitle) "$t\n$b" else b
        }

        fun lf(text: String) = text.replace("\r\n", "\n").replace('\r', '\n')
    }
}

/** Reads at most [max] bytes; null when the stream has more (so a lying size or a bomb cannot take the memory). */
internal fun InputStream.readBounded(max: Int): ByteArray? {
    val out = java.io.ByteArrayOutputStream()
    val chunk = ByteArray(16 * 1024)
    while (true) {
        val n = read(chunk)
        if (n < 0) break
        if (out.size() + n > max) return null
        out.write(chunk, 0, n)
    }
    return out.toByteArray()
}
