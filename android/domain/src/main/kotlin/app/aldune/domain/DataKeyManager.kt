package app.aldune.domain

import java.io.File
import java.io.FileOutputStream
import java.security.GeneralSecurityException
import java.security.SecureRandom

/**
 * La custodia de la clave del Keystore, abstraída para poder probar [DataKeyManager] en la JVM. La implementa `app` con el Android
 * Keystore; la clave nunca sale de allí, solo envuelve y desenvuelve.
 */
interface KeyWrapper {
    fun hasKey(): Boolean

    /** Crea la clave de envoltura si no existe. */
    fun ensureKey()

    /** Devuelve un blob opaco (nonce, tag y texto cifrado, en el orden que elija la implementación). */
    fun wrap(plain: ByteArray): ByteArray

    /** @throws KeyMissingException si la clave de envoltura no existe. @throws GeneralSecurityException si el blob no abre. */
    fun unwrap(blob: ByteArray): ByteArray

    fun delete()
}

/** La clave de envoltura no existe (borrada por el sistema, restauración en otro móvil, datos de la app borrados a medias). */
class KeyMissingException : GeneralSecurityException("La clave de envoltura no está en el Keystore")

enum class LostReason {
    /** Hay datos pero no el fichero de la clave. */
    KEY_FILE_MISSING,

    /** Está el fichero, pero el Keystore ya no tiene la clave que lo envolvió. */
    KEYSTORE_KEY_MISSING,

    /** El Keystore tiene una clave, pero no abre el fichero (otra clave, fichero dañado). */
    UNWRAP_FAILED,

    /** El fichero no es de un formato que esta versión conozca. */
    UNKNOWN_FORMAT,
}

sealed class DataKeyState {
    /** @property created la clave se acaba de crear (primera ejecución o tras empezar de cero). */
    class Ready(val key: ByteArray, val created: Boolean) : DataKeyState()

    data class Lost(val reason: LostReason) : DataKeyState()
}

/**
 * La clave de datos (32 bytes con los que [AesGcmNoteCipher] cifra el texto de las notas en reposo), guardada en [keyFile] envuelta por
 * la clave del Keystore. Mismo modelo que Windows con DPAPI (spec de Android §4.1 y spec de F4 §2.1).
 *
 * La regla que manda: **si la clave no se puede recuperar, nunca se genera otra en silencio ni se borra nada.** Una clave nueva haría
 * ilegibles las notas que hay sin decirlo; borrar sería perderlas. Se devuelve [DataKeyState.Lost] y es el usuario quien decide
 * [startOver] (la recuperación real es volver a sincronizar). Formato del fichero: un byte de versión ([FORMAT_VERSION]) + el blob del
 * [KeyWrapper].
 */
class DataKeyManager(
    private val keyFile: File,
    private val wrapper: KeyWrapper,
    private val random: SecureRandom = SecureRandom(),
) {
    /**
     * @param hasExistingData si ya hay una base con notas. Sin fichero de clave, decide entre «primera vez» (se crea) y «clave perdida».
     */
    fun open(hasExistingData: Boolean): DataKeyState {
        if (!keyFile.exists()) {
            return if (hasExistingData) DataKeyState.Lost(LostReason.KEY_FILE_MISSING) else create()
        }

        val bytes = keyFile.readBytes()
        if (bytes.isEmpty() || bytes[0] != FORMAT_VERSION) return DataKeyState.Lost(LostReason.UNKNOWN_FORMAT)
        val key = try {
            wrapper.unwrap(bytes.copyOfRange(1, bytes.size))
        } catch (e: KeyMissingException) {
            return DataKeyState.Lost(LostReason.KEYSTORE_KEY_MISSING)
        } catch (e: GeneralSecurityException) {
            return DataKeyState.Lost(LostReason.UNWRAP_FAILED)
        }
        if (key.size != KEY_BYTES) {
            key.fill(0)
            return DataKeyState.Lost(LostReason.UNWRAP_FAILED)
        }
        return DataKeyState.Ready(key, created = false)
    }

    /**
     * «Empezar de cero», solo tras confirmación del usuario: borra los datos (lo hace [deleteData], que conoce la base), después el
     * fichero de la clave y la clave del Keystore, y crea una nueva. El orden importa: si se corta a medias después de borrar los datos,
     * la clave vieja sigue sirviendo para una base vacía; al revés quedarían datos sin clave. Si [deleteData] lanza, no se toca la clave.
     */
    fun startOver(deleteData: () -> Unit): DataKeyState.Ready {
        deleteData()
        keyFile.delete()
        wrapper.delete()
        return create()
    }

    private fun create(): DataKeyState.Ready {
        wrapper.ensureKey()
        val key = ByteArray(KEY_BYTES).also(random::nextBytes)
        writeAtomically(byteArrayOf(FORMAT_VERSION) + wrapper.wrap(key))
        return DataKeyState.Ready(key, created = true)
    }

    // Se escribe a un temporal y se renombra: un corte a mitad nunca deja un fichero a medias que luego se leería como «clave perdida».
    private fun writeAtomically(bytes: ByteArray) {
        val temp = File(keyFile.parentFile, keyFile.name + ".tmp")
        FileOutputStream(temp).use { out ->
            out.write(bytes)
            out.fd.sync()
        }
        try {
            java.nio.file.Files.move(
                temp.toPath(), keyFile.toPath(),
                java.nio.file.StandardCopyOption.REPLACE_EXISTING, java.nio.file.StandardCopyOption.ATOMIC_MOVE,
            )
        } catch (e: java.io.IOException) {
            temp.delete()
            throw e
        }
    }

    companion object {
        const val FORMAT_VERSION: Byte = 1
        private const val KEY_BYTES = 32
    }
}
