package app.aldune.data

import android.content.Context
import android.util.Log
import androidx.test.core.app.ApplicationProvider
import androidx.test.ext.junit.runners.AndroidJUnit4
import app.aldune.domain.AesGcmNoteCipher
import app.aldune.domain.DataKeyManager
import app.aldune.domain.DataKeyState
import app.aldune.domain.KeyMissingException
import app.aldune.domain.LostReason
import java.io.File
import java.security.GeneralSecurityException
import java.security.KeyStore
import java.util.UUID
import javax.crypto.SecretKey
import org.junit.After
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Test
import org.junit.runner.RunWith

/**
 * El Keystore real del dispositivo. Cada prueba usa un alias y una carpeta propios y los borra al acabar: nunca toca la clave de la app.
 */
@RunWith(AndroidJUnit4::class)
class KeystoreTest {
    private val context = ApplicationProvider.getApplicationContext<Context>()
    private val aliases = mutableListOf<String>()
    private val dir = File(context.cacheDir, "keystore-test-${UUID.randomUUID()}").apply { mkdirs() }

    private fun wrapper(alias: String = "aldune.test.${UUID.randomUUID()}") = AndroidKeystoreKeyWrapper(alias).also { aliases += alias }

    @After fun cleanUp() {
        aliases.forEach { AndroidKeystoreKeyWrapper(it).delete() }
        dir.deleteRecursively()
    }

    @Test fun wrapAndUnwrapRoundTrip() {
        val w = wrapper().apply { ensureKey() }
        val plain = ByteArray(32) { (it * 7).toByte() }
        val blob = w.wrap(plain)
        assertEquals(12 + 32 + 16, blob.size)
        assertArrayEquals(plain, w.unwrap(blob))
        // Nonce nuevo en cada envoltura.
        assertFalse(blob.contentEquals(w.wrap(plain)))
    }

    @Test fun theKeyIsNotExportableAndLivesInHardware() {
        val alias = "aldune.test.${UUID.randomUUID()}"
        val w = wrapper(alias).apply { ensureKey() }
        val key = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }.getKey(alias, null) as SecretKey
        assertNull("La clave del Keystore no debe poder leerse", key.encoded)
        val hardware = w.isHardwareBacked()
        Log.i("KeystoreTest", "Clave en hardware seguro: $hardware")
        assertTrue("En un Pixel la clave debe estar en hardware seguro", hardware)
    }

    @Test fun ensureKeyIsIdempotent() {
        val w = wrapper().apply { ensureKey() }
        val blob = w.wrap(byteArrayOf(1, 2, 3))
        w.ensureKey()
        assertArrayEquals(byteArrayOf(1, 2, 3), w.unwrap(blob))
    }

    @Test fun missingKeyIsReportedAsMissing() {
        val w = wrapper().apply { ensureKey() }
        val blob = w.wrap(byteArrayOf(1))
        w.delete()
        assertFalse(w.hasKey())
        try {
            w.unwrap(blob)
            fail()
        } catch (expected: KeyMissingException) {
        }
    }

    @Test fun anotherKeyCannotUnwrap() {
        val blob = wrapper().apply { ensureKey() }.wrap(byteArrayOf(1, 2, 3))
        val other = wrapper().apply { ensureKey() }
        try {
            other.unwrap(blob)
            fail()
        } catch (expected: GeneralSecurityException) {
            assertFalse(expected is KeyMissingException)
        }
    }

    @Test fun dataKeyManagerEndToEndOnTheDevice() {
        val alias = "aldune.test.${UUID.randomUUID()}"
        val file = File(dir, "datakey.bin")
        val created = DataKeyManager(file, wrapper(alias)).open(hasExistingData = false) as DataKeyState.Ready
        assertTrue(created.created)

        // Lo que se cifró con la clave de datos se lee tras «reiniciar» (objetos nuevos, misma clave del Keystore y mismo fichero).
        val sealed = AesGcmNoteCipher(created.key).encrypt("n1", "nota")
        val reopened = DataKeyManager(file, AndroidKeystoreKeyWrapper(alias)).open(hasExistingData = true) as DataKeyState.Ready
        assertEquals("nota", AesGcmNoteCipher(reopened.key).decrypt("n1", sealed))

        // Si el sistema pierde la clave del Keystore: clave perdida, fichero intacto.
        val before = file.readBytes()
        AndroidKeystoreKeyWrapper(alias).delete()
        assertEquals(DataKeyState.Lost(LostReason.KEYSTORE_KEY_MISSING), DataKeyManager(file, AndroidKeystoreKeyWrapper(alias)).open(hasExistingData = true))
        assertArrayEquals(before, file.readBytes())

        // Empezar de cero vuelve a funcionar.
        var deleted = false
        val fresh = DataKeyManager(file, AndroidKeystoreKeyWrapper(alias)).startOver { deleted = true }
        assertTrue(deleted)
        assertTrue(fresh.created)
    }

    @Test fun unwrappingIsFastEnoughForStartup() {
        val w = wrapper().apply { ensureKey() }
        val blob = w.wrap(ByteArray(32))
        val start = System.nanoTime()
        repeat(5) { w.unwrap(blob) }
        val perCallMs = (System.nanoTime() - start) / 5 / 1_000_000
        Log.i("KeystoreTest", "Desenvolver la clave de datos: $perCallMs ms")
        assertTrue("Desenvolver no debe tardar más de 500 ms ($perCallMs ms)", perCallMs < 500)
    }
}
