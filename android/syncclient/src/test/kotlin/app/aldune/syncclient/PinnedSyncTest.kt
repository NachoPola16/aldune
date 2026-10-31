package app.aldune.syncclient

import app.aldune.core.NoteState as WireState
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import app.aldune.domain.DockOrder
import app.aldune.domain.IdFactory
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.sync.InMemorySyncTransport
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue

/**
 * «Fijada arriba» a través de la sync completa (servicio + almacén local + transporte en memoria): el pin que llega del PC, el que
 * sale hacia el PC, los conflictos, la papelera, el archivo y el orden manual. Los vectores fijan el sobre; esto fija lo que
 * hace el motor con él. Spec: docs/specs/2026-10-31-pinned-notes-design.md.
 */
class PinnedSyncTest {
    private val remote = InMemorySyncTransport()
    private val clock = TestClock(Instant.parse("2026-03-01T12:00:00Z"))

    private inner class Phone(val deviceId: String) {
        val notes = InMemoryNoteStore()
        val store = InMemoryLocalSyncStore(notes)
        val service = NoteService(notes, clock, deviceId, IdFactory { UUID.randomUUID().toString() })

        fun syncOk(): SyncOutcome.Success = assertIs<SyncOutcome.Success>(runSync(store, remote, TEST_KEY, SyncClientSettings(), deviceId, clock))
    }

    private val phone = Phone(DEVICE)

    private fun wire(
        id: String, text: String, updatedAt: Instant, pinned: Boolean = false, state: Int = WireState.ACTIVE, position: Double? = null,
    ) = SyncNote(
        UUID.fromString(id), text, "#FFDDD4", OffsetDateTime.ofInstant(T0, ZoneOffset.UTC), OffsetDateTime.ofInstant(updatedAt, ZoneOffset.UTC),
        state, "", emptyList(), false, null, position, pinnedToTop = pinned,
    )

    /** Lo que escribiría el PC: otro dispositivo con la misma clave. */
    private fun putPc(n: SyncNote): SyncEnvelope {
        val envelope = SyncEnvelopeCodec.createNote(n, TEST_KEY, OTHER_DEVICE)
        remote.store[envelope.noteId] = envelope
        return envelope
    }

    private fun remoteNote(id: String): SyncNote = SyncEnvelopeCodec.decryptNote(remote.store.getValue(UUID.fromString(id)), TEST_KEY)

    private fun later(seconds: Long = 60) = clock.advanceSeconds(seconds)

    @Test fun `un pin que llega del PC queda guardado y la nota sale la primera de la lista`() {
        putPc(wire(ID1, "normal", T0.plusSeconds(10)))
        putPc(wire(ID2, "fijada", T0.plusSeconds(10), pinned = true))

        phone.syncOk()

        assertTrue(phone.notes.get(ID2)!!.pinnedToTop)
        assertFalse(phone.notes.get(ID1)!!.pinnedToTop)
        assertEquals(listOf(ID2, ID1), DockOrder.sort(phone.notes.all()).map { it.id })
    }

    @Test fun `un pin hecho aqui sube dentro del sobre y no cambia el texto`() {
        val created = phone.service.create("hola").getOrThrow()
        later()
        phone.syncOk()
        assertFalse(remoteNote(created.id).pinnedToTop)

        later()
        phone.service.setPinned(created.id, true)
        later()
        val outcome = phone.syncOk()

        assertEquals(1, outcome.uploaded)
        val sent = remoteNote(created.id)
        assertTrue(sent.pinnedToTop)
        assertEquals("hola", sent.text)
    }

    @Test fun `desfijar tras una sync llega al PC y la pasada siguiente no hace nada`() {
        val created = phone.service.create("hola").getOrThrow()
        phone.service.setPinned(created.id, true)
        later()
        phone.syncOk()
        assertTrue(remoteNote(created.id).pinnedToTop)

        later()
        phone.service.setPinned(created.id, false)
        later()
        phone.syncOk()

        assertFalse(remoteNote(created.id).pinnedToTop)
        later()
        assertEquals(SyncOutcome.Success(), phone.syncOk())
    }

    @Test fun `el PC desfija una nota que aqui estaba fijada y el telefono la desfija`() {
        putPc(wire(ID1, "x", T0.plusSeconds(10), pinned = true))
        phone.syncOk()
        assertTrue(phone.notes.get(ID1)!!.pinnedToTop)

        putPc(wire(ID1, "x", T0.plusSeconds(40), pinned = false))
        later()
        phone.syncOk()

        assertFalse(phone.notes.get(ID1)!!.pinnedToTop)
    }

    @Test fun `el PC edita el texto despues de que el telefono fije y gana el PC, sin pin, con el pin perdido guardado en el conflicto`() {
        putPc(wire(ID1, "acordada", T0.plusSeconds(10)))
        later()
        phone.syncOk() // base acordada

        later()
        phone.service.setPinned(ID1, true)
        val pinnedAt = phone.notes.get(ID1)!!.updatedAt
        putPc(wire(ID1, "editada en el PC", pinnedAt.plusSeconds(30)))
        later(120)
        val outcome = phone.syncOk()

        assertEquals(1, outcome.conflicts)
        val kept = phone.notes.get(ID1)!!
        assertEquals("editada en el PC", kept.text)
        assertFalse(kept.pinnedToTop)
        val conflict = phone.store.conflicts.getValue(ID1)
        assertTrue(conflict.losing.note!!.pinnedToTop, "la version perdedora es la fijada")
    }

    @Test fun `el telefono fija despues de que el PC edite el texto y gana el telefono, con su pin y el texto viejo`() {
        putPc(wire(ID1, "acordada", T0.plusSeconds(10)))
        later()
        phone.syncOk()

        putPc(wire(ID1, "editada en el PC", clock.current.plusSeconds(5)))
        later(30)
        phone.service.setPinned(ID1, true)
        later()
        val outcome = phone.syncOk()

        assertEquals(1, outcome.conflicts)
        assertTrue(phone.notes.get(ID1)!!.pinnedToTop)
        assertEquals("acordada", phone.notes.get(ID1)!!.text)
        val sent = remoteNote(ID1)
        assertTrue(sent.pinnedToTop)
        assertEquals("acordada", sent.text)
        assertEquals("editada en el PC", phone.store.conflicts.getValue(ID1).losing.note!!.text)
    }

    @Test fun `si los dos lados fijan la misma nota no hay conflicto`() {
        putPc(wire(ID1, "x", T0.plusSeconds(10)))
        later()
        phone.syncOk()

        phone.service.setPinned(ID1, true)
        putPc(wire(ID1, "x", clock.current.plusSeconds(30), pinned = true))
        later(120)
        val outcome = phone.syncOk()

        assertEquals(0, outcome.conflicts)
        assertTrue(phone.store.conflictedNoteIds().isEmpty())
        assertTrue(phone.notes.get(ID1)!!.pinnedToTop)
    }

    @Test fun `una fijada enviada a la papelera sube en la papelera con su pin y vuelve fijada al restaurarla`() {
        val created = phone.service.create("hola").getOrThrow()
        phone.service.setPinned(created.id, true)
        later()
        phone.syncOk()

        later()
        phone.service.trash(created.id)
        later()
        phone.syncOk()
        val sent = remoteNote(created.id)
        assertEquals(WireState.TRASH, sent.state)
        assertTrue(sent.pinnedToTop)

        later()
        phone.service.restore(created.id)
        assertEquals(NoteState.ACTIVE, phone.notes.get(created.id)!!.state)
        assertTrue(phone.notes.get(created.id)!!.pinnedToTop)
    }

    @Test fun `una fijada archivada en el PC llega archivada y fijada y no sale en la lista de activas`() {
        putPc(wire(ID1, "archivada", T0.plusSeconds(10), pinned = true, state = WireState.ARCHIVED))

        phone.syncOk()

        val note = phone.notes.get(ID1)!!
        assertEquals(NoteState.ARCHIVED, note.state)
        assertTrue(note.pinnedToTop)
        assertTrue(phone.service.list(NoteState.ACTIVE).isEmpty())
        assertEquals(listOf(ID1), phone.service.list(NoteState.ARCHIVED).map { it.id })
    }

    @Test fun `un borrado firmado de una fijada la quita del telefono`() {
        putPc(wire(ID1, "x", T0.plusSeconds(10), pinned = true))
        phone.syncOk()
        assertNotNull(phone.notes.get(ID1))

        remote.store[UUID.fromString(ID1)] = SyncEnvelopeCodec.createTombstone(
            UUID.fromString(ID1), OffsetDateTime.ofInstant(clock.current.plusSeconds(120), ZoneOffset.UTC), TEST_KEY, OTHER_DEVICE,
        )
        later(300)
        phone.syncOk()

        assertNull(phone.notes.get(ID1))
    }

    @Test fun `el pin y la posicion manual del PC conviven, primero las fijadas y cada grupo con su orden`() {
        putPc(wire(ID1, "a", T0.plusSeconds(10), position = 1.0))
        putPc(wire(ID2, "b", T0.plusSeconds(10), position = 2.0, pinned = true))
        putPc(wire(ID3, "c", T0.plusSeconds(10), position = 3.0))

        phone.syncOk()

        assertEquals(listOf(ID2, ID1, ID3), phone.service.list(NoteState.ACTIVE).map { it.id })
        assertEquals(2.0, phone.notes.get(ID2)!!.dockPosition)
    }

    @Test fun `un sobre sin el campo pinnedToTop, como el de una version antigua, deja la nota sin fijar aunque antes lo estuviera`() {
        putPc(wire(ID1, "x", T0.plusSeconds(10), pinned = true))
        phone.syncOk()
        assertTrue(phone.notes.get(ID1)!!.pinnedToTop)

        // pinned = false no escribe la clave: es exactamente el sobre de una versión que no conoce el campo.
        putPc(wire(ID1, "editada por una version antigua", T0.plusSeconds(500)))
        later(1000)
        phone.syncOk()

        assertFalse(phone.notes.get(ID1)!!.pinnedToTop)
        assertEquals("editada por una version antigua", phone.notes.get(ID1)!!.text)
    }

    @Test fun `mover una nota despues de fijar otra mantiene el pin y sube ambas con su posicion`() {
        val a = phone.service.create("a").getOrThrow()
        later()
        val b = phone.service.create("b").getOrThrow()
        later()
        val c = phone.service.create("c").getOrThrow()
        later()
        phone.service.setPinned(c.id, true)
        later()
        // Mueve la «b» delante de la «a», ambas sin fijar.
        phone.service.move(b.id, beforeId = null, afterId = a.id).getOrThrow()
        later()
        phone.syncOk()

        assertTrue(remoteNote(c.id).pinnedToTop)
        assertFalse(remoteNote(a.id).pinnedToTop)
        assertEquals(listOf(c.id, b.id, a.id), phone.service.list(NoteState.ACTIVE).map { it.id })
        assertNotNull(remoteNote(b.id).dockPosition)
    }
}
