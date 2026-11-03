package app.aldune.domain.backup

import app.aldune.domain.Note
import app.aldune.domain.NoteState
import app.aldune.domain.Reminder
import java.time.Instant

/** What the user chooses when restoring. */
enum class RestoreMode {
    /** Notes whose id is already here are kept as they are; only the missing ones come in. Never deletes. */
    ADD_MISSING,

    /** The notes of this device become exactly the ones of the backup; the rest are deleted (with the usual tombstone for sync). */
    REPLACE_ALL,
}

/** What a restore would do, for the preview (nothing is written to compute it). */
data class RestorePreview(
    val inBackup: Int,
    /** Notes of the backup whose id is not here yet. */
    val missing: Int,
    /** Notes of the backup that are here already. */
    val alreadyHere: Int,
    /** Notes here that [RestoreMode.REPLACE_ALL] would delete. */
    val wouldDelete: Int,
    val protectedInBackup: Int,
    val trashedInBackup: Int,
    val archivedInBackup: Int,
    val reminders: Int,
) {
    companion object {
        fun of(content: BackupContent, existing: List<Note>): RestorePreview {
            val here = existing.mapTo(HashSet()) { it.id }
            val ids = content.notes.mapTo(HashSet()) { it.id }
            val missing = content.notes.count { it.id !in here }
            return RestorePreview(
                inBackup = content.notes.size, missing = missing, alreadyHere = content.notes.size - missing,
                wouldDelete = existing.count { it.id !in ids },
                protectedInBackup = content.notes.count { it.isProtected },
                trashedInBackup = content.notes.count { it.state == NoteState.TRASHED },
                archivedInBackup = content.notes.count { it.state == NoteState.ARCHIVED },
                reminders = content.reminders.size,
            )
        }
    }
}

/** [replaced] are notes that were here with different content and took the backup's; [kept] are those left untouched. */
data class RestoreResult(val added: Int, val replaced: Int, val kept: Int, val deleted: Int)

object BackupReminders {
    /**
     * Reminders worth restoring: one whose time already passed would fire the moment it is set, which nobody wants from an old backup.
     * Only those of notes that are actually restored ([noteIds]).
     */
    fun future(reminders: List<Reminder>, noteIds: Set<String>, now: Instant): List<Reminder> =
        reminders.filter { it.noteId in noteIds && it.dueAt.isAfter(now) }
}

/**
 * The automatic backup taken just before «Replace everything», kept in the app's own files (`files/backups/`).
 * Like every backup it holds the notes in plain text (protected notes stay password-encrypted); it is NOT encrypted with the app's data key,
 * because that key is bound to the database wrapper and a second scheme was not invented. Only the app can read the folder (private storage);
 * the newest three are kept.
 */
object SafetyBackups {
    const val DIRECTORY = "backups"
    private const val PREFIX = "aldune-safety-"
    private val STAMP = java.time.format.DateTimeFormatter.ofPattern("yyyyMMdd-HHmmss").withZone(java.time.ZoneOffset.UTC)

    fun fileName(now: Instant): String = PREFIX + STAMP.format(now) + ".zip"

    /**
     * The files to delete so that only the [keep] newest safety backups stay. Names sort chronologically (UTC stamp), and only names this
     * object could have produced are ever returned: anything else in the folder is not ours to delete.
     */
    fun surplus(names: List<String>, keep: Int = 3): List<String> =
        names.filter { it.startsWith(PREFIX) && it.endsWith(".zip") }.sortedDescending().drop(keep)
}
