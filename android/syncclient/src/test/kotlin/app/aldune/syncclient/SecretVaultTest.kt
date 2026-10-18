package app.aldune.syncclient

import app.aldune.domain.KeyMissingException
import app.aldune.domain.KeyWrapper
import java.io.File
import java.nio.file.Files
import java.security.GeneralSecurityException
import kotlin.test.AfterTest
import kotlin.test.BeforeTest
import kotlin.test.Test
import kotlin.test.assertContentEquals
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertTrue

class SecretVaultTest {
    /** Un envoltorio de mentira: XOR con 0x5A y una marca delante, para poder comprobar que el fichero no guarda nada en claro. */
    private class FakeWrapper : KeyWrapper {
        var present = true
        var broken = false
        override fun hasKey() = present
        override fun ensureKey() { present = true }
        override fun wrap(plain: ByteArray): ByteArray {
            if (!present) throw KeyMissingException()
            return byteArrayOf(0x7F) + ByteArray(plain.size) { (plain[it].toInt() xor 0x5A).toByte() }
        }
        override fun unwrap(blob: ByteArray): ByteArray {
            if (!present) throw KeyMissingException()
            if (broken || blob.isEmpty() || blob[0] != 0x7F.toByte()) throw GeneralSecurityException("no abre")
            return ByteArray(blob.size - 1) { (blob[it + 1].toInt() xor 0x5A).toByte() }
        }
        override fun delete() { present = false }
    }

    private lateinit var dir: File
    private lateinit var file: File
    private val wrapper = FakeWrapper()
    private val key = ByteArray(32) { (it * 3 + 1).toByte() }

    @BeforeTest fun setUp() {
        dir = Files.createTempDirectory("vault").toFile()
        file = File(dir, "sync.bin")
    }

    @AfterTest fun tearDown() {
        dir.deleteRecursively()
    }

    private fun vault() = SecretVault(file, wrapper)

    @Test fun sinFicheroNoHaySecretos() {
        assertIs<VaultState.Empty>(vault().load())
    }

    @Test fun guardaYLeeUnTokenDeServidor() {
        vault().save(SyncSecrets(key, ProfileCredentials.Token("tok-123")))
        val loaded = assertIs<VaultState.Ready>(vault().load()).secrets
        assertContentEquals(key, loaded.key)
        assertEquals(ProfileCredentials.Token("tok-123"), loaded.credentials)
    }

    @Test fun guardaYLeeUsuarioYContrasenaDeWebDavConAcentos() {
        vault().save(SyncSecrets(key, ProfileCredentials.Basic("ñandú", "contraseña \"rara\" ✓")))
        val loaded = assertIs<VaultState.Ready>(vault().load()).secrets
        assertEquals(ProfileCredentials.Basic("ñandú", "contraseña \"rara\" ✓"), loaded.credentials)
    }

    @Test fun elFicheroNoGuardaNadaEnClaro() {
        vault().save(SyncSecrets(key, ProfileCredentials.Token("tok-secreto-visible")))
        val bytes = file.readBytes()
        assertFalse(String(bytes, Charsets.ISO_8859_1).contains("tok-secreto-visible"))
        assertFalse(String(bytes, Charsets.ISO_8859_1).contains("token"))
    }

    @Test fun guardarDeNuevoReemplazaYNoDejaTemporales() {
        vault().save(SyncSecrets(key, ProfileCredentials.Token("uno")))
        vault().save(SyncSecrets(key, ProfileCredentials.Token("dos")))
        assertEquals(ProfileCredentials.Token("dos"), assertIs<VaultState.Ready>(vault().load()).secrets.credentials)
        assertEquals(listOf("sync.bin"), dir.list()!!.toList())
    }

    @Test fun clearBorraElFichero() {
        vault().save(SyncSecrets(key, ProfileCredentials.Token("x")))
        vault().clear()
        assertFalse(file.exists())
        assertIs<VaultState.Empty>(vault().load())
    }

    @Test fun clearSinFicheroNoFalla() {
        vault().clear()
        assertIs<VaultState.Empty>(vault().load())
    }

    @Test fun siElKeystorePierdeLaClaveEsIlegibleYNoSeBorraElFichero() {
        vault().save(SyncSecrets(key, ProfileCredentials.Token("x")))
        wrapper.present = false
        assertIs<VaultState.Unreadable>(vault().load())
        // Nunca se borra solo: quien decide es el usuario (volver a pegar el código).
        assertTrue(file.exists())
    }

    @Test fun unEnvoltorioQueNoAbreEsIlegible() {
        vault().save(SyncSecrets(key, ProfileCredentials.Token("x")))
        wrapper.broken = true
        assertIs<VaultState.Unreadable>(vault().load())
    }

    @Test fun unFormatoDesconocidoOUnFicheroVacioEsIlegible() {
        file.writeBytes(byteArrayOf(9, 1, 2, 3))
        assertIs<VaultState.Unreadable>(vault().load())
        file.writeBytes(ByteArray(0))
        assertIs<VaultState.Unreadable>(vault().load())
    }

    @Test fun unContenidoQueNoEsJsonValidoOConClaveCortaEsIlegible() {
        // Un envoltorio que abre pero con basura dentro: no puede explotar con una excepción rara.
        file.writeBytes(byteArrayOf(SecretVault.FORMAT_VERSION) + wrapper.wrap("no es json".toByteArray()))
        assertIs<VaultState.Unreadable>(vault().load())
        val shortKey = """{"key":"AAAA","kind":"token","token":"x"}""".toByteArray()
        file.writeBytes(byteArrayOf(SecretVault.FORMAT_VERSION) + wrapper.wrap(shortKey))
        assertIs<VaultState.Unreadable>(vault().load())
    }

    @Test fun guardarCreaLaClaveDeEnvolturaSiFalta() {
        wrapper.present = false
        vault().save(SyncSecrets(key, ProfileCredentials.Token("x")))
        assertTrue(wrapper.present)
        assertIs<VaultState.Ready>(vault().load())
    }

    @Test fun unaClaveQueNoTiene32BytesNoSeGuarda() {
        val error = runCatching { vault().save(SyncSecrets(ByteArray(16), ProfileCredentials.Token("x"))) }.exceptionOrNull()
        assertIs<IllegalArgumentException>(error)
        assertFalse(file.exists())
    }

    @Test fun lasCredencialesVaciasNoSeGuardan() {
        assertIs<IllegalArgumentException>(runCatching { vault().save(SyncSecrets(key, ProfileCredentials.Token(" "))) }.exceptionOrNull())
        assertIs<IllegalArgumentException>(runCatching { vault().save(SyncSecrets(key, ProfileCredentials.Basic("u", ""))) }.exceptionOrNull())
    }
}
