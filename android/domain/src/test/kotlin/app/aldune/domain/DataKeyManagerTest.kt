package app.aldune.domain

import java.io.File
import java.nio.file.Files
import java.security.GeneralSecurityException
import kotlin.test.AfterTest
import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertTrue

class DataKeyManagerTest {
    private val dir: File = Files.createTempDirectory("aldune-datakey").toFile()
    private val keyFile = File(dir, "datakey.bin")
    private val wrapper = FakeKeyWrapper()
    private val manager = DataKeyManager(keyFile, wrapper)

    @AfterTest fun cleanUp() {
        dir.deleteRecursively()
    }

    @Test
    fun `primera vez sin datos crea la clave y la guarda envuelta`() {
        val state = manager.open(hasExistingData = false)

        val ready = assertIs<DataKeyState.Ready>(state)
        assertTrue(ready.created)
        assertEquals(32, ready.key.size)
        assertTrue(keyFile.exists())
        // Lo que queda en disco no es la clave en claro.
        assertFalse(keyFile.readBytes().toList().windowed(32).any { it == ready.key.toList() })
    }

    @Test
    fun `al volver a abrir devuelve la misma clave y no crea otra`() {
        val first = assertIs<DataKeyState.Ready>(manager.open(hasExistingData = false))
        val again = assertIs<DataKeyState.Ready>(DataKeyManager(keyFile, wrapper).open(hasExistingData = true))

        assertFalse(again.created)
        assertContentEquals(first.key, again.key)
    }

    @Test
    fun `sin fichero pero con datos es clave perdida, y no se genera otra en silencio`() {
        val state = manager.open(hasExistingData = true)

        assertEquals(DataKeyState.Lost(LostReason.KEY_FILE_MISSING), state)
        assertFalse(keyFile.exists())
    }

    @Test
    fun `si el Keystore perdio su clave es clave perdida y el fichero se conserva`() {
        manager.open(hasExistingData = false)
        val before = keyFile.readBytes()
        wrapper.delete()

        assertEquals(DataKeyState.Lost(LostReason.KEYSTORE_KEY_MISSING), manager.open(hasExistingData = true))
        assertContentEquals(before, keyFile.readBytes())
    }

    @Test
    fun `si el Keystore cambio de clave el envoltorio no abre`() {
        manager.open(hasExistingData = false)
        wrapper.rotate()

        assertEquals(DataKeyState.Lost(LostReason.UNWRAP_FAILED), manager.open(hasExistingData = true))
    }

    @Test
    fun `un fichero danado o de otra version es clave perdida`() {
        manager.open(hasExistingData = false)
        val bytes = keyFile.readBytes()

        keyFile.writeBytes(bytes.copyOf().also { it[it.size - 1] = (it[it.size - 1].toInt() xor 1).toByte() })
        assertEquals(DataKeyState.Lost(LostReason.UNWRAP_FAILED), manager.open(hasExistingData = true))

        keyFile.writeBytes(bytes.copyOf().also { it[0] = 99 })
        assertEquals(DataKeyState.Lost(LostReason.UNKNOWN_FORMAT), manager.open(hasExistingData = true))

        keyFile.writeBytes(ByteArray(0))
        assertEquals(DataKeyState.Lost(LostReason.UNKNOWN_FORMAT), manager.open(hasExistingData = true))
    }

    @Test
    fun `un envoltorio que abre pero no da 32 bytes es clave perdida`() {
        keyFile.writeBytes(byteArrayOf(DataKeyManager.FORMAT_VERSION) + wrapper.also { it.ensureKey() }.wrap(ByteArray(16)))

        assertEquals(DataKeyState.Lost(LostReason.UNWRAP_FAILED), manager.open(hasExistingData = true))
    }

    @Test
    fun `empezar de cero borra datos, fichero y clave del Keystore, en ese orden, y crea una nueva`() {
        val old = assertIs<DataKeyState.Ready>(manager.open(hasExistingData = false)).key
        wrapper.rotate()
        assertIs<DataKeyState.Lost>(manager.open(hasExistingData = true))
        val order = mutableListOf<String>()
        wrapper.onDelete = { order += "keystore" }

        val fresh = manager.startOver(deleteData = { order += "datos"; assertTrue(keyFile.exists()) })

        assertEquals(listOf("datos", "keystore"), order)
        assertTrue(fresh.created)
        assertFalse(fresh.key.contentEquals(old))
        assertContentEquals(fresh.key, assertIs<DataKeyState.Ready>(manager.open(hasExistingData = true)).key)
    }

    @Test
    fun `si borrar los datos falla no se toca la clave`() {
        manager.open(hasExistingData = false)
        val before = keyFile.readBytes()

        runCatching { manager.startOver(deleteData = { error("disco lleno") }) }

        assertContentEquals(before, keyFile.readBytes())
        assertTrue(wrapper.hasKey())
    }

    @Test
    fun `la escritura es atomica - no queda un fichero temporal`() {
        manager.open(hasExistingData = false)

        assertEquals(listOf("datakey.bin"), dir.list()!!.toList())
    }

    @Test
    fun `cada clave nueva es distinta`() {
        val a = assertIs<DataKeyState.Ready>(manager.open(hasExistingData = false)).key
        keyFile.delete()
        val b = assertIs<DataKeyState.Ready>(manager.open(hasExistingData = false)).key

        assertFalse(a.contentEquals(b))
    }
}

/** Doble del Keystore: una clave AES de verdad en memoria, que se puede «perder» o cambiar para simular lo que hacen algunos móviles. */
private class FakeKeyWrapper : KeyWrapper {
    private var key: AesGcmNoteCipher? = null
    var onDelete: () -> Unit = {}

    override fun hasKey(): Boolean = key != null

    override fun ensureKey() {
        if (key == null) rotate()
    }

    fun rotate() {
        key = AesGcmNoteCipher(ByteArray(32).also(java.security.SecureRandom()::nextBytes))
    }

    override fun wrap(plain: ByteArray): ByteArray {
        val sealed = (key ?: throw KeyMissingException()).encrypt(WRAP_ID, plain.joinToString(",") { it.toString() })
        return sealed.nonce + sealed.tag + sealed.cipherText
    }

    override fun unwrap(blob: ByteArray): ByteArray {
        val k = key ?: throw KeyMissingException()
        if (blob.size < 28) throw GeneralSecurityException("corto")
        val text = k.decrypt(WRAP_ID, EncryptedText(blob.copyOfRange(28, blob.size), blob.copyOfRange(0, 12), blob.copyOfRange(12, 28)))
        return if (text.isEmpty()) ByteArray(0) else text.split(",").map { it.toByte() }.toByteArray()
    }

    override fun delete() {
        onDelete()
        key = null
    }

    private companion object {
        const val WRAP_ID = "wrap"
    }
}
