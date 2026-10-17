package app.aldune.sync

import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncFormatException
import app.aldune.core.SyncNote
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import okhttp3.mockwebserver.SocketPolicy
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.BeforeEach
import org.junit.jupiter.api.Test
import java.io.IOException
import java.time.Duration
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID

/**
 * El transporte HTTP contra un MockWebServer que imita las rutas y los códigos de `Aldune.SyncServer`
 * (`GET/PUT/DELETE api/v1/objects[/{id:N}]`, 401 sin token, 413 pasado el máximo). Falta la prueba contra
 * el servidor real, que es la puerta de F2.
 */
class HttpSyncTransportTest {
    private val key = ByteArray(32) { it.toByte() }
    private val id = UUID.fromString("00000001-0000-4000-8000-000000000000")
    private lateinit var server: MockWebServer

    @BeforeEach
    fun start() {
        server = MockWebServer().also { it.start() }
    }

    @AfterEach
    fun stop() = server.shutdown()

    private fun url(path: String = "/") = server.url(path).toString()

    private fun note(text: String, minute: Int, noteId: UUID = id) = SyncNote(
        id = noteId, text = text, color = "#EBD38B",
        createdAt = OffsetDateTime.of(2024, 5, 1, 9, 0, 0, 0, ZoneOffset.UTC),
        updatedAt = OffsetDateTime.of(2024, 5, 1, 10, minute, 0, 0, ZoneOffset.UTC),
        state = 0, screenOrigin = "primary", tags = emptyList(), isProtected = false,
        protectedContent = null, dockPosition = null,
    )

    private fun envelope(text: String, minute: Int, noteId: UUID = id) =
        SyncEnvelopeCodec.createNote(note(text, minute, noteId), key, "device-b")

    @Test
    fun `lee la lista y cada objeto con el token y los ids sin guiones`() {
        val a = envelope("uno", 10)
        val b = envelope("dos", 20, UUID(0, 2))
        val bodies = mapOf(
            "/api/v1/objects" to """[{"noteId":"${a.noteId}","tombstone":false},{"noteId":"${b.noteId}","tombstone":false}]""",
            "/api/v1/objects/00000001000040008000000000000000" to SyncEnvelopeCodec.serialize(a),
            "/api/v1/objects/00000000000000000000000000000002" to SyncEnvelopeCodec.serialize(b),
        )
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) = bodies[request.path]?.let { MockResponse().setBody(it) }
                ?: MockResponse().setResponseCode(404)
        }

        val read = HttpSyncTransport(url(), "secreto").readAll()
        assertEquals(listOf(a.noteId, b.noteId), read.map { it.noteId })
        assertEquals("uno", SyncEnvelopeCodec.decryptNote(read[0], key).text)

        repeat(3) { assertEquals("Bearer secreto", server.takeRequest().getHeader("Authorization")) }
    }

    @Test
    fun `un almacen vacio devuelve una lista vacia`() {
        server.enqueue(MockResponse().setBody("[]"))
        assertEquals(emptyList<SyncEnvelope>(), HttpSyncTransport(url(), "t").readAll())
    }

    @Test
    fun `write hace PUT del sobre serializado y delete hace DELETE`() {
        server.enqueue(MockResponse().setResponseCode(204))
        server.enqueue(MockResponse().setResponseCode(204))
        val transport = HttpSyncTransport(url(), "t")
        val e = envelope("subida", 10)
        transport.write(e)
        transport.delete(id)

        val put = server.takeRequest()
        assertEquals("PUT", put.method)
        assertEquals("/api/v1/objects/00000001000040008000000000000000", put.path)
        assertEquals("application/json; charset=utf-8", put.getHeader("Content-Type")?.lowercase())
        assertEquals(SyncEnvelopeCodec.serialize(e), put.body.readUtf8())
        val del = server.takeRequest()
        assertEquals("DELETE", del.method)
        assertEquals("/api/v1/objects/00000001000040008000000000000000", del.path)
    }

    @Test
    fun `un 401 dice que el servidor rechazo el token`() {
        server.enqueue(MockResponse().setResponseCode(401))
        val error = assertThrows(IllegalStateException::class.java) { HttpSyncTransport(url(), "malo").readAll() }
        assertTrue(error.message!!.contains("rejected the token (401)"), error.message)
    }

    @Test
    fun `404, 413 y 5xx son IOException con el codigo`() {
        for (code in listOf(404, 413, 500, 503)) {
            server.enqueue(MockResponse().setResponseCode(code))
            val error = assertThrows(IOException::class.java) { HttpSyncTransport(url(), "t").write(envelope("x", 10)) }
            assertTrue(error.message!!.contains(code.toString()), "código $code: ${error.message}")
        }
    }

    @Test
    fun `una respuesta que no es JSON es un fallo normal y no un cierre`() {
        server.enqueue(MockResponse().setBody("<html>portal cautivo</html>"))
        assertThrows(IOException::class.java) { HttpSyncTransport(url(), "t").readAll() }
    }

    @Test
    fun `un objeto de formato 5 aborta la lectura entera`() {
        val future = SyncEnvelopeCodec.serialize(envelope("futura", 10)).replace("\"format\":4", "\"format\":5")
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) =
                if (request.path == "/api/v1/objects") MockResponse().setBody("""[{"noteId":"$id"}]""") else MockResponse().setBody(future)
        }
        val error = assertThrows(SyncFormatException::class.java) { HttpSyncTransport(url(), "t").readAll() }
        assertTrue(error.message!!.contains("newer Aldune version"), error.message)
    }

    @Test
    fun `sin respuesta a tiempo falla con un IOException`() {
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.NO_RESPONSE))
        assertThrows(IOException::class.java) { HttpSyncTransport(url(), "t", Duration.ofMillis(300)).readAll() }
    }

    @Test
    fun `una URL con ruta acabada en barra conserva la ruta`() {
        server.enqueue(MockResponse().setBody("[]"))
        HttpSyncTransport(url("/aldune/"), "t").readAll()
        assertEquals("/aldune/api/v1/objects", server.takeRequest().path)
    }

    @Test
    fun `una URL o un token invalidos se rechazan al crear el transporte`() {
        assertThrows(IllegalArgumentException::class.java) { HttpSyncTransport("no es una url", "t") }
        assertThrows(IllegalArgumentException::class.java) { HttpSyncTransport("ftp://host/", "t") }
        assertThrows(IllegalArgumentException::class.java) { HttpSyncTransport(url(), "  ") }
    }

    @Test
    fun `dos dispositivos se sincronizan a traves del servidor`() {
        // Un servidor mínimo con la regla de Aldune.SyncServer: un PUT más antiguo que lo guardado no lo pisa (204 igualmente).
        val store = linkedMapOf<String, String>()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse {
                val path = request.path!!
                return when {
                    request.method == "GET" && path == "/api/v1/objects" ->
                        MockResponse().setBody(store.values.joinToString(prefix = "[", postfix = "]") { """{"noteId":"${SyncEnvelopeCodec.deserialize(it).noteId}"}""" })
                    request.method == "GET" -> store[path.substringAfterLast('/')]?.let { MockResponse().setBody(it) } ?: MockResponse().setResponseCode(404)
                    request.method == "PUT" -> {
                        val incoming = SyncEnvelopeCodec.deserialize(request.body.readUtf8())
                        val name = path.substringAfterLast('/')
                        val current = store[name]?.let { SyncEnvelopeCodec.deserialize(it) }
                        if (current == null || incoming.updatedAt.toInstant() >= current.updatedAt.toInstant()) store[name] = SyncEnvelopeCodec.serialize(incoming)
                        MockResponse().setResponseCode(204)
                    }
                    else -> MockResponse().setResponseCode(404)
                }
            }
        }
        val now = OffsetDateTime.of(2026, 10, 17, 12, 0, 0, 0, ZoneOffset.UTC)
        fun pass(device: String, local: InMemoryLocalStore) = execute(
            planSync(SyncInput(device, SyncScope.All, local.notes.values.toList(), local.tombstones.values.toList(), local.bases, emptySet(),
                HttpSyncTransport(url(), "t").readAll(), now), key),
            local, HttpSyncTransport(url(), "t"),
        )

        val a = InMemoryLocalStore(listOf(LocalNote(note("desde A", 10), null)))
        val b = InMemoryLocalStore()
        assertEquals(1, pass("device-a", a).uploaded)
        assertEquals(1, pass("device-b", b).downloaded)
        assertEquals("desde A", b.notes.getValue(id).note.text)
        assertEquals(SyncResult(0, 0, 0, 0), pass("device-b", b))
        assertEquals(SyncResult(0, 0, 0, 0), pass("device-a", a))
    }
}
