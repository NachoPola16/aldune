package app.aldune.domain.importing

import app.aldune.domain.Note
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.text.NoteColorAssigner
import app.aldune.text.NoteColorAssignment
import app.aldune.text.NoteTheme
import app.aldune.text.NoteTone
import app.aldune.text.OklchColor
import java.time.Instant

data class ImportColorPolicy(val theme: NoteTheme, val tone: NoteTone, val rule: NoteColorAssignment, val fixedColor: String?)

/** The notes that would be created and how many were left out because they are already there. */
data class ImportPlan(val toCreate: List<ImportedNote>, val duplicates: Int)

data class ImportResult(val created: Int, val failed: Int)

/**
 * Turns a preview into notes (port of `NoteImporter`). It only ever adds: existing notes are read for the duplicate check and never modified.
 *
 * Nothing in the sync format can remember "this note came from that source" (format 4 must not change), so a note counts as already imported
 * when an existing note has the same text and, if the source knows it, the same original creation date. Importing the same export twice adds
 * nothing; a note edited in Aldune after importing is no longer recognised and would come in again.
 */
object NoteImporter {
    /** A hue further than this from every colour of the theme is not worth matching: the normal assignment is used instead. */
    private const val MAX_HUE_DISTANCE = 50.0

    fun plan(preview: ImportPreview, existing: Iterable<Note>): ImportPlan {
        val known = HashMap<String, MutableList<Long>>()
        for (note in existing) {
            if (note.isProtected || note.text.isBlank()) continue
            known.getOrPut(normalize(note.text)) { ArrayList() } += note.createdAt.epochSecond
        }

        val batch = HashSet<String>()
        val toCreate = ArrayList<ImportedNote>()
        var duplicates = 0
        for (note in preview.notes) {
            val key = normalize(note.text)
            val created = note.createdAt?.epochSecond
            val dates = known[key]
            val already = dates != null && (created == null || created in dates)
            if (already || !batch.add("$created|$key")) { duplicates++; continue }
            toCreate += note
        }
        return ImportPlan(toCreate, duplicates)
    }

    /** One write per note ([NoteService.importNote]); [onProgress] gets the number of notes handled so far. */
    fun run(plan: ImportPlan, service: NoteService, colors: ImportColorPolicy, now: Instant = Instant.now(), onProgress: (Int) -> Unit = {}): ImportResult {
        val assigned = ArrayList<String>()
        var created = 0
        var failed = 0
        for ((index, imported) in plan.toCreate.withIndex()) {
            val color = colorFor(imported.hue, colors, assigned)
            assigned += color

            // Dates from another app can be missing, in the future or out of order; the bin clock restarts so the 30-day purge does not take a
            // note straight to the void just because it was deleted long ago in the other app.
            val createdAt = clamp(imported.createdAt, now) ?: now
            var updatedAt = if (imported.state == NoteState.TRASHED) now else clamp(imported.updatedAt, now) ?: createdAt
            if (updatedAt < createdAt) updatedAt = createdAt

            val result = try {
                service.importNote(imported.text, color, createdAt, updatedAt, imported.state, imported.tags, imported.pinned)
            } catch (_: RuntimeException) {
                null
            }
            if (result is app.aldune.domain.NoteResult.Ok) created++ else failed++
            onProgress(index + 1)
        }
        return ImportResult(created, failed)
    }

    private fun clamp(value: Instant?, now: Instant) = if (value == null) null else if (value > now) now else value

    private fun normalize(text: String) = text.replace("\r\n", "\n").trim()

    private fun colorFor(hue: ImportHue?, policy: ImportColorPolicy, assigned: List<String>): String {
        if (hue != null) closest(policy, hue)?.let { return it }
        return NoteColorAssigner.assign(policy.theme, policy.tone, policy.rule, policy.fixedColor, assigned)
    }

    private fun closest(policy: ImportColorPolicy, hue: ImportHue): String? {
        val theme = policy.theme
        val candidates = when (policy.tone) {
            NoteTone.Dark -> theme.darkColors.ifEmpty { theme.lightColors }
            NoteTone.Light -> theme.lightColors.ifEmpty { theme.darkColors }
            else -> theme.lightColors + theme.darkColors
        }
        val degrees = ImportHues.degrees(hue)
        var best: String? = null
        var bestDistance = Double.MAX_VALUE
        for (candidate in candidates) {
            // A grey theme has no hue to match: the caller falls back to the normal assignment.
            val oklch = OklchColor.fromHex(candidate) ?: continue
            if (oklch.c < ImportHues.NEUTRAL_CHROMA / 3) continue
            val distance = ImportHues.hueDistance(degrees, oklch.h)
            if (distance < bestDistance) { bestDistance = distance; best = candidate }
        }
        return if (bestDistance <= MAX_HUE_DISTANCE) best else null
    }
}
