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
    fun `sin servidor es sin conexion`() {
        val error = assertThrows(InviteException::class.java) { Invitations.redeem(Invitation("http://127.0.0.1:1/", "x")) }
        assertEquals(InviteFailure.OFFLINE, error.failure)
    }
}
