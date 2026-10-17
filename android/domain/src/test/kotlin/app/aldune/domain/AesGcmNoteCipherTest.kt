package app.aldune.domain

import java.security.GeneralSecurityException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse

class AesGcmNoteCipherTest {
    private val key = ByteArray(32) { it.toByte() }
    private val cipher = AesGcmNoteCipher(key)

    @Test
    fun `ida y vuelta con texto vacio, acentos y emoji`() {
        for (text in listOf("", "hola", "☐ comprar leche\n→ viñeta ñandú", "😀 con par sustituto", "a".repeat(100_000))) {
            assertEquals(text, cipher.decrypt("n1", cipher.encrypt("n1", text)))
        }
    }

    @Test
    fun `nonce de 12 bytes y tag de 16, distinto en cada cifrado`() {
        val a = cipher.encrypt("n1", "igual")
        val b = cipher.encrypt("n1", "igual")
        assertEquals(12, a.nonce.size)
        assertEquals(16, a.tag.size)
        assertEquals("igual".toByteArray().size, a.cipherText.size)
        assertFalse(a.nonce.contentEquals(b.nonce))
        assertFalse(a.cipherText.contentEquals(b.cipherText))
    }

    @Test
    fun `una fila copiada a otro id no descifra`() {
        val encrypted = cipher.encrypt("n1", "secreto")
        assertFailsWith<GeneralSecurityException> { cipher.decrypt("n2", encrypted) }
    }

    @Test
    fun `con otra clave o con datos alterados lanza, no devuelve basura`() {
        val encrypted = cipher.encrypt("n1", "secreto")
        assertFailsWith<GeneralSecurityException> { AesGcmNoteCipher(ByteArray(32) { 7 }).decrypt("n1", encrypted) }
        val flipped = encrypted.cipherText.copyOf().also { it[0] = (it[0].toInt() xor 1).toByte() }
        assertFailsWith<GeneralSecurityException> { cipher.decrypt("n1", EncryptedText(flipped, encrypted.nonce, encrypted.tag)) }
        val badTag = encrypted.tag.copyOf().also { it[0] = (it[0].toInt() xor 1).toByte() }
        assertFailsWith<GeneralSecurityException> { cipher.decrypt("n1", EncryptedText(encrypted.cipherText, encrypted.nonce, badTag)) }
    }

    @Test
    fun `la clave debe ser de 32 bytes`() {
        assertFailsWith<IllegalArgumentException> { AesGcmNoteCipher(ByteArray(16)) }
    }

    @Test
    fun `no deja la clave modificable desde fuera`() {
        val mine = ByteArray(32) { 1 }
        val c = AesGcmNoteCipher(mine)
        val encrypted = c.encrypt("n1", "x")
        mine.fill(0)
        assertEquals("x", c.decrypt("n1", encrypted))
    }
}
