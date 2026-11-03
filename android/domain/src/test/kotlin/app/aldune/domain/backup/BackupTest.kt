package app.aldune.domain.backup

import app.aldune.domain.Fixture
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteState
import app.aldune.domain.Reminder
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.time.Instant
import java.util.zip.ZipEntry
import java.util.zip.ZipOutputStream
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class BackupTest {
    private val at = Instant.parse("2026-11-03T10:00:00Z")

    private fun note(id: String, text: String = "hola", state: NoteState = NoteState.ACTIVE) = Note(
        id = id, text = text, lineEnding = LineEnding.LF, color = "#FFDDD4",
        createdAt = Instant.parse("2026-01-01T00:00:00.123456700Z"), updatedAt = Instant.parse("2026-02-01T00:00:00Z"), state = state,
    )

    private fun roundTrip(content: BackupContent): BackupReadResult {
        val out = ByteArrayOutputStream()
        BackupArchive.write(out, content, "3.3.0", at)
        return BackupArchive.read(ByteArrayInputStream(out.toByteArray()))
    }

    private fun zip(vararg entries: Pair<String, String>): ByteArray {
        val out = ByteArrayOutputStream()
        ZipOutputStream(out).use { z -> for ((n, c) in entries) { z.putNextEntry(ZipEntry(n)); z.write(c.toByteArray()); z.closeEntry() } }
        return out.toByteArray()
    }

    private fun manifest(version: Int, kind: String = BackupArchive.KIND) = """{"Kind":"$kind","FormatVersion":$version}"""

    @Test
    fun `every field of every kind of note survives a round trip`() {
        val rich = note("a", "line1\nline2", NoteState.ARCHIVED).copy(
            lineEnding = LineEnding.CRLF, tags = listOf("Work", "ideas"), dockPosition = 2.5, screenOrigin = "DISPLAY1",
            pinnedToTop = true, completedTasks = 2, color = "#abcdef",
        )
        val trashed = note("b", "bin", NoteState.TRASHED)
        val locked = note("c", "", NoteState.ACTIVE).copy(isProtected = true, protectedContentJson = """{"Salt":"x","Cipher":"y"}""", protectedTitle = "Bank")
        val content = BackupContent(
            listOf(rich, trashed, locked), listOf(Reminder("a", Instant.ofEpochMilli(1_900_000_000_000))),
            mapOf("appearance" to 3, "plainEditor" to true, "language" to "es"),
        )
        val read = roundTrip(content) as BackupReadResult.Ok
        assertEquals(content.notes, read.content.notes) // data-class equality: dates to the nanosecond, tags, order, protected payload
        assertEquals(content.reminders, read.content.reminders)
        assertEquals(content.settings, read.content.settings)
        assertEquals(0, read.skippedNotes)
        assertEquals(1, read.manifest.formatVersion)
        assertEquals("3.3.0", read.manifest.appVersion)
        assertEquals(at, read.manifest.createdAt)
    }

    @Test
    fun `a protected note is written exactly as stored and its text never appears`() {
        val locked = note("c", "").copy(isProtected = true, protectedContentJson = "CIPHERTEXT-AS-STORED", protectedTitle = null)
        val out = ByteArrayOutputStream()
        BackupArchive.write(out, BackupContent(listOf(locked), emptyList()), "3.3.0", at)
        val back = (BackupArchive.read(ByteArrayInputStream(out.toByteArray())) as BackupReadResult.Ok).content.notes.single()
        assertEquals("CIPHERTEXT-AS-STORED", back.protectedContentJson)
        assertEquals("", back.text)
    }

    @Test
    fun `the manifest counts notes and says whether settings are included`() {
        val out = ByteArrayOutputStream()
        BackupArchive.write(out, BackupContent(listOf(note("a"), note("b")), emptyList()), "3.3.0", at)
        val manifest = java.util.zip.ZipInputStream(ByteArrayInputStream(out.toByteArray())).use { z ->
            generateSequence { z.nextEntry }.first { it.name == BackupArchive.MANIFEST_ENTRY }.let { z.readBytes().toString(Charsets.UTF_8) }
        }
        assertTrue(manifest.contains("\"NoteCount\": 2"))
        assertTrue(manifest.contains("\"HasSettings\": false"))
        assertTrue(manifest.contains("\"FormatVersion\": 1"))
    }

    @Test
    fun `a backup of a newer format is refused`() {
        val bytes = zip("manifest.json" to manifest(BackupArchive.FORMAT_VERSION + 1), "notes.json" to """{"Notes":[]}""")
        assertEquals(BackupReadResult.Failed(BackupFailure.TOO_NEW), BackupArchive.read(ByteArrayInputStream(bytes)))
    }

    @Test
    fun `things that are not our backup are refused`() {
        assertEquals(BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP), BackupArchive.read(ByteArrayInputStream("not a zip".toByteArray())))
        assertEquals(BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP), BackupArchive.read(ByteArrayInputStream(zip("notas/a.md" to "# x"))))
        assertEquals(
            BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP),
            BackupArchive.read(ByteArrayInputStream(zip("manifest.json" to manifest(1, "something-else"), "notes.json" to """{"Notes":[]}"""))),
        )
        assertEquals(BackupReadResult.Failed(BackupFailure.NOT_A_BACKUP), BackupArchive.read(ByteArrayInputStream(zip("manifest.json" to manifest(0)))))
    }

    @Test
    fun `a truncated zip is not a backup and does not throw`() {
        val out = ByteArrayOutputStream()
        BackupArchive.write(out, BackupContent(listOf(note("a")), emptyList()), "3.3.0", at)
        val bytes = out.toByteArray()
        val result = BackupArchive.read(ByteArrayInputStream(bytes.copyOf(bytes.size / 2)))
        assertTrue(result is BackupReadResult.Failed)
    }

    @Test
    fun `missing or broken notes are a corrupt backup`() {
        assertEquals(BackupReadResult.Failed(BackupFailure.CORRUPT), BackupArchive.read(ByteArrayInputStream(zip("manifest.json" to manifest(1)))))
        assertEquals(
            BackupReadResult.Failed(BackupFailure.CORRUPT),
            BackupArchive.read(ByteArrayInputStream(zip("manifest.json" to manifest(1), "notes.json" to """{"Notes":[{"Id":""}]}"""))),
        )
    }

    @Test
    fun `an unreadable note or a repeated id is skipped and the rest is kept`() {
        val good = """{"Id":"a","Text":"x","Color":"#FFFFFF","CreatedAt":"2026-01-01T00:00:00Z","UpdatedAt":"2026-01-01T00:00:00Z","State":0}"""
        val badState = good.replace("\"State\":0", "\"State\":9").replace("\"a\"", "\"b\"")
        val emptyProtected = good.replace("\"a\"", "\"c\"").replace("\"State\":0", "\"State\":0,\"IsProtected\":true")
        val bytes = zip(
            "manifest.json" to manifest(1),
            "notes.json" to """{"Notes":[$good,$badState,$emptyProtected,$good]}""",
        )
        val read = BackupArchive.read(ByteArrayInputStream(bytes)) as BackupReadResult.Ok
        assertEquals(listOf("a"), read.content.notes.map { it.id })
        assertEquals(3, read.skippedNotes)
    }

    @Test
    fun `unknown keys and entries are ignored so a later compatible version still reads`() {
        val n = """{"Id":"a","Text":"x","Color":"#FFFFFF","CreatedAt":"2026-01-01T00:00:00Z","UpdatedAt":"2026-01-01T00:00:00Z","State":0,"Future":{"a":1}}"""
        val bytes = zip("manifest.json" to manifest(1), "notes.json" to """{"Notes":[$n],"Other":1}""", "extra.bin" to "zzz")
        assertEquals(1, (BackupArchive.read(ByteArrayInputStream(bytes)) as BackupReadResult.Ok).content.notes.size)
    }

    @Test
    fun `reminders of unknown notes and settings of the wrong kind are dropped`() {
        val n = """{"Id":"a","Text":"x","Color":"#FFFFFF","CreatedAt":"2026-01-01T00:00:00Z","UpdatedAt":"2026-01-01T00:00:00Z","State":0}"""
        val bytes = zip(
            "manifest.json" to manifest(1), "notes.json" to """{"Notes":[$n]}""",
            "reminders.json" to """{"Reminders":[{"NoteId":"a","DueAtEpochMillis":5},{"NoteId":"zz","DueAtEpochMillis":6},{"NoteId":"a"}]}""",
            "settings.json" to """{"Values":{"ok":1,"flag":true,"s":"t","nested":{"x":1},"f":1.5,"list":[1]}}""",
        )
        val read = BackupArchive.read(ByteArrayInputStream(bytes)) as BackupReadResult.Ok
        assertEquals(listOf(Reminder("a", Instant.ofEpochMilli(5))), read.content.reminders)
        assertEquals(mapOf("ok" to 1, "flag" to true, "s" to "t"), read.content.settings)
    }

    @Test
    fun `an entry bigger than the limit is refused`() {
        // Cheap to build: a long run of one byte deflates to almost nothing but inflates past the cap.
        val out = ByteArrayOutputStream()
        ZipOutputStream(out).use { z ->
            z.putNextEntry(ZipEntry("manifest.json")); z.write(manifest(1).toByteArray()); z.closeEntry()
            z.putNextEntry(ZipEntry("notes.json"))
            val chunk = ByteArray(1024 * 1024) { 'a'.code.toByte() }
            repeat(257) { z.write(chunk) }
            z.closeEntry()
        }
        assertEquals(BackupReadResult.Failed(BackupFailure.TOO_LARGE), BackupArchive.read(ByteArrayInputStream(out.toByteArray())))
    }

    // ---- Preview and restore ----

    @Test
    fun `the preview counts what is missing, what is here and what a replace would delete`() {
        val here = listOf(note("a"), note("x"))
        val content = BackupContent(listOf(note("a"), note("b", state = NoteState.TRASHED), note("c", state = NoteState.ARCHIVED)), emptyList())
        val p = RestorePreview.of(content, here)
        assertEquals(RestorePreview(3, 2, 1, 1, 0, 1, 1, 0), p)
    }

    private fun fixtureWith(vararg notes: Note): Fixture = Fixture().also { fx -> notes.forEach(fx.store::put) }

    @Test
    fun `add missing keeps what is here and never deletes`() {
        val fx = fixtureWith(note("a", "mine"), note("x", "only here"))
        val (result, written) = fx.service.restoreBackup(listOf(note("a", "older"), note("b", "new")), RestoreMode.ADD_MISSING)
        assertEquals(RestoreResult(added = 1, replaced = 0, kept = 1, deleted = 0), result)
        assertEquals(setOf("b"), written)
        assertEquals("mine", fx.stored("a").text)
        assertEquals("only here", fx.stored("x").text)
        assertEquals("new", fx.stored("b").text)
        assertTrue(fx.store.tombstones().isEmpty())
    }

    @Test
    fun `an added note keeps its dates and state and becomes a local edit for the sync`() {
        val fx = Fixture()
        val incoming = note("b", "new", NoteState.TRASHED).copy(syncAuthorDeviceId = "windows-1", pinnedToTop = true)
        fx.service.restoreBackup(listOf(incoming), RestoreMode.ADD_MISSING)
        val stored = fx.stored("b")
        assertEquals(incoming.updatedAt, stored.updatedAt)
        assertEquals(NoteState.TRASHED, stored.state)
        assertTrue(stored.pinnedToTop)
        assertNull(stored.syncAuthorDeviceId)
    }

    @Test
    fun `replace everything deletes the rest with tombstones and overwrites what differs`() {
        val fx = fixtureWith(note("a", "mine"), note("x", "only here"), note("s", "same"))
        val (result, written) = fx.service.restoreBackup(
            listOf(note("a", "from backup"), note("s", "same"), note("b", "new")), RestoreMode.REPLACE_ALL,
        )
        assertEquals(RestoreResult(added = 1, replaced = 1, kept = 1, deleted = 1), result)
        assertEquals(setOf("a", "b"), written)
        assertEquals("from backup", fx.stored("a").text)
        assertEquals(fx.clock.now(), fx.stored("a").updatedAt) // so the sync does not hand the server's newer copy straight back
        assertNull(fx.store.get("x"))
        assertEquals(listOf("x"), fx.store.tombstones().map { it.noteId })
        assertEquals(note("s", "same"), fx.stored("s")) // untouched
    }

    @Test
    fun `replace everything with an empty backup is a total replace`() {
        val fx = fixtureWith(note("a"), note("b"))
        val (result, _) = fx.service.restoreBackup(emptyList(), RestoreMode.REPLACE_ALL)
        assertEquals(2, result.deleted)
        assertTrue(fx.store.all().isEmpty())
    }

    @Test
    fun `a restored note drops the local tombstone of its id`() {
        val fx = Fixture()
        val cleared = mutableListOf<String>()
        fx.service.restoreBackup(listOf(note("b")), RestoreMode.ADD_MISSING) { cleared += it }
        assertEquals(listOf("b"), cleared)
    }

    @Test
    fun `only future reminders of restored notes are worth restoring`() {
        val now = Instant.parse("2026-11-03T10:00:00Z")
        val list = listOf(Reminder("a", now.plusSeconds(60)), Reminder("a2", now.minusSeconds(60)), Reminder("zz", now.plusSeconds(60)))
        assertEquals(listOf(Reminder("a", now.plusSeconds(60))), BackupReminders.future(list, setOf("a", "a2"), now))
        assertFalse(BackupReminders.future(list, emptySet(), now).isNotEmpty())
    }

    @Test
    fun `safety backups are named by UTC time and only the newest three stay`() {
        assertEquals("aldune-safety-20261103-101500.zip", SafetyBackups.fileName(Instant.parse("2026-11-03T10:15:00Z")))
        val names = listOf(
            "aldune-safety-20261101-000000.zip", "aldune-safety-20261103-000000.zip", "aldune-safety-20261102-000000.zip",
            "aldune-safety-20261104-000000.zip", "notes.txt", "aldune-safety-20261031-000000.zip.tmp",
        )
        assertEquals(listOf("aldune-safety-20261101-000000.zip"), SafetyBackups.surplus(names))
    }
}
