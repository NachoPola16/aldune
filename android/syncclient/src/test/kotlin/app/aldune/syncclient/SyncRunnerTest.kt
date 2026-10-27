package app.aldune.syncclient

import app.aldune.core.NoteState as WireState
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.IdFactory
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.sync.InMemorySyncTransport
import app.aldune.sync.NOTHING_READABLE_ERROR
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncTransport
import java.io.IOException
import java.net.UnknownHostException
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertFailsWith
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class SyncRunnerTest {
    private val noteStore = InMemoryNoteStore()
    private val store = InMemoryLocalSyncStore(noteStore)
    private val clock = TestClock(T0.plusSeconds(3600))
    private val service = NoteService(noteStore, clock, DEVICE, IdFactory { UUID.randomUUID().toString() })
    private val remote = InMemorySyncTransport()

    private fun run(transport: SyncTransport = remote, key: ByteArray = TEST_KEY) =
        runSync(store, transport, key, SyncClientSettings(), DEVICE, clock)

    private fun wire(id: String, text: String, updatedAt: Instant, state: Int = WireState.ACTIVE) = SyncNote(
        UUID.fromString(id), text, "#FFDDD4", OffsetDateTime.ofInstant(T0, ZoneOffset.UTC), OffsetDateTime.ofInstant(updatedAt, ZoneOffset.UTC),
        state, "", emptyList(), false, null, null,
    )

    private fun envelopeOf(id: String, text: String, updatedAt: Instant, device: String = OTHER_DEVICE, state: Int = WireState.ACTIVE) =
        SyncEnvelopeCodec.createNote(wire(id, text, updatedAt, state), TEST_KEY, device)

    private fun success(outcome: SyncOutcome) = assertIs<SyncOutcome.Success>(outcome)

    private fun localNote(id: String, text: String = "hola", updatedAt: Instant = T0) =
        noteStore.put(note(id, text, updatedAt = updatedAt))

    /** Un transporte que falla en la operación pedida (la n-ésima escritura, o la lectura). */
    private class FlakyTransport(val inner: SyncTransport, val failOnRead: Throwable? = null, val failOnWriteNumber: Int? = null,
                                 val beforeWrite: (() -> Unit)? = null) : SyncTransport {
        var writes = 0
        override fun readAll(): List<SyncEnvelope> = failOnRead?.let { throw it } ?: inner.readAll()
        override fun write(envelope: SyncEnvelope) {
            beforeWrite?.invoke()
            writes++
            if (writes == failOnWriteNumber) throw UnknownHostException("sin red")
            inner.write(envelope)
        }
        override fun delete(noteId: UUID) = inner.delete(noteId)
    }

    @Test fun `sin nada en ningun lado no hace nada`() {
        assertEquals(SyncOutcome.Success(), run())
        assertEquals(0, noteStore.writeCount)
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `una nota local que el almacen no tiene se sube y su base se fija despues`() {
        localNote(ID1)
        val result = success(run())
        assertEquals(1, result.uploaded)
        assertEquals(setOf(UUID.fromString(ID1)), remote.written)
        val base = store.bases.getValue(ID1)
        assertEquals(DEVICE, base.deviceId)
        assertEquals(T0, base.updatedAt.toInstant())
    }

    @Test fun `una nota del almacen se aplica con su autor y su base`() {
        remote.store[UUID.fromString(ID1)] = envelopeOf(ID1, "del pc", T0.plusSeconds(10))
        val result = success(run())
        assertEquals(1, result.received)
        val applied = noteStore.get(ID1)!!
        assertEquals("del pc", applied.text)
        assertEquals(OTHER_DEVICE, applied.syncAuthorDeviceId)
        assertEquals(OTHER_DEVICE, store.bases.getValue(ID1).deviceId)
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `las bases no se guardan antes de que el almacen remoto tenga las subidas`() {
        localNote(ID1)
        var basesWhenUploading: Int? = null
        val spy = FlakyTransport(remote, beforeWrite = { basesWhenUploading = store.bases.size })
        success(run(spy))
        assertEquals(0, basesWhenUploading)
        assertEquals(1, store.bases.size)
    }

    @Test fun `si la lectura falla no se escribe nada y se clasifica`() {
        localNote(ID1)
        val before = noteStore.writeCount
        val outcome = run(FlakyTransport(remote, failOnRead = UnknownHostException("x")))
        assertEquals(SyncFailureKind.OFFLINE, assertIs<SyncOutcome.Failure>(outcome).failure.kind)
        assertEquals(before, noteStore.writeCount)
        assertTrue(store.bases.isEmpty())
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `un fallo de red en la fase 1 no escribe nada local, ni siquiera lo que ya se habia recibido`() {
        localNote(ID1)
        localNote(ID2)
        remote.store[UUID.fromString(ID3)] = envelopeOf(ID3, "del pc", T0)
        val before = noteStore.writeCount

        val outcome = run(FlakyTransport(remote, failOnWriteNumber = 2))

        assertEquals(SyncFailureKind.OFFLINE, assertIs<SyncOutcome.Failure>(outcome).failure.kind)
        assertEquals(before, noteStore.writeCount, "ni la nota recibida ni las lápidas")
        assertNull(noteStore.get(ID3))
        assertTrue(store.bases.isEmpty())
        assertEquals(1, remote.written.size, "la primera subida sí llegó (es idempotente)")
    }

    @Test fun `tras un fallo en la fase 1 la pasada siguiente converge sin perder nada`() {
        localNote(ID1, "primera")
        localNote(ID2, "segunda")
        run(FlakyTransport(remote, failOnWriteNumber = 2))
        val second = success(run())
        assertEquals(setOf(ID1, ID2), store.bases.keys)
        assertTrue(second.uploaded >= 1)
        assertEquals(setOf(UUID.fromString(ID1), UUID.fromString(ID2)), remote.store.keys)
        assertEquals(SyncOutcome.Success(), run(), "y la tercera ya no tiene nada que hacer")
    }

    @Test fun `si ninguna nota del almacen descifra es clave cambiada y no se escribe nada en ningun lado`() {
        localNote(ID1)
        val foreign = SyncEnvelopeCodec.createNote(wire(ID2, "de otro perfil", T0), ByteArray(32) { 99 }, OTHER_DEVICE)
        remote.store[UUID.fromString(ID2)] = foreign
        val before = noteStore.writeCount

        val failure = assertIs<SyncOutcome.Failure>(run()).failure

        assertEquals(SyncFailureKind.KEY_CHANGED, failure.kind)
        assertEquals(NOTHING_READABLE_ERROR, failure.detail)
        assertEquals(before, noteStore.writeCount)
        assertTrue(remote.written.isEmpty())
        assertTrue(store.bases.isEmpty())
    }

    @Test fun `un estado desconocido se omite, no se fija su base y el resto sigue`() {
        remote.store[UUID.fromString(ID1)] = envelopeOf(ID1, "de un formato futuro", T0, state = 7)
        remote.store[UUID.fromString(ID2)] = envelopeOf(ID2, "normal", T0)

        val result = success(run())

        assertEquals(1, result.omitted)
        assertEquals(1, result.received)
        assertNull(noteStore.get(ID1))
        assertEquals("normal", noteStore.get(ID2)!!.text)
        assertEquals(setOf(ID2), store.bases.keys)
    }

    @Test fun `una edicion durante la fase 1 no se pisa, no fija la base y se cuenta`() {
        localNote(ID1, "original")
        remote.store[UUID.fromString(ID2)] = envelopeOf(ID2, "del pc", T0)
        // Para que la nota 1 tenga acción local hace falta una versión remota más nueva.
        remote.store[UUID.fromString(ID1)] = envelopeOf(ID1, "del pc sobre la 1", T0.plusSeconds(100))
        localNote(ID3, "para subir")
        val editing = FlakyTransport(remote, beforeWrite = { service.edit(ID1, "escrito mientras se sincronizaba") })

        val result = success(run(editing))

        assertEquals(1, result.skippedConcurrent)
        assertEquals("escrito mientras se sincronizaba", noteStore.get(ID1)!!.text)
        assertNull(store.bases[ID1])
        assertEquals("del pc", noteStore.get(ID2)!!.text, "las demás sí se aplican")
        assertTrue(ID3 in store.bases)
    }

    @Test fun `un borrado sin firma se ignora, la nota sigue activa y el almacen vuelve a tenerla`() {
        localNote(ID1, "importante")
        val unsigned = SyncEnvelopeCodec.createTombstone(UUID.fromString(ID1), OffsetDateTime.ofInstant(T0.plusSeconds(60), ZoneOffset.UTC),
            ByteArray(32) { 7 }, OTHER_DEVICE)
        remote.store[UUID.fromString(ID1)] = unsigned
        remote.store[UUID.fromString(ID2)] = envelopeOf(ID2, "otra", T0) // que algo descifre

        val result = success(run())

        assertEquals(0, result.trashed)
        val kept = noteStore.get(ID1)!!
        assertEquals(NoteState.ACTIVE, kept.state)
        assertEquals("importante", kept.text)
        assertFalse(remote.store.getValue(UUID.fromString(ID1)).tombstone)
    }

    @Test fun `un borrado firmado borra la nota y deja la lapida con el autor remoto`() {
        localNote(ID1, "vieja", updatedAt = T0)
        remote.store[UUID.fromString(ID1)] = SyncEnvelopeCodec.createTombstone(
            UUID.fromString(ID1), OffsetDateTime.ofInstant(T0.plusSeconds(60), ZoneOffset.UTC), TEST_KEY, OTHER_DEVICE,
        )

        val result = success(run())

        assertEquals(1, result.tombstonesApplied)
        assertNull(noteStore.get(ID1))
        assertEquals(listOf(OTHER_DEVICE), noteStore.tombstones().map { it.deviceId })
    }

    @Test fun `dos versiones cambiadas guardan un conflicto y gana la mayor`() {
        localNote(ID1, "base")
        // Acuerdo previo en la versión T0, y luego cada lado cambia.
        store.commit(LocalCommit(emptyList(), mapOf(ID1 to SyncBase(OffsetDateTime.ofInstant(T0, ZoneOffset.UTC), DEVICE)), emptyMap()))
        noteStore.put(note(ID1, "edicion local", updatedAt = T0.plusSeconds(10)))
        remote.store[UUID.fromString(ID1)] = envelopeOf(ID1, "edicion del pc", T0.plusSeconds(20))

        val result = success(run())

        assertEquals(1, result.conflicts)
        assertEquals(setOf(ID1), store.conflictedNoteIds())
        assertEquals("edicion del pc", noteStore.get(ID1)!!.text)
        assertEquals("edicion local", store.conflicts.getValue(ID1).losing.note!!.text)
    }

    @Test fun `un id que no es UUID es un fallo de programa y lanza`() {
        noteStore.put(note("n1"))
        assertFailsWith<IllegalArgumentException> { run() }
    }

    @Test fun `un fallo del almacen local en el commit se devuelve como fallo`() {
        remote.store[UUID.fromString(ID1)] = envelopeOf(ID1, "del pc", T0)
        val broken = object : LocalSyncStore by store {
            override fun commit(commit: LocalCommit): CommitReport = throw IOException("disco lleno")
        }
        val outcome = runSync(broken, remote, TEST_KEY, SyncClientSettings(), DEVICE, clock)
        assertEquals(SyncFailureKind.UNKNOWN, assertIs<SyncOutcome.Failure>(outcome).failure.kind)
    }
}
