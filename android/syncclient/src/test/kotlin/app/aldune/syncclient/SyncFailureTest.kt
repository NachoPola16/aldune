package app.aldune.syncclient

import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncFormatException
import app.aldune.core.SyncNote
import app.aldune.sync.HttpSyncTransport
import app.aldune.sync.NOTHING_READABLE_ERROR
import app.aldune.sync.SyncTransport
import app.aldune.sync.WebDavSyncTransport
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
import okhttp3.mockwebserver.SocketPolicy
import org.junit.jupiter.api.AfterEach
import org.junit.jupiter.api.BeforeEach
import java.io.IOException
import java.net.ConnectException
import java.net.SocketTimeoutException
import java.net.UnknownHostException
import java.time.Duration
import java.time.OffsetDateTime
import java.time.ZoneOffset
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNotNull
import kotlin.test.assertNull
import kotlin.test.assertTrue

/** Los transportes reales de `android/sync` contra un MockWebServer: lo que se clasifica es lo que de verdad lanzan, no un mensaje de prueba. */
class SyncFailureTest {
    private lateinit var server: MockWebServer
    private val key = ByteArray(32) { it.toByte() }
    private val noteId = UUID.fromString(ID1)

    @BeforeEach fun start() {
        server = MockWebServer().also { it.start() }
    }

    @AfterEach fun stop() = server.shutdown()

    private fun http(timeout: Duration = Duration.ofSeconds(5)) = HttpSyncTransport(server.url("/").toString(), "token", timeout)

    private fun webDav(timeout: Duration = Duration.ofSeconds(5)) = WebDavSyncTransport(server.url("/dav/").toString(), "ana", "clave", timeout)

    private fun envelope() = SyncEnvelopeCodec.createNote(
        SyncNote(noteId, "x", "#FFF", OffsetDateTime.of(2026, 1, 1, 0, 0, 0, 0, ZoneOffset.UTC), OffsetDateTime.of(2026, 1, 1, 0, 0, 0, 0, ZoneOffset.UTC),
            0, "", emptyList(), false, null, null),
        key, DEVICE,
    )

    private fun respond(code: Int, body: String = "") {
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) = MockResponse().setResponseCode(code).setBody(body)
        }
    }

    private fun failureOf(block: () -> Unit): SyncFailure {
        val error = try {
            block()
            null
        } catch (e: Exception) {
            e
        }
        return classifyFailure(assertNotNull(error, "se esperaba un fallo del transporte"))
    }

    private fun readFailure(transport: SyncTransport) = failureOf { transport.readAll() }

    // --- servidor de Aldune ---

    @Test fun `401 del servidor es no autorizado`() {
        respond(401)
        val failure = readFailure(http())
        assertEquals(SyncFailureKind.UNAUTHORIZED, failure.kind)
        assertEquals(401, failure.httpStatus)
    }

    @Test fun `413 al subir es demasiado grande`() {
        respond(413)
        val failure = failureOf { http().write(envelope()) }
        assertEquals(SyncFailureKind.TOO_LARGE, failure.kind)
        assertEquals(413, failure.httpStatus)
    }

    @Test fun `5xx y 404 de la ruta base son error del servidor`() {
        for (code in listOf(500, 502, 503, 404)) {
            respond(code)
            val failure = readFailure(http())
            assertEquals(SyncFailureKind.SERVER_ERROR, failure.kind, "código $code")
            assertEquals(code, failure.httpStatus)
        }
    }

    @Test fun `una respuesta que no es JSON es error del servidor`() {
        respond(200, "<html>Inicia sesión en la wifi</html>")
        assertEquals(SyncFailureKind.SERVER_ERROR, readFailure(http()).kind)
    }

    @Test fun `una respuesta JSON que no es una lista es error del servidor`() {
        respond(200, """{"error":"x"}""")
        assertEquals(SyncFailureKind.SERVER_ERROR, readFailure(http()).kind)
    }

    @Test fun `otros codigos 4xx son desconocidos y conservan el codigo`() {
        for (code in listOf(400, 403, 429)) {
            respond(code)
            val failure = readFailure(http())
            assertEquals(SyncFailureKind.UNKNOWN, failure.kind, "código $code")
            assertEquals(code, failure.httpStatus)
        }
    }

    @Test fun `un servidor apagado es sin conexion`() {
        val transport = http()
        server.shutdown()
        val failure = readFailure(transport)
        assertEquals(SyncFailureKind.OFFLINE, failure.kind)
        assertNull(failure.httpStatus)
    }

    @Test fun `un servidor que no contesta a tiempo es sin conexion`() {
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.NO_RESPONSE))
        assertEquals(SyncFailureKind.OFFLINE, readFailure(http(timeout = Duration.ofMillis(300))).kind)
    }

    @Test fun `una conexion cortada sin respuesta es sin conexion`() {
        // Un proxy, un túnel o un servidor que se cae a mitad cierran el socket sin contestar: OkHttp lo cuenta como «unexpected end of stream».
        server.enqueue(MockResponse().setSocketPolicy(SocketPolicy.DISCONNECT_AFTER_REQUEST))
        val failure = readFailure(http())
        assertEquals(SyncFailureKind.OFFLINE, failure.kind)
        assertNull(failure.httpStatus)
    }

    @Test fun `un nombre que no resuelve es sin conexion`() {
        val failure = readFailure(HttpSyncTransport("http://no-existe.invalid/", "token", Duration.ofSeconds(5)))
        assertEquals(SyncFailureKind.OFFLINE, failure.kind)
    }

    @Test fun `un sobre de un formato mas nuevo es formato no soportado`() {
        val future = SyncEnvelopeCodec.serialize(envelope().copy(format = 99))
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) =
                if (request.path == "/api/v1/objects") MockResponse().setBody("""[{"noteId":"$ID1"}]""") else MockResponse().setBody(future)
        }
        assertEquals(SyncFailureKind.UNSUPPORTED_FORMAT, readFailure(http()).kind)
    }

    @Test fun `un sobre de un formato demasiado viejo no se presenta como version nueva`() {
        val old = SyncEnvelopeCodec.serialize(envelope().copy(format = 0))
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) =
                if (request.path == "/api/v1/objects") MockResponse().setBody("""[{"noteId":"$ID1"}]""") else MockResponse().setBody(old)
        }
        assertEquals(SyncFailureKind.UNKNOWN, readFailure(http()).kind)
    }

    // --- WebDAV ---

    @Test fun `401 de WebDAV es no autorizado`() {
        respond(401)
        val failure = readFailure(webDav())
        assertEquals(SyncFailureKind.UNAUTHORIZED, failure.kind)
        assertEquals(401, failure.httpStatus)
    }

    @Test fun `500 de WebDAV es error del servidor`() {
        respond(500)
        assertEquals(SyncFailureKind.SERVER_ERROR, readFailure(webDav()).kind)
    }

    @Test fun `413 al subir a WebDAV es demasiado grande`() {
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) =
                MockResponse().setResponseCode(if (request.method == "PUT") 413 else 405)
        }
        assertEquals(SyncFailureKind.TOO_LARGE, failureOf { webDav().write(envelope()) }.kind)
    }

    @Test fun `un listado WebDAV que no es XML es error del servidor`() {
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) =
                if (request.method == "PROPFIND") MockResponse().setResponseCode(207).setBody("esto no es xml") else MockResponse().setResponseCode(405)
        }
        assertEquals(SyncFailureKind.SERVER_ERROR, readFailure(webDav()).kind)
    }

    @Test fun `WebDAV apagado es sin conexion`() {
        val transport = webDav()
        server.shutdown()
        assertEquals(SyncFailureKind.OFFLINE, readFailure(transport).kind)
    }

    // --- excepciones sueltas y errores del plan ---

    @Test fun `los tipos de red conocidos son sin conexion, tambien envueltos`() {
        assertEquals(SyncFailureKind.OFFLINE, classifyFailure(UnknownHostException("x")).kind)
        assertEquals(SyncFailureKind.OFFLINE, classifyFailure(ConnectException("x")).kind)
        assertEquals(SyncFailureKind.OFFLINE, classifyFailure(SocketTimeoutException("x")).kind)
        assertEquals(SyncFailureKind.OFFLINE, classifyFailure(java.io.InterruptedIOException("timeout")).kind)
        assertEquals(SyncFailureKind.OFFLINE, classifyFailure(java.net.SocketException("Connection reset")).kind)
        assertEquals(SyncFailureKind.OFFLINE, classifyFailure(RuntimeException("envuelta", UnknownHostException("x"))).kind)
    }

    @Test fun `un IOException cualquiera es desconocido y lo demas tambien`() {
        assertEquals(SyncFailureKind.UNKNOWN, classifyFailure(IOException("algo")).kind)
        assertEquals(SyncFailureKind.UNKNOWN, classifyFailure(IllegalStateException("otra cosa")).kind)
        assertEquals(SyncFailureKind.UNKNOWN, classifyFailure(SyncFormatException("roto")).kind)
    }

    @Test fun `el detalle lleva el tipo y el mensaje, acotado, y nunca esta vacio`() {
        assertEquals("IOException: algo", classifyFailure(IOException("algo")).detail)
        assertEquals("java.lang.IllegalStateException", classifyFailure(IllegalStateException()).detail)
        assertTrue(classifyFailure(IOException("x".repeat(5000))).detail!!.length <= 500)
    }

    @Test fun `ninguna nota descifra es clave cambiada y otro error del plan es desconocido`() {
        assertEquals(SyncFailureKind.KEY_CHANGED, classifyPlanError(NOTHING_READABLE_ERROR).kind)
        assertEquals(SyncFailureKind.UNKNOWN, classifyPlanError("otra cosa").kind)
        assertFalse(classifyPlanError(NOTHING_READABLE_ERROR).detail.isNullOrBlank())
    }
}
