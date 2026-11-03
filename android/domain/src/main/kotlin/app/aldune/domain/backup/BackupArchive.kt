package app.aldune.domain.backup

import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteState
import app.aldune.domain.Reminder
import java.io.InputStream
import java.io.OutputStream
import java.time.Instant
import java.util.zip.ZipEntry
import java.util.zip.ZipInputStream
import java.util.zip.ZipOutputStream
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.booleanOrNull
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.doubleOrNull
import kotlinx.serialization.json.intOrNull
import kotlinx.serialization.json.longOrNull
import kotlinx.serialization.json.put

/**
 * The full backup of the notes of this device: ONE portable `.zip` (like the Windows backup, which also is a single zip with a small manifest),
 * written to wherever the system file picker points.
 *
 * ```
 * manifest.json   {"Kind":"aldune-android-backup","FormatVersion":1,"AppVersion":"3.3.0","CreatedAtUtc":"...Z","NoteCount":N,
 *                  "ReminderCount":R,"HasSettings":true}
 * notes.json      {"Notes":[{"Id","Text","LineEnding":"LF|CRLF","Color","CreatedAt","UpdatedAt","State":0|1|2,"Tags":[],
 *                            "DockPosition":1.5|null,"ScreenOrigin","IsProtected","ProtectedContent":"..."|null,"ProtectedTitle":"..."|null,
 *                            "PinnedToTop","CompletedTasks"}]}
 * reminders.json  {"Reminders":[{"NoteId","DueAtEpochMillis"}]}
 * settings.json   {"Values":{"<preference key>": true | 12 | "text"}}   // device-bound and secret values are never written
 * ```
 *
 * Trashed and archived notes are included with their state. A password-protected note is stored EXACTLY as it is in the database: its
 * `ProtectedContent` is the password-encrypted JSON and is never decrypted here, so reading it back needs the same password. Every other note
 * is written as plain text (that is what makes the file portable), so the zip must be kept like the notes themselves. Sync keys, tokens and
 * the app's data key are never part of it: after restoring on a new phone the sync is set up again with the profile code.
 *
 * The format version is checked on read: a zip written by a newer format is refused ([BackupFailure.TOO_NEW]) instead of being half understood.
 * Unknown keys are ignored, so a later, compatible version can add fields without bumping the version.
 */
object BackupArchive {
    const val KIND = "aldune-android-backup"
    const val FORMAT_VERSION = 1
    const val MANIFEST_ENTRY = "manifest.json"
    const val NOTES_ENTRY = "notes.json"
    const val REMINDERS_ENTRY = "reminders.json"
    const val SETTINGS_ENTRY = "settings.json"

    /** Per entry, after inflating: a corrupt or hostile zip must not fill the memory of the phone. 256 MB is far above any real set of notes. */
    const val MAX_ENTRY_BYTES: Long = 256L * 1024 * 1024
    const val MAX_NOTES = 200_000

    private val WANTED = setOf(MANIFEST_ENTRY, NOTES_ENTRY, REMINDERS_ENTRY, SETTINGS_ENTRY)
    private val PRETTY = Json { prettyPrint = true }

    fun write(out: OutputStream, content: BackupContent, appVersion: String, createdAt: Instant) {
        val zip = ZipOutputStream(out, Charsets.UTF_8)
        zip.entry(MANIFEST_ENTRY, buildJsonObject {
            put("Kind", KIND)
            put("FormatVersion", FORMAT_VERSION)
            put("AppVersion", appVersion)
            put("CreatedAtUtc", createdAt.toString())
            put("NoteCount", content.notes.size)
            put("ReminderCount", content.reminders.size)
            put("HasSettings", content.settings.isNotEmpty())
        })
        zip.entry(NOTES_ENTRY, buildJsonObject { put("Notes", buildJsonArray { content.notes.forEach { add(noteJson(it)) } }) })
        zip.entry(REMINDERS_ENTRY, buildJsonObject {
            put("Reminders", buildJsonArray {
                content.reminders.forEach { add(buildJsonObject { put("NoteId", it.noteId); put("DueAtEpochMillis", it.dueAt.toEpochMilli()) }) }
            })
        })
        if (content.settings.isNotEmpty()) {
            zip.entry(SETTINGS_ENTRY, buildJsonObject {
                put("Values", buildJsonObject {
                    for ((key, value) in content.settings.toSortedMap()) when (value) {
                        is Boolean -> put(key, value)
                        is Int -> put(key, value)
                        is String -> put(key, value)
                        else -> {} // only the three kinds a preference file holds in this app
                    }
                })
            })
        }
        zip.finish() // the caller owns [out] (the file descriptor of the picker)
    }

    private fun ZipOutputStream.entry(name: String, json: JsonObject) {
        putNextEntry(ZipEntry(name))
        write(PRETTY.encodeToString(JsonObject.serializer(), json).toByteArray(Charsets.UTF_8))
        closeEntry()
    }

    private fun noteJson(n: Note) = buildJsonObject {
        put("Id", n.id)
        put("Text", n.text)
        put("LineEnding", if (n.lineEnding == LineEnding.CRLF) "CRLF" else "LF")
        put("Color", n.color)
        put("CreatedAt", n.createdAt.toString())
        put("UpdatedAt", n.updatedAt.toString())
        put("State", n.state.code)
        put("Tags", buildJsonArray { n.tags.forEach { add(JsonPrimitive(it)) } })
        put("DockPosition", n.dockPosition?.let { JsonPrimitive(it) } ?: JsonNull)
        put("ScreenOrigin", n.screenOrigin)
        put("IsProtected", n.isProtected)
        put("ProtectedContent", n.protectedContentJson?.let { JsonPrimitive(it) } ?: JsonNull)
        put("ProtectedTitle", n.protectedTitle?.let { JsonPrimitive(it) } ?: JsonNull)
        put("PinnedToTop", n.pinnedToTop)
        put("CompletedTasks", n.completedTasks)
    }

    /** Reads and validates a backup. Never throws for bad data: the answer says what was wrong. */
    fun read(input: InputStream): BackupReadResult {
        val entries = HashMap<String, ByteArray>()
        try {
            ZipInputStream(input, Charsets.UTF_8).use { zip ->
                while (true) {
                    val e = zip.nextEntry ?: break
                    if (e.isDirectory || e.name !in WANTED) continue
                    entries[e.name] = readBounded(zip) ?: return BackupReadResult.Failed(BackupFailure.TOO_LARGE)
                }
            }
        } catch (_: Exception) {
            return BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP) // not a zip, or truncated
        }
        val manifestJson = entries[MANIFEST_ENTRY]?.let(::parseObject) ?: return BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP)
        if (manifestJson.str("Kind") != KIND) return BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP)
        val version = manifestJson.int("FormatVersion") ?: return BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP)
        if (version > FORMAT_VERSION) return BackupReadResult.Failed(BackupFailure.TOO_NEW)
        if (version < 1) return BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP)
        val created = manifestJson.str("CreatedAtUtc")?.let { runCatching { Instant.parse(it) }.getOrNull() }
        val manifest = BackupManifest(version, manifestJson.str("AppVersion").orEmpty(), created)

        val notesRoot = entries[NOTES_ENTRY]?.let(::parseObject) ?: return BackupReadResult.Failed(BackupFailure.CORRUPT)
        val rawNotes = notesRoot["Notes"] as? JsonArray ?: return BackupReadResult.Failed(BackupFailure.CORRUPT)
        if (rawNotes.size > MAX_NOTES) return BackupReadResult.Failed(BackupFailure.TOO_LARGE)
        val notes = ArrayList<Note>(rawNotes.size)
        val seen = HashSet<String>()
        var skipped = 0
        for (raw in rawNotes) {
            val note = (raw as? JsonObject)?.let(::parseNote)
            // A bad or repeated note costs only itself; the rest of the backup stays restorable.
            if (note == null || !seen.add(note.id)) skipped++ else notes += note
        }
        // Only bad notes in a non-empty array means the file is damaged, not that the user had no notes.
        if (notes.isEmpty() && skipped > 0) return BackupReadResult.Failed(BackupFailure.CORRUPT)

        val reminders = (entries[REMINDERS_ENTRY]?.let(::parseObject)?.get("Reminders") as? JsonArray).orEmpty().mapNotNull { r ->
            val o = r as? JsonObject ?: return@mapNotNull null
            val id = o.str("NoteId")?.takeIf { it in seen } ?: return@mapNotNull null
            val millis = o.long("DueAtEpochMillis") ?: return@mapNotNull null
            Reminder(id, Instant.ofEpochMilli(millis))
        }
        val settings = LinkedHashMap<String, Any>()
        (entries[SETTINGS_ENTRY]?.let(::parseObject)?.get("Values") as? JsonObject)?.forEach { (key, v) ->
            val p = v as? JsonPrimitive ?: return@forEach
            val bool = p.booleanOrNull
            val int = p.intOrNull
            when {
                p.isString -> settings[key] = p.content
                bool != null -> settings[key] = bool
                int != null -> settings[key] = int
            }
        }
        return BackupReadResult.Ok(manifest, BackupContent(notes, reminders, settings), skipped)
    }

    /** Inflates one entry, or `null` when it is bigger than [MAX_ENTRY_BYTES]. */
    private fun readBounded(zip: ZipInputStream): ByteArray? {
        val out = java.io.ByteArrayOutputStream()
        val buffer = ByteArray(64 * 1024)
        var total = 0L
        while (true) {
            val n = zip.read(buffer)
            if (n < 0) break
            total += n
            if (total > MAX_ENTRY_BYTES) return null
            out.write(buffer, 0, n)
        }
        return out.toByteArray()
    }

    private fun parseObject(bytes: ByteArray): JsonObject? =
        runCatching { Json.parseToJsonElement(bytes.toString(Charsets.UTF_8)) }.getOrNull() as? JsonObject

    private fun JsonObject.str(key: String): String? = (this[key] as? JsonPrimitive)?.takeIf { it.isString }?.content
    private fun JsonObject.int(key: String): Int? = (this[key] as? JsonPrimitive)?.takeIf { !it.isString }?.intOrNull
    private fun JsonObject.long(key: String): Long? = (this[key] as? JsonPrimitive)?.takeIf { !it.isString }?.longOrNull
    private fun JsonObject.bool(key: String): Boolean? = (this[key] as? JsonPrimitive)?.takeIf { !it.isString }?.booleanOrNull

    private fun parseNote(o: JsonObject): Note? {
        val id = o.str("Id")?.takeIf { it.isNotBlank() } ?: return null
        val color = o.str("Color")?.takeIf { it.isNotBlank() } ?: return null
        val created = o.str("CreatedAt")?.let { runCatching { Instant.parse(it) }.getOrNull() } ?: return null
        val updated = o.str("UpdatedAt")?.let { runCatching { Instant.parse(it) }.getOrNull() } ?: return null
        val state = o.int("State")?.let(NoteState::fromCode) ?: return null
        val isProtected = o.bool("IsProtected") ?: false
        val protectedContent = o.str("ProtectedContent")
        // A protected note without its encrypted payload would be an empty shell that looks protected and holds nothing.
        if (isProtected && protectedContent.isNullOrEmpty()) return null
        val tags = (o["Tags"] as? JsonArray).orEmpty().mapNotNull { (it as? JsonPrimitive)?.takeIf { p -> p.isString }?.content }
        return Note(
            id = id,
            text = LineEnding.normalize(if (isProtected) "" else o.str("Text").orEmpty()),
            lineEnding = if (o.str("LineEnding") == "CRLF") LineEnding.CRLF else LineEnding.LF,
            color = color, createdAt = created, updatedAt = updated, state = state, tags = tags,
            dockPosition = (o["DockPosition"] as? JsonPrimitive)?.takeIf { !it.isString }?.doubleOrNull,
            screenOrigin = o.str("ScreenOrigin").orEmpty(),
            syncAuthorDeviceId = null, // a restored note is, for the sync, an edit made on this device
            isProtected = isProtected, protectedContentJson = if (isProtected) protectedContent else null,
            protectedTitle = o.str("ProtectedTitle"),
            pinnedToTop = o.bool("PinnedToTop") ?: false,
            completedTasks = o.int("CompletedTasks") ?: 0,
        )
    }
}

/** What a backup holds. [settings] are portable preference values (see `SettingsStore.backupValues`), never secrets. */
data class BackupContent(val notes: List<Note>, val reminders: List<Reminder>, val settings: Map<String, Any> = emptyMap())

data class BackupManifest(val formatVersion: Int, val appVersion: String, val createdAt: Instant?)

enum class BackupFailure {
    /** Not a zip, or a zip without a manifest of ours (for instance the Windows backup, or a Markdown export). */
    NOT_A_BACKUP,

    /** Written by a newer format version: restoring could lose data, so it is refused. */
    TOO_NEW,

    /** Manifest fine but the notes cannot be read. */
    CORRUPT,

    /** An entry or the note count is beyond the limits. */
    TOO_LARGE,
}

sealed class BackupReadResult {
    /** [skippedNotes] counts notes of the file that were unreadable or repeated; the others are in [content]. */
    data class Ok(val manifest: BackupManifest, val content: BackupContent, val skippedNotes: Int) : BackupReadResult()
    data class Failed(val reason: BackupFailure) : BackupReadResult()
}
