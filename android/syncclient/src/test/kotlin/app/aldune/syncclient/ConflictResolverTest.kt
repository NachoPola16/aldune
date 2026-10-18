package app.aldune.syncclient

import app.aldune.core.ProtectedNoteContent
import app.aldune.core.ProtectedNotes
import app.aldune.core.SyncNote
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.InMemoryReminderStore
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteError
import app.aldune.domain.NoteResult
import app.aldune.domain.NoteState
import app.aldune.domain.Reminder
import app.aldune.domain.Tombstone
import app.aldune.sync.ConflictVersion
import app.aldune.sync.SyncConflict
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class ConflictResolverTest {
    private val clock = TestClock(Instant.parse("2026-06-01T10:00:00Z"))
    private val notes = InMemoryNoteStore()
    private val conflicts = InMemoryConflictStore()
    private val reminders = InMemoryReminderStore()
    private val resolver = ConflictResolver(notes, conflicts, reminders, clock, DEVICE, TombstoneEraser(notes::removeTombstone))

    private fun wire(t: Instant): OffsetDateTime = OffsetDateTime.ofInstant(t, ZoneOffset.UTC)

    private fun syncNote(
        id: String = ID1,
        text: String = "otra versión",
        color: String = "#AABBCC",
        state: Int = 0,
        tags: List<String> = listOf("Etiqueta"),
        position: Double? = 3.0,
        origin: String = "pantalla-2",
        content: ProtectedNoteContent? = null,
    ) = SyncNote(
        UUID.fromString(id), text, color, wire(T0.minusSeconds(500)), wire(T0.plusSeconds(100)), state, origin, tags,
        isProtected = content != null, protectedContent = content, dockPosition = position,
    )

    private fun noteVersion(n: SyncNote?) = ConflictVersion(n?.updatedAt ?: wire(T0), OTHER_DEVICE, tombstone = n == null, note = n)

    private fun conflict(id: String = ID1, losing: SyncNote?, detectedAt: Instant = T0) = SyncConflict(
        UUID.fromString(id), wire(detectedAt), winner = ConflictVersion(wire(T0), DEVICE, tombstone = false, note = null), losing = noteVersion(losing),
    ).also(conflicts::put)

    /** El ganador de un conflicto con un borrado: la nota viva ya no está. */
    private fun conflictAgainstDelete(id: String = ID1, losing: SyncNote?, detectedAt: Instant = T0) = SyncConflict(
        UUID.fromString(id), wire(detectedAt),
        winner = ConflictVersion(wire(T0), OTHER_DEVICE, tombstone = true, note = null), losing = noteVersion(losing),
    ).also(conflicts::put)

    private fun live(id: String = ID1, text: String = "versión viva", author: String? = null, state: NoteState = NoteState.ACTIVE) =
        note(id = id, text = text, updatedAt = T0, author = author, state = state, color = "#112233", tags = listOf("viva")).also(notes::put)

    // --- kind ---

    @Test fun `kind distingue los tres tipos`() {
        live(ID1)
        val editVsEdit = conflict(ID1, losing = syncNote(ID1))
        assertEquals(ConflictKind.EditVsEdit, resolver.kind(editVsEdit))

        live(ID2)
        val editVsDelete = conflict(ID2, losing = null)
        assertEquals(ConflictKind.EditVsDelete, resolver.kind(editVsDelete))

        val orphan = conflictAgainstDelete(ID3, losing = syncNote(ID3))
        assertEquals(ConflictKind.Orphan, resolver.kind(orphan))
    }

    @Test fun `una nota viva archivada o en la papelera sigue siendo viva para kind`() {
        live(state = NoteState.TRASHED)
        assertEquals(ConflictKind.EditVsEdit, resolver.kind(conflict(losing = syncNote())))
    }

    // --- keepCurrent ---

    @Test fun `keepCurrent en edicion contra edicion descarta el conflicto sin tocar la nota`() {
        val before = live()
        conflict(losing = syncNote())
        val writes = notes.writeCount
        assertIs<NoteResult.Ok<Unit>>(resolver.keepCurrent(ID1))
        assertNull(conflicts.get(ID1))
        assertEquals(before, notes.get(ID1))
        assertEquals(writes, notes.writeCount)
    }

    @Test fun `keepCurrent en edicion contra borrado conserva la nota`() {
        val before = live()
        conflict(losing = null)
        assertIs<NoteResult.Ok<Unit>>(resolver.keepCurrent(ID1))
        assertNull(conflicts.get(ID1))
        assertEquals(before, notes.get(ID1))
        assertTrue(notes.tombstones().isEmpty())
    }

    @Test fun `keepCurrent de una huerfana descarta el conflicto y la nota sigue sin existir`() {
        conflictAgainstDelete(losing = syncNote())
        assertIs<NoteResult.Ok<Unit>>(resolver.keepCurrent(ID1))
        assertNull(conflicts.get(ID1))
        assertNull(notes.get(ID1))
    }

    @Test fun `keepCurrent no quita el recordatorio de la nota`() {
        live()
        conflict(losing = syncNote(state = 2))
        reminders.set(Reminder(ID1, T0.plusSeconds(60)))
        resolver.keepCurrent(ID1)
        assertEquals(1, reminders.all().size)
    }

    // --- useOther: edición contra edición ---

    @Test fun `useOther en edicion contra edicion aplica la otra version como version nueva`() {
        live(author = OTHER_DEVICE)
        conflict(losing = syncNote(text = "la otra", tags = listOf("A", "b"), color = "#AABBCC", state = 1, position = 7.5, origin = "pantalla-9"))
        clock.advanceSeconds(3600)

        assertIs<NoteResult.Ok<Unit>>(resolver.useOther(ID1))

        val result = notes.get(ID1)!!
        assertEquals("la otra", result.text)
        assertEquals(clock.current, result.updatedAt)
        assertNull(result.syncAuthorDeviceId)
        assertEquals(listOf("A", "b"), result.tags)
        assertEquals("#AABBCC", result.color)
        assertEquals(NoteState.ARCHIVED, result.state)
        assertEquals(7.5, result.dockPosition)
        assertEquals("pantalla-9", result.screenOrigin)
        assertEquals(T0.minusSeconds(500), result.createdAt)
        assertNull(conflicts.get(ID1))
        assertTrue(notes.tombstones().isEmpty())
    }

    @Test fun `useOther conserva los saltos de linea CRLF de la otra version`() {
        live()
        conflict(losing = syncNote(text = "a\r\nb\r\nc"))
        resolver.useOther(ID1)
        val result = notes.get(ID1)!!
        assertEquals("a\nb\nc", result.text)
        assertEquals(LineEnding.CRLF, result.lineEnding)
        assertEquals("a\r\nb\r\nc", result.wireText)
    }

    @Test fun `useOther de una perdedora protegida copia el contenido sin desbloquearla`() {
        val content = ProtectedNotes.protect("secreto de la otra", "clave-1234")
        live()
        conflict(losing = syncNote(text = "", content = content))

        assertIs<NoteResult.Ok<Unit>>(resolver.useOther(ID1))

        val result = notes.get(ID1)!!
        assertTrue(result.isProtected)
        assertEquals("", result.text)
        assertEquals(protectedContentToJson(content), result.protectedContentJson)
        assertEquals("secreto de la otra", ProtectedNotes.tryUnprotect(parseProtectedContent(result.protectedContentJson!!, ID1), "clave-1234"))
    }

    @Test fun `useOther de una perdedora normal sobre una nota viva protegida la deja sin proteccion`() {
        live().let { notes.put(it.copy(isProtected = true, text = "", protectedContentJson = PROTECTED_JSON)) }
        conflict(losing = syncNote(text = "en claro"))
        resolver.useOther(ID1)
        val result = notes.get(ID1)!!
        assertEquals(false, result.isProtected)
        assertNull(result.protectedContentJson)
        assertEquals("en claro", result.text)
    }

    // --- useOther: edición contra borrado ---

    @Test fun `useOther en edicion contra borrado borra la nota viva y deja la lapida local`() {
        live()
        conflict(losing = null)
        clock.advanceSeconds(120)

        assertIs<NoteResult.Ok<Unit>>(resolver.useOther(ID1))

        assertNull(notes.get(ID1))
        assertEquals(listOf(Tombstone(ID1, clock.current, Tombstone.LOCAL_DEVICE)), notes.tombstones())
        assertNull(conflicts.get(ID1))
    }

    @Test fun `useOther en edicion contra borrado quita el recordatorio de la nota`() {
        live()
        conflict(losing = null)
        reminders.set(Reminder(ID1, T0.plusSeconds(60)))
        reminders.set(Reminder(ID2, T0.plusSeconds(60)))
        notes.put(note(id = ID2))
        resolver.useOther(ID1)
        assertEquals(listOf(ID2), reminders.all().map { it.noteId })
    }

    // --- useOther: huérfana ---

    @Test fun `useOther de una huerfana recupera la nota y quita su lapida`() {
        notes.putTombstone(Tombstone(ID1, T0, OTHER_DEVICE))
        conflictAgainstDelete(losing = syncNote(text = "recuperada"))
        clock.advanceSeconds(30)

        assertIs<NoteResult.Ok<Unit>>(resolver.useOther(ID1))

        val result = notes.get(ID1)!!
        assertEquals("recuperada", result.text)
        assertEquals(clock.current, result.updatedAt)
        assertNull(result.syncAuthorDeviceId)
        assertTrue(notes.tombstones().isEmpty(), "con la lápida, la sync seguiría mandando al borrado")
        assertNull(conflicts.get(ID1))
    }

    @Test fun `useOther de una huerfana archivada no deja recordatorio`() {
        conflictAgainstDelete(losing = syncNote(state = 1))
        reminders.set(Reminder(ID1, T0.plusSeconds(60)))
        resolver.useOther(ID1)
        assertTrue(reminders.all().isEmpty())
        assertEquals(NoteState.ARCHIVED, notes.get(ID1)!!.state)
    }

    @Test fun `useOther de una huerfana activa conserva su recordatorio`() {
        conflictAgainstDelete(losing = syncNote(state = 0))
        reminders.set(Reminder(ID1, T0.plusSeconds(60)))
        resolver.useOther(ID1)
        assertEquals(1, reminders.all().size)
    }

    @Test fun `useOther que deja la nota en la papelera quita el recordatorio`() {
        live()
        conflict(losing = syncNote(state = 2))
        reminders.set(Reminder(ID1, T0.plusSeconds(60)))
        resolver.useOther(ID1)
        assertTrue(reminders.all().isEmpty())
    }

    // --- orphans ---

    @Test fun `orphans trae solo los huerfanos, los mas recientes primero`() {
        live(ID1)
        conflict(ID1, losing = syncNote(ID1), detectedAt = T0.plusSeconds(500))
        conflictAgainstDelete(ID2, losing = syncNote(ID2), detectedAt = T0.plusSeconds(10))
        conflictAgainstDelete(ID3, losing = syncNote(ID3), detectedAt = T0.plusSeconds(20))
        assertEquals(listOf(ID3, ID2), resolver.orphans().map { it.noteId.toString() })
    }

    // --- idempotencia y errores: no escribe ---

    @Test fun `resolver dos veces el mismo conflicto es NotFound la segunda y no escribe`() {
        live()
        conflict(losing = syncNote())
        assertIs<NoteResult.Ok<Unit>>(resolver.useOther(ID1))
        val writes = notes.writeCount
        val after = notes.get(ID1)

        assertEquals(NoteError.NotFound(ID1), resolver.useOther(ID1).errorOrNull())
        assertEquals(NoteError.NotFound(ID1), resolver.keepCurrent(ID1).errorOrNull())
        assertEquals(writes, notes.writeCount)
        assertEquals(after, notes.get(ID1))
    }

    @Test fun `useOther de un conflicto que no existe no escribe nada`() {
        live()
        val writes = notes.writeCount
        assertEquals(NoteError.NotFound(ID1), resolver.useOther(ID1).errorOrNull())
        assertEquals(writes, notes.writeCount)
        assertEquals(0, conflicts.writeCount)
    }

    @Test fun `una perdedora que no se puede representar falla sin escribir y deja el conflicto`() {
        live()
        conflict(losing = syncNote(state = 9))
        val writes = notes.writeCount
        assertIs<NoteError.InvalidArgument>(resolver.useOther(ID1).errorOrNull())
        assertEquals(writes, notes.writeCount)
        assertTrue(conflicts.get(ID1) != null)
    }

    @Test fun `una perdedora protegida sin contenido tampoco se aplica`() {
        live()
        val broken = syncNote().copy(isProtected = true, protectedContent = null)
        conflict(losing = broken)
        val writes = notes.writeCount
        assertIs<NoteError.InvalidArgument>(resolver.useOther(ID1).errorOrNull())
        assertEquals(writes, notes.writeCount)
    }

    @Test fun `un conflicto sin nota ni borrado como perdedora no se aplica`() {
        live()
        SyncConflict(
            UUID.fromString(ID1), wire(T0), ConflictVersion(wire(T0), DEVICE, false, null),
            ConflictVersion(wire(T0), OTHER_DEVICE, tombstone = false, note = null),
        ).also(conflicts::put)
        val writes = notes.writeCount
        assertIs<NoteError.InvalidArgument>(resolver.useOther(ID1).errorOrNull())
        assertEquals(writes, notes.writeCount)
        // Descartarlo sí se puede: es la salida de un conflicto que no se sabe aplicar.
        assertIs<NoteResult.Ok<Unit>>(resolver.keepCurrent(ID1))
    }

    @Test fun `resolver un conflicto no toca los de otras notas`() {
        live(ID1)
        live(ID2)
        conflict(ID1, losing = syncNote(ID1))
        conflict(ID2, losing = syncNote(ID2))
        resolver.useOther(ID1)
        assertEquals(listOf(ID2), conflicts.all().map { it.noteId.toString() })
        assertEquals("versión viva", notes.get(ID2)!!.text)
    }
}
