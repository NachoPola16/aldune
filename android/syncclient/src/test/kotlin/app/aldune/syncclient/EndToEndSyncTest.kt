package app.aldune.syncclient

import app.aldune.core.NoteState as WireState
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import app.aldune.domain.IdFactory
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.LineEnding
import app.aldune.domain.NoteService
import app.aldune.domain.NoteState
import app.aldune.sync.InMemorySyncTransport
import app.aldune.sync.SyncTransport
import java.net.UnknownHostException
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue

/**
 * La spec F5 §6 punto 2: `NoteService` + `InMemoryLocalSyncStore` + el transporte en memoria de `android/sync`, de principio a fin.
 * Cada «teléfono» es una instalación entera (almacén, servicio, reloj y deviceId); el «PC» escribe sobres directamente en el almacén.
 */
class EndToEndSyncTest {
    private val remote = InMemorySyncTransport()
    private val clock = TestClock(Instant.parse("2026-03-01T12:00:00Z"))

    private inner class Phone(val deviceId: String) {
        val notes = InMemoryNoteStore()
        val store = InMemoryLocalSyncStore(notes)
        val service = NoteService(notes, clock, deviceId, IdFactory { UUID.randomUUID().toString() })

        fun sync(transport: SyncTransport = remote): SyncOutcome {
            remote.written.clear()
            return runSync(store, transport, TEST_KEY, SyncClientSettings(), deviceId, clock)
        }

        /** La pasada tiene que salir bien. */
        fun syncOk(transport: SyncTransport = remote): SyncOutcome.Success = assertIs<SyncOutcome.Success>(sync(transport))
    }

    private val phone = Phone(DEVICE)

    private fun wire(
        id: String, text: String, updatedAt: Instant, state: Int = WireState.ACTIVE, color: String = "#FFDDD4", tags: List<String> = emptyList(),
        position: Double? = null,
    ) = SyncNote(
        UUID.fromString(id), text, color, OffsetDateTime.ofInstant(T0, ZoneOffset.UTC), OffsetDateTime.ofInstant(updatedAt, ZoneOffset.UTC),
        state, "", tags, false, null, position,
    )

    /** Un sobre como el que escribiría el PC (otro dispositivo, misma clave). */
    private fun fromPc(id: String, text: String, updatedAt: Instant, state: Int = WireState.ACTIVE, color: String = "#FFDDD4", tags: List<String> = emptyList(), position: Double? = null) =
        SyncEnvelopeCodec.createNote(wire(id, text, updatedAt, state, color, tags, position), TEST_KEY, OTHER_DEVICE)

    private fun putPc(envelope: SyncEnvelope) {
        remote.store[envelope.noteId] = envelope
    }

    private fun remoteNote(id: String): SyncNote = SyncEnvelopeCodec.decryptNote(remote.store.getValue(UUID.fromString(id)), TEST_KEY)

    private fun later(seconds: Long = 60) = clock.advanceSeconds(seconds)

    @Test fun `una nota creada aqui sube y la segunda pasada no hace nada`() {
        val created = phone.service.create("hola desde Android").getOrThrow()
        later()

        val first = phone.syncOk()
        assertEquals(1, first.uploaded)
        assertEquals("hola desde Android", remoteNote(created.id).text)
        assertEquals(DEVICE, remote.store.getValue(UUID.fromString(created.id)).deviceId)

        val second = phone.syncOk()
        assertEquals(SyncOutcome.Success(), second)
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `una nota del PC se aplica con su autor y no se resube en la pasada siguiente`() {
        putPc(fromPc(ID1, "escrita en el PC", T0.plusSeconds(10)))

        val first = phone.syncOk()
        assertEquals(1, first.received)
        assertEquals(0, first.uploaded)
        assertEquals(OTHER_DEVICE, phone.notes.get(ID1)!!.syncAuthorDeviceId)
        assertTrue(remote.written.isEmpty())

        later()
        assertEquals(SyncOutcome.Success(), phone.syncOk())
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `abrir y cerrar una nota sin editar no genera subida aunque venga con formas raras`() {
        // E28: color en minúsculas, etiquetas sin normalizar, posición y orden tal cual; nada de eso se reescribe al tocar sin cambiar.
        putPc(fromPc(ID1, "texto\n", T0.plusSeconds(10), color = "#aabbcc", tags = listOf(" Trabajo ", "trabajo", "Z"), position = 3.5))
        phone.syncOk()
        val before = phone.notes.get(ID1)
        val writes = phone.notes.writeCount

        phone.service.edit(ID1, "texto\n")
        phone.service.setColor(ID1, "#aabbcc")
        later()

        assertEquals(writes, phone.notes.writeCount)
        assertEquals(before, phone.notes.get(ID1))
        assertEquals(SyncOutcome.Success(), phone.syncOk())
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `una nota con CRLF vuelve con CRLF`() {
        putPc(fromPc(ID1, "uno\r\ndos\r\n", T0.plusSeconds(10)))
        phone.syncOk()
        val local = phone.notes.get(ID1)!!
        assertEquals("uno\ndos\n", local.text)
        assertEquals(LineEnding.CRLF, local.lineEnding)

        later()
        assertEquals(SyncOutcome.Success(), phone.syncOk(), "recibirla no la resube")

        phone.service.edit(ID1, "uno\ndos\ntres\n")
        later()
        assertEquals(1, phone.syncOk().uploaded)
        assertEquals("uno\r\ndos\r\ntres\r\n", remoteNote(ID1).text)
    }

    @Test fun `una nota creada en Android sube con LF`() {
        val created = phone.service.create("a\nb").getOrThrow()
        phone.syncOk()
        assertEquals("a\nb", remoteNote(created.id).text)
    }

    @Test fun `una protegida sube con texto vacio aunque el Note traiga texto`() {
        val json = """{"version":1,"salt":"c2FsdA==","cipherText":"Y2lwaGVy","nonce":"bm9uY2U=","tag":"dGFn"}"""
        phone.notes.put(note(ID1, "esto no debe salir en claro", isProtected = true, protectedJson = json))

        assertEquals(1, phone.syncOk().uploaded)

        val sent = remoteNote(ID1)
        assertEquals("", sent.text)
        assertTrue(sent.isProtected)
        assertEquals("Y2lwaGVy", sent.protectedContent!!.cipherText)
    }

    @Test fun `una protegida del PC se guarda entera y vuelve igual tras cambiarle el color`() {
        val content = app.aldune.core.ProtectedNoteContent(1, "c2FsdA==", "Y2lwaGVy", "bm9uY2U=", "dGFn")
        putPc(SyncEnvelopeCodec.createNote(wire(ID1, "", T0.plusSeconds(10)).copy(isProtected = true, protectedContent = content), TEST_KEY, OTHER_DEVICE))
        phone.syncOk()
        assertTrue(phone.notes.get(ID1)!!.isProtected)

        later()
        phone.service.setColor(ID1, "#112233")
        later()
        phone.syncOk()

        val sent = remoteNote(ID1)
        assertEquals("#112233", sent.color)
        assertEquals(content, sent.protectedContent)
        assertEquals("", sent.text)
    }

    @Test fun `gana la version mayor cuando el almacen y el telefono tienen la misma nota`() {
        // El PC es más nuevo: gana el PC y no hay subida.
        phone.notes.put(note(ID1, "local viejo", updatedAt = T0.plusSeconds(10)))
        putPc(fromPc(ID1, "pc nuevo", T0.plusSeconds(20)))
        phone.syncOk()
        assertEquals("pc nuevo", phone.notes.get(ID1)!!.text)
        assertTrue(remote.written.isEmpty())

        // El teléfono es más nuevo: gana el teléfono y sube.
        phone.notes.put(note(ID2, "local nuevo", updatedAt = T0.plusSeconds(30)))
        putPc(fromPc(ID2, "pc viejo", T0.plusSeconds(15)))
        phone.syncOk()
        assertEquals("local nuevo", remoteNote(ID2).text)
    }

    @Test fun `con dos versiones del mismo id en el almacen se aplica la mayor, se pasen en el orden que se pasen`() {
        val old = fromPc(ID1, "vieja", T0.plusSeconds(10))
        val new = fromPc(ID1, "nueva", T0.plusSeconds(20))
        for (order in listOf(listOf(old, new), listOf(new, old))) {
            val fresh = Phone("0000000000000000000000000000000${order.indexOf(old)}")
            val duplicated = object : SyncTransport {
                override fun readAll() = order
                override fun write(envelope: SyncEnvelope) = Unit
                override fun delete(noteId: UUID) = Unit
            }
            fresh.syncOk(duplicated)
            assertEquals("nueva", fresh.notes.get(ID1)!!.text)
        }
    }

    @Test fun `un borrado sin firma manda a la papelera y nunca borra`() {
        phone.notes.put(note(ID1, "importante", updatedAt = T0))
        putPc(fromPc(ID2, "para que haya algo legible", T0))
        // Un borrado firmado con OTRA clave: sin firma válida.
        putPcUnsignedDelete(ID1, T0.plusSeconds(5))
        later()

        val result = phone.syncOk()

        assertEquals(1, result.trashed)
        val kept = assertNotNull(phone.notes.get(ID1), "una nota nunca se borra por un borrado sin firma")
        assertEquals(NoteState.TRASHED, kept.state)
        assertEquals("importante", kept.text)
        assertTrue(phone.notes.tombstones().none { it.noteId == ID1 })
        // Y lo que sube es la nota en la papelera, no un borrado.
        assertEquals(WireState.TRASH, remoteNote(ID1).state)
    }

    private fun putPcUnsignedDelete(id: String, at: Instant) {
        remote.store[UUID.fromString(id)] = SyncEnvelopeCodec.createTombstone(
            UUID.fromString(id), OffsetDateTime.ofInstant(at, ZoneOffset.UTC), ByteArray(32) { 42 }, OTHER_DEVICE,
        )
    }

    @Test fun `un borrado firmado borra la nota en el otro telefono`() {
        val other = Phone("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")
        val created = phone.service.create("efimera").getOrThrow()
        later()
        phone.syncOk()
        other.syncOk()
        assertEquals("efimera", other.notes.get(created.id)!!.text)

        later()
        phone.service.trash(created.id)
        phone.service.deleteForever(created.id)
        later()
        phone.syncOk()
        assertTrue(remote.store.getValue(UUID.fromString(created.id)).tombstone)

        later()
        val received = other.syncOk()
        assertEquals(1, received.tombstonesApplied)
        assertNull(other.notes.get(created.id))
        // La lápida recibida lleva el autor del borrado: el otro teléfono no la vuelve a firmar como propia.
        assertEquals(DEVICE, other.notes.tombstones().single().deviceId)
        later()
        assertEquals(SyncOutcome.Success(), other.syncOk())
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `una edicion durante la fase 1 no se pisa y la pasada siguiente guarda el conflicto`() {
        putPc(fromPc(ID1, "version acordada", T0.plusSeconds(10)))
        later()
        phone.syncOk() // acuerdo: base = la versión del PC

        // El PC cambia la nota, y el teléfono tiene otra nota nueva que subir (eso es lo que abre la ventana de la fase 1).
        putPc(fromPc(ID1, "el PC la cambia", clock.current.plusSeconds(30)))
        later(120)
        phone.service.create("otra nota para subir").getOrThrow()
        val editing = object : SyncTransport by remote {
            override fun write(envelope: SyncEnvelope) {
                remote.write(envelope)
                later(10)
                phone.service.edit(ID1, "lo que escribi en el editor mientras sincronizaba")
            }
        }

        val during = assertIs<SyncOutcome.Success>(phone.sync(editing))

        assertEquals(1, during.skippedConcurrent)
        assertEquals("lo que escribi en el editor mientras sincronizaba", phone.notes.get(ID1)!!.text, "la edición no se pisa")
        assertTrue(phone.store.conflictedNoteIds().isEmpty())

        later()
        val next = phone.syncOk()
        assertEquals(1, next.conflicts, "con la base vieja, los dos lados cambiados son un conflicto real")
        assertEquals(setOf(ID1), phone.store.conflictedNoteIds())
        // Gana la versión más reciente (la edición local) y la perdedora (la del PC) queda guardada entera.
        assertEquals("lo que escribi en el editor mientras sincronizaba", phone.notes.get(ID1)!!.text)
        assertEquals("el PC la cambia", phone.store.conflicts.getValue(ID1).losing.note!!.text)
        assertEquals("lo que escribi en el editor mientras sincronizaba", remoteNote(ID1).text)
    }

    @Test fun `un fallo de red en la fase 1 no escribe nada local`() {
        putPc(fromPc(ID3, "del pc", T0.plusSeconds(10)))
        phone.service.create("uno").getOrThrow()
        phone.service.create("dos").getOrThrow()
        val writes = phone.notes.writeCount
        val failing = object : SyncTransport by remote {
            var calls = 0
            override fun write(envelope: SyncEnvelope) {
                if (++calls == 2) throw UnknownHostException("sin red")
                remote.write(envelope)
            }
        }

        val outcome = phone.sync(failing)

        assertEquals(SyncFailureKind.OFFLINE, assertIs<SyncOutcome.Failure>(outcome).failure.kind)
        assertEquals(writes, phone.notes.writeCount)
        assertNull(phone.notes.get(ID3))
        assertTrue(phone.store.snapshot().bases.isEmpty())

        later()
        phone.syncOk()
        assertEquals("del pc", phone.notes.get(ID3)!!.text)
        assertEquals(SyncOutcome.Success(), phone.syncOk(), "converge")
    }

    @Test fun `dos pasadas seguidas sin cambios la segunda no hace nada`() {
        phone.service.create("a").getOrThrow()
        putPc(fromPc(ID1, "b", T0.plusSeconds(5)))
        phone.syncOk()
        later()
        val writes = phone.notes.writeCount

        assertEquals(SyncOutcome.Success(), phone.syncOk())
        assertEquals(SyncOutcome.Success(), phone.syncOk())
        assertEquals(writes, phone.notes.writeCount)
        assertTrue(remote.written.isEmpty())
    }

    @Test fun `dos telefonos convergen y en reposo nadie sube`() {
        val other = Phone("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")
        val created = phone.service.create("de A").getOrThrow()
        later(); phone.syncOk()
        later(); other.syncOk()

        later(); other.service.edit(created.id, "de A, editada en B")
        later(); other.syncOk()
        later(); phone.syncOk()
        assertEquals("de A, editada en B", phone.notes.get(created.id)!!.text)

        for (round in 1..2) {
            later(); assertEquals(SyncOutcome.Success(), phone.syncOk())
            later(); assertEquals(SyncOutcome.Success(), other.syncOk())
        }
        assertTrue(phone.store.conflictedNoteIds().isEmpty() && other.store.conflictedNoteIds().isEmpty())
    }
}
