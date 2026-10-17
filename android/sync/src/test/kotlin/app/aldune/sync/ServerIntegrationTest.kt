package app.aldune.sync

import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Assumptions.assumeTrue
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/**
 * HttpSyncTransport + planSync + execute contra un `Aldune.SyncServer` **real**, arrancado a mano por quien lo lanza
 * (nunca contra datos ni servidores reales de nadie). Se salta entero si falta `aldune.server.url`:
 *
 *     ALDUNE_SYNC_TOKEN=token-de-prueba-largo ALDUNE_DATA_DIR=$(mktemp -d) \
 *       ASPNETCORE_URLS=http://127.0.0.1:5099 dotnet run --project src/Aldune.SyncServer
 *     cd android/sync && gradle test --tests '*ServerIntegration*' \
 *       -Daldune.server.url=http://127.0.0.1:5099/ -Daldune.server.token=token-de-prueba-largo
 *
 * Cada test usa ids nuevos y los borra al terminar, para no depender del estado del servidor.
 */
class ServerIntegrationTest {
    private val serverUrl = System.getProperty("aldune.server.url")
    private val token = System.getProperty("aldune.server.token") ?: "token-de-prueba"
    private val key = ByteArray(32) { (it * 3).toByte() }
    private val created = mutableListOf<UUID>()

    @BeforeEach
    fun requireServer() = assumeTrue(!serverUrl.isNullOrBlank(), "sin aldune.server.url: no hay servidor real contra el que probar")

    @AfterEach
    fun cleanUp() {
        if (serverUrl.isNullOrBlank()) return
        val transport = HttpSyncTransport(serverUrl, token)
        created.forEach { runCatching { transport.delete(it) } }
    }

    private fun transport() = HttpSyncTransport(serverUrl, token)

    private fun newId() = UUID.randomUUID().also { created += it }

    private fun note(noteId: UUID, text: String, minute: Int) = SyncNote(
        id = noteId, text = text, color = "#EBD38B",
        createdAt = OffsetDateTime.of(2024, 5, 1, 9, 0, 0, 0, ZoneOffset.UTC),
        updatedAt = OffsetDateTime.of(2024, 5, 1, 10, minute, 0, 0, ZoneOffset.UTC),
        state = 0, screenOrigin = "primary", tags = emptyList(), isProtected = false,
        protectedContent = null, dockPosition = null,
    )

    private val now: OffsetDateTime get() = OffsetDateTime.now(ZoneOffset.UTC)

    /** Una pasada de sync completa de un «dispositivo»: leer, planificar, ejecutar. */
    private fun pass(device: String, local: InMemoryLocalStore): SyncResult {
        val transport = transport()
        val input = SyncInput(device, SyncScope.All, local.notes.values.toList(), local.tombstones.values.toList(), local.bases, emptySet(), transport.readAll(), now)
        return execute(planSync(input, key), local, transport)
    }

    @Test
    fun `subida y bajada entre dos dispositivos`() {
        val id = newId()
        val a = InMemoryLocalStore(listOf(LocalNote(note(id, "desde A", 10))))
        val b = InMemoryLocalStore()

        assertEquals(1, pass("device-a", a).uploaded)
        assertEquals(1, pass("device-b", b).downloaded)
        assertEquals("desde A", b.notes.getValue(id).note.text)
        // Estable: ninguno de los dos tiene nada más que hacer.
        assertEquals(0, pass("device-b", b).uploaded + pass("device-b", b).downloaded)
        assertEquals(0, pass("device-a", a).uploaded + pass("device-a", a).downloaded)
    }

    @Test
    fun `un borrado firmado llega a otro dispositivo`() {
        val id = newId()
        val a = InMemoryLocalStore(listOf(LocalNote(note(id, "para borrar", 10))))
        val b = InMemoryLocalStore()
        pass("device-a", a)
        pass("device-b", b)
        assertTrue(b.notes.containsKey(id))

        val deletedAt = OffsetDateTime.of(2024, 5, 1, 11, 0, 0, 0, ZoneOffset.UTC)
        a.notes.remove(id)
        a.tombstones[id] = LocalTombstone(id, deletedAt, "device-a")
        assertEquals(1, pass("device-a", a).uploaded)

        val remote = transport().readAll().single { it.noteId == id }
        assertTrue(remote.tombstone)
        assertTrue(SyncEnvelopeCodec.isAuthenticTombstone(remote, key), "el borrado subido va firmado")

        val result = pass("device-b", b)
        assertEquals(1, result.tombstonesApplied)
        assertFalse(b.notes.containsKey(id))
    }

    @Test
    fun `un PUT de una version mas antigua no pisa la nueva`() {
        val id = newId()
        val newer = SyncEnvelopeCodec.createNote(note(id, "nueva", 30), key, "device-a")
        val older = SyncEnvelopeCodec.createNote(note(id, "vieja", 10), key, "device-b")
        val transport = transport()

        transport.write(newer)
        transport.write(older) // el servidor contesta 204 y se queda con la nueva
        val stored = transport.readAll().single { it.noteId == id }
        assertEquals("nueva", SyncEnvelopeCodec.decryptNote(stored, key).text)
        assertEquals(newer.updatedAt.toInstant(), stored.updatedAt.toInstant())

        // Y la inversa: una versión más nueva sí sustituye.
        transport.write(SyncEnvelopeCodec.createNote(note(id, "nuevisima", 50), key, "device-a"))
        assertEquals("nuevisima", SyncEnvelopeCodec.decryptNote(transport.readAll().single { it.noteId == id }, key).text)
    }

    @Test
    fun `delete quita el objeto y es idempotente`() {
        val id = newId()
        val transport = transport()
        transport.write(SyncEnvelopeCodec.createNote(note(id, "x", 10), key, "device-a"))
        assertTrue(transport.readAll().any { it.noteId == id })
        transport.delete(id)
        transport.delete(id) // el servidor contesta 204 aunque ya no esté
        assertFalse(transport.readAll().any { it.noteId == id })
    }

    @Test
    fun `un token malo da 401 en cualquier operacion`() {
        val bad = HttpSyncTransport(serverUrl, "este-token-no-existe")
        val envelope = SyncEnvelopeCodec.createNote(note(newId(), "x", 10), key, "device-a")
        for (operation in listOf<() -> Unit>({ bad.readAll() }, { bad.write(envelope) }, { bad.delete(envelope.noteId) })) {
            val error = assertThrows(IllegalStateException::class.java) { operation() }
            assertTrue(error.message!!.contains("401"), error.message)
        }
    }
}
