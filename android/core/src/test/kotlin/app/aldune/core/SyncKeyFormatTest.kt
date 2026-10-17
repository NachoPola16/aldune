package app.aldune.core

import org.junit.jupiter.api.Assertions.assertArrayEquals
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertThrows
import org.junit.jupiter.api.Test

class SyncKeyFormatTest {
    @Test
    fun `codifica la clave de los vectores en base64url sin relleno`() {
        assertEquals(Vectors.keyEncoded, SyncKeyFormat.encode(Vectors.keyBytes))
    }

    @Test
    fun `decodifica la clave de los vectores a 32 bytes`() {
        val key = SyncKeyFormat.decode(Vectors.keyEncoded)
        assertEquals(32, key.size)
        assertArrayEquals(Vectors.keyBytes, key)
    }

    @Test
    fun `acepta la clave con relleno y con espacios alrededor`() {
        assertArrayEquals(Vectors.keyBytes, SyncKeyFormat.decode("  " + Vectors.keyEncoded + "=  "))
    }

    @Test
    fun `usa el alfabeto url y no deja relleno`() {
        // 0xfb 0xff produce '+' y '/' en base64 estándar: aquí tienen que salir '-' y '_'.
        val key = ByteArray(32) { 0xfb.toByte() }
        val encoded = SyncKeyFormat.encode(key)
        assertFalse(encoded.contains('+') || encoded.contains('/') || encoded.contains('='))
        assertArrayEquals(key, SyncKeyFormat.decode(encoded))
    }

    @Test
    fun `rechaza longitudes distintas de 32 bytes`() {
        assertThrows(IllegalArgumentException::class.java) { SyncKeyFormat.decode(SyncKeyFormat.encode(ByteArray(16))) }
        assertThrows(IllegalArgumentException::class.java) { SyncKeyFormat.encode(ByteArray(31)) }
    }

    @Test
    fun `rechaza texto que no es base64`() {
        assertThrows(IllegalArgumentException::class.java) { SyncKeyFormat.decode("no es base64 !!") }
    }
}
