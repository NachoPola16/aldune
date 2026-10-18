package app.aldune.core

import kotlinx.serialization.json.jsonObject
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNotEquals
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test
import java.util.Base64

class ProtectedNoteTest {
    private val vector = Vectors.objects("protectedNotes").first()
    private val password = vector.str("password")
    private val plaintext = vector.str("plaintext")

    private fun vectorContent(): ProtectedNoteContent =
        SyncEnvelopeCodec.readProtectedContent(vector.getValue("content").jsonObject)

    @Test
    fun `descifra la nota protegida de los vectores`() {
        assertEquals(600_000, vector.getValue("pbkdf2Iterations").toString().toInt())
        assertEquals(plaintext, ProtectedNotes.tryUnprotect(vectorContent(), password))
    }

    @Test
    fun `la contrasena con acentos se codifica en UTF-8`() {
        // "contraseña-de-prueba": la ñ es la que distingue UTF-8 de Latin-1 al derivar la clave.
        assertTrue(password.any { it.code > 127 })
        assertNotEquals(null, ProtectedNotes.tryUnprotect(vectorContent(), password))
    }

    @Test
    fun `contrasena equivocada o vacia devuelve null`() {
        assertNull(ProtectedNotes.tryUnprotect(vectorContent(), "otra-contraseña"))
        assertNull(ProtectedNotes.tryUnprotect(vectorContent(), ""))
        assertNull(ProtectedNotes.tryUnprotect(vectorContent(), "   "))
    }

    @Test
    fun `una version desconocida o datos mal formados devuelven null`() {
        val c = vectorContent()
        assertNull(ProtectedNotes.tryUnprotect(c.copy(version = 2), password))
        assertNull(ProtectedNotes.tryUnprotect(c.copy(salt = "AAAA"), password))
        assertNull(ProtectedNotes.tryUnprotect(c.copy(nonce = "AAAA"), password))
        assertNull(ProtectedNotes.tryUnprotect(c.copy(tag = "no es base64"), password))
        assertNull(ProtectedNotes.tryUnprotect(c.copy(cipherText = "AAAA"), password))
    }

    @Test
    fun `ida y vuelta con sal y nonce aleatorios`() {
        val a = ProtectedNotes.protect("hola ☐ mundo ñ", password)
        val b = ProtectedNotes.protect("hola ☐ mundo ñ", password)
        assertEquals(1, a.version)
        assertEquals(16, Base64.getDecoder().decode(a.salt).size)
        assertEquals(12, Base64.getDecoder().decode(a.nonce).size)
        assertEquals(16, Base64.getDecoder().decode(a.tag).size)
        assertNotEquals(a.salt, b.salt)
        assertNotEquals(a.nonce, b.nonce)
        assertEquals("hola ☐ mundo ñ", ProtectedNotes.tryUnprotect(a, password))
        assertNull(ProtectedNotes.tryUnprotect(a, password + "x"))
    }

    @Test
    fun `reprotect conserva la sal y cambia el nonce`() {
        val c = vectorContent()
        val again = ProtectedNotes.reprotect(c, "texto nuevo", password)
        assertEquals(c.salt, again.salt)
        assertNotEquals(c.nonce, again.nonce)
        assertEquals("texto nuevo", ProtectedNotes.tryUnprotect(again, password))
    }

    @Test
    fun `no hay capa base64 intermedia y se cifra el texto utf-8 directamente`() {
        // Si hubiera capa base64, el texto descifrado de los vectores no sería el texto original.
        assertEquals(plaintext, ProtectedNotes.tryUnprotect(vectorContent(), password))
        assertFalse(plaintext.matches(Regex("^[A-Za-z0-9+/=]+$")))
    }

    @Test
    fun `la contrasena debe tener al menos cuatro caracteres para proteger`() {
        assertThrows(IllegalArgumentException::class.java) { ProtectedNotes.protect("x", "abc") }
        assertThrows(IllegalArgumentException::class.java) { ProtectedNotes.protect("x", "    ") }
    }
}

class UnlockedProtectedNoteTest {
    private val password = "contraseña-de-prueba"

    @Test
    fun `unlock devuelve el texto y null con otra contrasena`() {
        val content = ProtectedNotes.protect("hola ☐ ñ", password)
        assertNull(ProtectedNotes.unlock(content, "otra-contraseña"))
        assertNull(ProtectedNotes.unlock(content, ""))
        val unlocked = ProtectedNotes.unlock(content, password)!!
        assertEquals("hola ☐ ñ", unlocked.text)
    }

    @Test
    fun `ida y vuelta protect unlock reprotect y tryUnprotect con la contrasena original`() {
        val content = ProtectedNotes.protect("texto viejo", password)
        val unlocked = ProtectedNotes.unlock(content, password)!!
        val again = unlocked.reprotect("texto nuevo")
        assertEquals("texto nuevo", ProtectedNotes.tryUnprotect(again, password))
        assertNull(ProtectedNotes.tryUnprotect(again, "otra-contraseña"))
    }

    @Test
    fun `reprotect conserva la sal y estrena nonce cada vez`() {
        val content = ProtectedNotes.protect("a", password)
        val unlocked = ProtectedNotes.unlock(content, password)!!
        val first = unlocked.reprotect("b")
        val second = unlocked.reprotect("b")
        assertEquals(content.salt, first.salt)
        assertEquals(content.salt, second.salt)
        assertNotEquals(content.nonce, first.nonce)
        assertNotEquals(first.nonce, second.nonce)
    }

    @Test
    fun `reprotect no deriva la clave otra vez`() {
        val unlocked = ProtectedNotes.unlock(ProtectedNotes.protect("a", password), password)!!
        // Con 600.000 iteraciones una derivación son decenas de ms como mínimo; cien reprotect sin derivar caben de sobra.
        val started = System.nanoTime()
        repeat(100) { unlocked.reprotect("texto $it") }
        val ms = (System.nanoTime() - started) / 1_000_000
        assertTrue(ms < 2_000, "100 reprotect tardaron $ms ms: parece que derivan la clave")
    }

    @Test
    fun `unlock descifra el contenido de los vectores y su reprotect se abre con la contrasena`() {
        val vector = Vectors.objects("protectedNotes").first()
        val content = SyncEnvelopeCodec.readProtectedContent(vector.getValue("content").jsonObject)
        val unlocked = ProtectedNotes.unlock(content, vector.str("password"))!!
        assertEquals(vector.str("plaintext"), unlocked.text)
        assertEquals("nuevo", ProtectedNotes.tryUnprotect(unlocked.reprotect("nuevo"), vector.str("password")))
    }

    @Test
    fun `tras close usarla lanza y la clave queda a cero`() {
        val unlocked = ProtectedNotes.unlock(ProtectedNotes.protect("a", password), password)!!
        unlocked.close()
        assertThrows(IllegalStateException::class.java) { unlocked.reprotect("b") }
        // El texto en memoria también se considera cerrado: es lo que la sesión debe soltar al salir de la nota.
        assertThrows(IllegalStateException::class.java) { unlocked.text }
        unlocked.close() // idempotente
    }

    @Test
    fun `unlock rechaza una version desconocida o datos mal formados`() {
        val c = ProtectedNotes.protect("a", password)
        assertNull(ProtectedNotes.unlock(c.copy(version = 2), password))
        assertNull(ProtectedNotes.unlock(c.copy(salt = "AAAA"), password))
        assertNull(ProtectedNotes.unlock(c.copy(nonce = "AAAA"), password))
    }
}

class UnlockedProtectedNoteTextTest {
    @Test
    fun `text sigue siendo el de al abrir despues de reprotect`() {
        val unlocked = ProtectedNotes.unlock(ProtectedNotes.protect("original", "clave-1234"), "clave-1234")!!
        unlocked.reprotect("otro")
        assertEquals("original", unlocked.text)
    }
}
