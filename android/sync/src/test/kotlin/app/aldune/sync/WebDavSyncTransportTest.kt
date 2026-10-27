package app.aldune.sync

import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncNote
import okhttp3.mockwebserver.Dispatcher
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import okhttp3.mockwebserver.RecordedRequest
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
import java.util.Base64
import java.util.UUID

/**
 * El transporte WebDAV contra un MockWebServer que imita las respuestas de Nextcloud y de otros servidores
 * (`PROPFIND` con `Depth: 1`, `MKCOL`, `GET/PUT/DELETE`). Como en Core, un objeto ilegible se salta.
 */
class WebDavSyncTransportTest {
    private val key = ByteArray(32) { it.toByte() }
    private val id = UUID.fromString("00000001-0000-4000-8000-000000000000")
    private val idN = "00000001000040008000000000000000"
    private lateinit var server: MockWebServer

    @BeforeEach
    fun start() {
        server = MockWebServer().also { it.start() }
    }

    @AfterEach
    fun stop() = server.shutdown()

    private fun url(path: String = "/") = server.url(path).toString()

    private fun transport(path: String = "/dav/", user: String = "ana", pass: String = "clave") =
        WebDavSyncTransport(url(path), user, pass)

    private fun note(text: String, minute: Int, noteId: UUID = id) = SyncNote(
        id = noteId, text = text, color = "#EBD38B",
        createdAt = OffsetDateTime.of(2024, 5, 1, 9, 0, 0, 0, ZoneOffset.UTC),
        updatedAt = OffsetDateTime.of(2024, 5, 1, 10, minute, 0, 0, ZoneOffset.UTC),
        state = 0, screenOrigin = "primary", tags = emptyList(), isProtected = false,
        protectedContent = null, dockPosition = null,
    )

    private fun envelope(text: String, minute: Int, noteId: UUID = id) =
        SyncEnvelopeCodec.createNote(note(text, minute, noteId), key, "device-b")

    private fun multistatus(vararg entries: String, prefix: String = "d:", declaration: String = "xmlns:d=\"DAV:\"") =
        """<?xml version="1.0"?><${prefix}multistatus $declaration>${entries.joinToString("")}</${prefix}multistatus>"""

    private fun fileEntry(href: String, p: String = "d:") =
        "<${p}response><${p}href>$href</${p}href><${p}propstat><${p}prop><${p}resourcetype/></${p}prop><${p}status>HTTP/1.1 200 OK</${p}status></${p}propstat></${p}response>"

    private fun folderEntry(href: String, p: String = "d:") =
        "<${p}response><${p}href>$href</${p}href><${p}propstat><${p}prop><${p}resourcetype><${p}collection/></${p}resourcetype></${p}prop><${p}status>HTTP/1.1 200 OK</${p}status></${p}propstat></${p}response>"

    /** Un servidor WebDAV mínimo: la carpeta existe (MKCOL → 405), el listado y los GET salen de [files]. */
    private fun serve(listing: String, files: Map<String, String> = emptyMap(), mkcol: Int = 405) {
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse = when (request.method) {
                "MKCOL" -> MockResponse().setResponseCode(mkcol)
                "PROPFIND" -> MockResponse().setResponseCode(207).setHeader("Content-Type", "application/xml").setBody(listing)
                "GET" -> files[request.path]?.let { MockResponse().setBody(it) } ?: MockResponse().setResponseCode(404)
                else -> MockResponse().setResponseCode(404)
            }
        }
    }

    @Test
    fun `una carpeta vacia devuelve una lista vacia`() {
        serve(multistatus(folderEntry("/dav/objects/")))
        assertEquals(emptyList<SyncEnvelope>(), transport().readAll())
    }

    @Test
    fun `lee varios objetos y manda PROPFIND con Depth 1 y Basic`() {
        val a = envelope("uno", 10)
        val b = envelope("dos", 20, UUID(0, 2))
        serve(
            multistatus(
                folderEntry("/dav/objects/"),
                fileEntry("/dav/objects/$idN.json"),
                fileEntry("/dav/objects/00000000000000000000000000000002.json"),
            ),
            mapOf(
                "/dav/objects/$idN.json" to SyncEnvelopeCodec.serialize(a),
                "/dav/objects/00000000000000000000000000000002.json" to SyncEnvelopeCodec.serialize(b),
            ),
        )

        val read = transport().readAll()
        assertEquals(listOf(a.noteId, b.noteId), read.map { it.noteId })
        assertEquals("uno", SyncEnvelopeCodec.decryptNote(read[0], key).text)

        val expected = "Basic " + Base64.getEncoder().encodeToString("ana:clave".toByteArray())
        val mkcol = server.takeRequest()
        assertEquals("MKCOL", mkcol.method)
        assertEquals("/dav/objects/", mkcol.path)
        val propfind = server.takeRequest()
        assertEquals("PROPFIND", propfind.method)
        assertEquals("1", propfind.getHeader("Depth"))
        assertTrue(propfind.body.readUtf8().contains("<d:resourcetype/>"))
        repeat(2) { assertEquals(expected, server.takeRequest().getHeader("Authorization")) } // los dos GET
        assertEquals(expected, mkcol.getHeader("Authorization"))
        assertEquals(expected, propfind.getHeader("Authorization"))
    }

    @Test
    fun `Basic usa UTF-8 con contrasenas con tildes`() {
        serve(multistatus(folderEntry("/dav/objects/")))
        transport(user = "ñandú", pass = "contraseña").readAll()
        val expected = "Basic " + Base64.getEncoder().encodeToString("ñandú:contraseña".toByteArray(Charsets.UTF_8))
        assertEquals(expected, server.takeRequest().getHeader("Authorization"))
    }

    @Test
    fun `respuesta tipo Nextcloud con URL absoluta ruta codificada y espacio de nombres extra`() {
        val a = envelope("nube", 10)
        val href = "/remote.php/dav/files/ana/Mis%20notas/objects/$idN.json"
        val ns = "xmlns:d=\"DAV:\" xmlns:s=\"http://sabredav.org/ns\" xmlns:oc=\"http://owncloud.org/ns\""
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse = when {
                request.method == "MKCOL" -> MockResponse().setResponseCode(405)
                request.method == "PROPFIND" -> MockResponse().setResponseCode(207).setBody(
                    multistatus(
                        folderEntry("/remote.php/dav/files/ana/Mis%20notas/objects/"),
                        // Con la URL completa y un host interno: solo se usa la ruta, las credenciales no salen a otro host.
                        fileEntry("http://interno.invalid$href"),
                        declaration = ns,
                    ),
                )
                request.path == href -> MockResponse().setBody(SyncEnvelopeCodec.serialize(a))
                else -> MockResponse().setResponseCode(404)
            }
        }
        val read = WebDavSyncTransport(url("/remote.php/dav/files/ana/Mis%20notas"), "ana", "clave").readAll()
        assertEquals(listOf(a.noteId), read.map { it.noteId })
    }

    @Test
    fun `un href relativo al esquema que apunta a otro host no recibe las credenciales`() {
        // `//otro-host/x.json` se resuelve contra la URL base como un host distinto: seguirlo enviaría
        // la cabecera Authorization al servidor de un atacante. Se ignora en vez de pedirlo.
        serve(multistatus(folderEntry("/dav/objects/"), fileEntry("//evil.invalid/dav/objects/$idN.json")))
        assertEquals(emptyList<UUID>(), transport().readAll().map { it.noteId })
    }

    @Test
    fun `una respuesta mas grande que el tope se rechaza en vez de cargarla entera`() {
        serve("x".repeat(9 * 1024 * 1024))
        val error = assertThrows(IOException::class.java) { transport().readAll() }
        assertTrue(error.message!!.contains("larger than"), error.message)
    }

    @Test
    fun `respuestas con otros prefijos o sin prefijo`() {
        val a = envelope("x", 10)
        val file = "/dav/objects/$idN.json"
        for ((prefix, declaration) in listOf("D:" to "xmlns:D=\"DAV:\"", "" to "xmlns=\"DAV:\"", "ns0:" to "xmlns:ns0=\"DAV:\"")) {
            serve(multistatus(folderEntry("/dav/objects/", prefix), fileEntry(file, prefix), prefix = prefix, declaration = declaration), mapOf(file to SyncEnvelopeCodec.serialize(a)))
            assertEquals(listOf(a.noteId), transport().readAll().map { it.noteId }, "prefijo «$prefix»")
        }
    }

    @Test
    fun `se ignoran los ficheros que no son json y las carpetas`() {
        val a = envelope("x", 10)
        serve(
            multistatus(folderEntry("/dav/objects/"), folderEntry("/dav/objects/sub.json/"), fileEntry("/dav/objects/readme.txt"), fileEntry("/dav/objects/$idN.JSON")),
            mapOf("/dav/objects/$idN.JSON" to SyncEnvelopeCodec.serialize(a)),
        )
        assertEquals(listOf(a.noteId), transport().readAll().map { it.noteId })
    }

    @Test
    fun `un objeto de formato 5 o dañado se salta y los demas se leen`() {
        val good = envelope("bueno", 10)
        val future = SyncEnvelopeCodec.serialize(envelope("futura", 10, UUID(0, 5))).replace("\"format\":4", "\"format\":5")
        serve(
            multistatus(
                fileEntry("/dav/objects/00000000000000000000000000000005.json"),
                fileEntry("/dav/objects/00000000000000000000000000000006.json"),
                fileEntry("/dav/objects/00000000000000000000000000000007.json"),
                fileEntry("/dav/objects/00000000000000000000000000000008.json"),
                fileEntry("/dav/objects/$idN.json"),
            ),
            mapOf(
                "/dav/objects/00000000000000000000000000000005.json" to future,
                "/dav/objects/00000000000000000000000000000006.json" to "{ esto no es json",
                "/dav/objects/00000000000000000000000000000007.json" to "",
                "/dav/objects/00000000000000000000000000000008.json" to """{"format":4,"noteId":"no-es-un-guid"}""",
                "/dav/objects/$idN.json" to SyncEnvelopeCodec.serialize(good),
            ),
        )
        assertEquals(listOf(good.noteId), transport().readAll().map { it.noteId })
    }

    @Test
    fun `un fallo al descargar un objeto aborta la lectura como en Core`() {
        serve(multistatus(fileEntry("/dav/objects/$idN.json")), emptyMap())
        val error = assertThrows(IOException::class.java) { transport().readAll() }
        assertTrue(error.message!!.contains("404"), error.message)
    }

    @Test
    fun `un 401 dice que el servidor rechazo las credenciales, en cualquier operacion`() {
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest) = MockResponse().setResponseCode(401)
        }
        val t = transport(pass = "mala")
        for (operation in listOf<() -> Unit>({ t.readAll() }, { t.write(envelope("x", 10)) }, { t.delete(id) })) {
            val error = assertThrows(IllegalStateException::class.java) { operation() }
            assertTrue(error.message!!.contains("rejected the credentials (401)"), error.message)
        }
    }

    @Test
    fun `un 404 al listar o un 5xx son IOException con el codigo`() {
        for (code in listOf(404, 500, 503)) {
            server.dispatcher = object : Dispatcher() {
                override fun dispatch(request: RecordedRequest) =
                    if (request.method == "MKCOL") MockResponse().setResponseCode(405) else MockResponse().setResponseCode(code)
            }
            val error = assertThrows(IOException::class.java) { transport().readAll() }
            assertTrue(error.message!!.contains(code.toString()), "código $code: ${error.message}")
        }
    }

    @Test
    fun `un listado que no es XML es un fallo normal`() {
        serve("<html>portal cautivo")
        assertThrows(IOException::class.java) { transport().readAll() }
        serve("")
        assertThrows(IOException::class.java) { transport().readAll() }
    }

    @Test
    fun `un listado con DTD se rechaza y no resuelve entidades externas`() {
        val secret = java.io.File.createTempFile("xxe", ".txt").apply { writeText("secreto"); deleteOnExit() }
        serve("""<?xml version="1.0"?><!DOCTYPE m [<!ENTITY x SYSTEM "${secret.toURI()}">]><d:multistatus xmlns:d="DAV:"><d:response><d:href>&x;</d:href></d:response></d:multistatus>""")
        assertThrows(IOException::class.java) { transport().readAll() }
        assertEquals(2, server.requestCount) // MKCOL y PROPFIND: no se intentó descargar nada
    }

    @Test
    fun `una entrada 207 con estado de error propio se salta`() {
        val ok = envelope("ok", 10)
        val broken = "<d:response><d:href>/dav/objects/00000000000000000000000000000009.json</d:href><d:status>HTTP/1.1 404 Not Found</d:status></d:response>"
        serve(multistatus(broken, fileEntry("/dav/objects/$idN.json")), mapOf("/dav/objects/$idN.json" to SyncEnvelopeCodec.serialize(ok)))
        assertEquals(listOf(ok.noteId), transport().readAll().map { it.noteId })
    }

    @Test
    fun `un objeto por encima del tope se salta y no impide el resto`() {
        val ok = envelope("ok", 10)
        val hugeId = "00000000000000000000000000000009"
        val huge = "x".repeat((MAX_RESPONSE_BYTES + 1024).toInt())
        serve(
            multistatus(fileEntry("/dav/objects/$hugeId.json"), fileEntry("/dav/objects/$idN.json")),
            mapOf("/dav/objects/$hugeId.json" to huge, "/dav/objects/$idN.json" to SyncEnvelopeCodec.serialize(ok)),
        )
        val t = transport()
        assertEquals(listOf(ok.noteId), t.readAll().map { it.noteId })
        // Existe en el almacén aunque no se leyó: se anota su id para que la sync no lo dé por inexistente.
        assertEquals(setOf(UUID.fromString("00000000-0000-0000-0000-000000000009")), t.unreadableIds)
    }

    @Test
    fun `MKCOL crea la carpeta si falta y solo una vez`() {
        val requests = mutableListOf<String>()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse {
                requests += "${request.method} ${request.path}"
                return when (request.method) {
                    "MKCOL" -> MockResponse().setResponseCode(201)
                    "PROPFIND" -> MockResponse().setResponseCode(207).setBody(multistatus(folderEntry("/dav/objects/")))
                    else -> MockResponse().setResponseCode(204)
                }
            }
        }
        val t = transport()
        t.readAll()
        t.write(envelope("x", 10))
        t.delete(id)
        assertEquals(listOf("MKCOL /dav/objects/", "PROPFIND /dav/objects/", "PUT /dav/objects/$idN.json", "DELETE /dav/objects/$idN.json"), requests)
    }

    @Test
    fun `si MKCOL falla (por ejemplo falta la carpeta padre) el error sale y se reintenta despues`() {
        server.enqueue(MockResponse().setResponseCode(409))
        val t = transport()
        val error = assertThrows(IOException::class.java) { t.write(envelope("x", 10)) }
        assertTrue(error.message!!.contains("409"), error.message)
        server.enqueue(MockResponse().setResponseCode(201))
        server.enqueue(MockResponse().setResponseCode(201))
        t.write(envelope("x", 10))
        assertEquals(listOf("MKCOL", "MKCOL", "PUT"), List(3) { server.takeRequest().method })
    }

    @Test
    fun `write hace PUT del sobre serializado y delete tolera el 404`() {
        server.enqueue(MockResponse().setResponseCode(405)) // MKCOL: ya existe
        server.enqueue(MockResponse().setResponseCode(201)) // PUT
        server.enqueue(MockResponse().setResponseCode(404)) // DELETE de algo que ya no está
        val t = transport()
        val e = envelope("subida", 10)
        t.write(e)
        t.delete(id)

        assertEquals("MKCOL", server.takeRequest().method)
        val put = server.takeRequest()
        assertEquals("PUT", put.method)
        assertEquals("/dav/objects/$idN.json", put.path)
        assertEquals("application/json; charset=utf-8", put.getHeader("Content-Type")?.lowercase())
        assertEquals(SyncEnvelopeCodec.serialize(e), put.body.readUtf8())
        val del = server.takeRequest()
        assertEquals("DELETE", del.method)
        assertEquals("/dav/objects/$idN.json", del.path)
    }

    @Test
    fun `delete y write con otros errores fallan`() {
        server.enqueue(MockResponse().setResponseCode(405))
        server.enqueue(MockResponse().setResponseCode(403))
        server.enqueue(MockResponse().setResponseCode(507))
        val t = transport()
        assertTrue(assertThrows(IOException::class.java) { t.delete(id) }.message!!.contains("403"))
        assertTrue(assertThrows(IOException::class.java) { t.write(envelope("x", 10)) }.message!!.contains("507"))
    }

    @Test
    fun `una URL sin barra final y con barra final son la misma carpeta`() {
        serve(multistatus(folderEntry("/dav/objects/")))
        WebDavSyncTransport(url("/dav"), "u", "p").readAll()
        WebDavSyncTransport(url("/dav/"), "u", "p").readAll()
        val paths = List(4) { server.takeRequest().path }
        assertEquals(listOf("/dav/objects/"), paths.distinct())
    }

    @Test
    fun `sin respuesta a tiempo falla con un IOException`() {
        server.enqueue(MockResponse().setSocketPolicy(okhttp3.mockwebserver.SocketPolicy.NO_RESPONSE))
        assertThrows(IOException::class.java) { WebDavSyncTransport(url("/dav/"), "u", "p", Duration.ofMillis(300)).readAll() }
    }

    @Test
    fun `una URL o unas credenciales invalidas se rechazan al crear el transporte`() {
        assertThrows(IllegalArgumentException::class.java) { WebDavSyncTransport("no es una url", "u", "p") }
        assertThrows(IllegalArgumentException::class.java) { WebDavSyncTransport("ftp://host/", "u", "p") }
        assertThrows(IllegalArgumentException::class.java) { WebDavSyncTransport(url(), " ", "p") }
        assertThrows(IllegalArgumentException::class.java) { WebDavSyncTransport(url(), "u", "") }
    }

    @Test
    fun `dos dispositivos se sincronizan a traves del servidor WebDAV`() {
        // Un WebDAV mínimo en memoria. Como FolderSyncTransport de Core, un PUT más antiguo que lo guardado no lo pisa (E12c).
        val store = linkedMapOf<String, String>()
        server.dispatcher = object : Dispatcher() {
            override fun dispatch(request: RecordedRequest): MockResponse {
                val path = request.path!!
                return when (request.method) {
                    "MKCOL" -> MockResponse().setResponseCode(405)
                    "PROPFIND" -> MockResponse().setResponseCode(207).setBody(
                        multistatus(folderEntry("/dav/objects/"), *store.keys.map { fileEntry("/dav/objects/$it") }.toTypedArray()),
                    )
                    "GET" -> store[path.substringAfterLast('/')]?.let { MockResponse().setBody(it) } ?: MockResponse().setResponseCode(404)
                    "PUT" -> {
                        val incoming = SyncEnvelopeCodec.deserialize(request.body.readUtf8())
                        val name = path.substringAfterLast('/')
                        val current = store[name]?.let { SyncEnvelopeCodec.deserialize(it) }
                        if (current == null || incoming.updatedAt.toInstant() >= current.updatedAt.toInstant()) store[name] = SyncEnvelopeCodec.serialize(incoming)
                        MockResponse().setResponseCode(if (current == null) 201 else 204)
                    }
                    "DELETE" -> if (store.remove(path.substringAfterLast('/')) != null) MockResponse().setResponseCode(204) else MockResponse().setResponseCode(404)
                    else -> MockResponse().setResponseCode(405)
                }
            }
        }
        val now = OffsetDateTime.of(2026, 10, 17, 12, 0, 0, 0, ZoneOffset.UTC)
        fun pass(device: String, local: InMemoryLocalStore): SyncResult {
            val transport = transport()
            return execute(
                planSync(SyncInput(device, SyncScope.All, local.notes.values.toList(), local.tombstones.values.toList(), local.bases, emptySet(), transport.readAll(), now), key),
                local, transport,
            )
        }

        val a = InMemoryLocalStore(listOf(LocalNote(note("desde A", 10), null)))
        val b = InMemoryLocalStore()
        assertEquals(1, pass("device-a", a).uploaded)
        assertEquals(1, pass("device-b", b).downloaded)
        assertEquals("desde A", b.notes.getValue(id).note.text)
        assertEquals(SyncResult(0, 0, 0, 0), pass("device-b", b))
        assertEquals(SyncResult(0, 0, 0, 0), pass("device-a", a))
        assertEquals(listOf("$idN.json"), store.keys.toList())
    }
}
