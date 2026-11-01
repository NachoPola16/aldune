package app.aldune.sync

import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test

class InvitationsTest {
    // Generado por SyncInvite.Format de Core (C#): las dos implementaciones leen el mismo texto.
    private val fromCSharp = "aldune-invite:eyJVIjoiaHR0cHM6Ly9hbGR1bmUuZXhhbXBsZS5jb20vIiwiQyI6InRoZS1jb2RlIn0"

    @Test
    fun `lee el texto que escribe Core`() {
        assertEquals(Invitation("https://aldune.example.com/", "the-code"), Invitations.parse(fromCSharp))
    }

    @Test
    fun `tolera lo que añade un chat`() {
        val wrapped = "  " + fromCSharp.substring(0, 20) + "\r\n " + fromCSharp.substring(20) + "  "
        assertEquals("the-code", Invitations.parse(wrapped)?.code)
    }

    @Test
    fun `rechaza lo que no es una invitacion`() {
        for (text in listOf(null, "", "hola", "aldune-invite:", "aldune-invite:!!!", "aldune-invite:e30", "aldune-invite:" + "A".repeat(10_000))) {
            assertNull(Invitations.parse(text), "debía rechazar: $text")
        }
    }

    @Test
    fun `nunca acepta una direccion que no sea http`() {
        // {"U":"file:///c:/secret","C":"x"}
        val file = "aldune-invite:" + java.util.Base64.getUrlEncoder().withoutPadding().encodeToString("""{"U":"file:///c:/secret","C":"x"}""".toByteArray())
        assertNull(Invitations.parse(file))
    }

    @Test
    fun `canjea el codigo y devuelve el token`() {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setBody("""{"token":"tok123","space":"u-abcd1234"}"""))
            val token = Invitations.redeem(Invitation(server.url("/").toString(), "the-code"))
            assertEquals("tok123", token)
            val request = server.takeRequest()
            assertEquals("/invites/v1/redeem", request.path)
            assertEquals("""{"code":"the-code"}""", request.body.readUtf8())
        }
    }

    @Test
    fun `cada fallo del servidor se distingue`() {
        val expected = mapOf(400 to InviteFailure.INVALID_CODE, 404 to InviteFailure.NOT_SUPPORTED, 409 to InviteFailure.SERVER_FULL, 429 to InviteFailure.TOO_MANY_ATTEMPTS, 500 to InviteFailure.SERVER)
        for ((status, failure) in expected) {
            MockWebServer().use { server ->
                server.enqueue(MockResponse().setResponseCode(status))
                val error = assertThrows(InviteException::class.java) { Invitations.redeem(Invitation(server.url("/").toString(), "x")) }
                assertEquals(failure, error.failure, "HTTP $status")
            }
        }
    }

    @Test
    fun `crea una invitacion con el token de administrador y devuelve el texto que lee parse`() {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setBody("""{"code":"fresh-code","expiresAt":"2026-11-08T00:00:00Z"}"""))
            val url = server.url("/").toString()

            val text = Invitations.create(url, "  admin-token ", "hermano")

            val request = server.takeRequest()
            assertEquals("/admin/v1/invites", request.path)
            assertEquals("Bearer admin-token", request.getHeader("Authorization"))
            assertEquals("""{"label":"hermano"}""", request.body.readUtf8())
            assertEquals(Invitation(url, "fresh-code"), Invitations.parse(text))
        }
    }

    @Test
    fun `una invitacion sin etiqueta no la manda`() {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setBody("""{"code":"c"}"""))
            Invitations.create(server.url("/").toString(), "t", "  ")
            assertEquals("{}", server.takeRequest().body.readUtf8())
        }
    }

    @Test
    fun `lista los espacios del mas nuevo al mas viejo`() {
        MockWebServer().use { server ->
            server.enqueue(
                MockResponse().setBody(
                    """[{"name":"u-1","label":"vieja","createdAt":"2026-10-01T10:00:00+00:00"},
                       {"name":"u-2","label":null,"createdAt":"2026-11-01T10:00:00.123+00:00"}]"""
                )
            )

            val spaces = Invitations.listSpaces(server.url("/").toString(), "adm")

            assertEquals(listOf("u-2", "u-1"), spaces.map { it.name })
            assertNull(spaces[0].label)
            assertEquals("vieja", spaces[1].label)
            assertEquals("/admin/v1/spaces", server.takeRequest().path)
        }
    }

    @Test
    fun `revoca un espacio y distingue el que ya no existe`() {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setResponseCode(204))
            server.enqueue(MockResponse().setResponseCode(404))
            val url = server.url("/").toString()

            assertEquals(true, Invitations.revokeSpace(url, "adm", "u-1"))
            assertEquals(false, Invitations.revokeSpace(url, "adm", "u-1"))

            val request = server.takeRequest()
            assertEquals("DELETE", request.method)
            assertEquals("/admin/v1/spaces/u-1", request.path)
        }
    }

    @Test
    fun `un token de administrador malo es no permitido y no cuenta como falta de soporte`() {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setResponseCode(401))
            val error = assertThrows(InviteException::class.java) { Invitations.listSpaces(server.url("/").toString(), "mal") }
            assertEquals(InviteFailure.NOT_ALLOWED, error.failure)
        }
    }

    @Test
    fun `sin servidor es sin conexion`() {
        val error = assertThrows(InviteException::class.java) { Invitations.redeem(Invitation("http://127.0.0.1:1/", "x")) }
        assertEquals(InviteFailure.OFFLINE, error.failure)
    }

    @Test
    fun `no sigue redirecciones y el codigo solo va al servidor que se escribio`() {
        MockWebServer().use { other ->
            MockWebServer().use { server ->
                server.enqueue(MockResponse().setResponseCode(307).setHeader("Location", other.url("/invites/v1/redeem").toString()))
                val error = assertThrows(InviteException::class.java) { Invitations.redeem(Invitation(server.url("/").toString(), "x")) }
                assertEquals(InviteFailure.SERVER, error.failure)
                assertEquals(0, other.requestCount)
            }
        }
    }

    @Test
    fun `una respuesta enorme es un fallo del servidor y no agota la memoria`() {
        MockWebServer().use { server ->
            server.enqueue(MockResponse().setBody(okio.Buffer().write(ByteArray(9 * 1024 * 1024) { 'a'.code.toByte() })))
            val error = assertThrows(InviteException::class.java) { Invitations.redeem(Invitation(server.url("/").toString(), "x")) }
            assertEquals(InviteFailure.SERVER, error.failure)
        }
    }
}
