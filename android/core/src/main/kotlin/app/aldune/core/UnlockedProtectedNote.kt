package app.aldune.core

import java.util.Arrays
import java.util.Base64

/**
 * Una nota protegida ya abierta: guarda la clave derivada y la sal para volver a cifrar sin pagar otra vez las 600.000 iteraciones de
 * PBKDF2 en cada autoguardado (en una tableta lenta, más de un segundo por pulsación).
 *
 * Es una sesión, no un dato: [close] pone la clave a cero y desde entonces todo lanza `IllegalStateException`. Quien la abre la cierra al
 * salir de la nota y al pasar la app a segundo plano; esta clase no recuerda la contraseña (no la recibe).
 */
class UnlockedProtectedNote internal constructor(
    private var key: ByteArray?,
    private val salt: ByteArray,
    text: String,
) : AutoCloseable {
    private var plainText: String? = text

    /**
     * El texto tal como se descifró al abrir; [reprotect] no lo cambia (quien edita tiene su propio texto, y así quien guarda puede
     * saber con qué saltos de línea llegó). Tras [close] lanza: el texto en claro tampoco debe sobrevivir a la sesión.
     */
    val text: String
        @Synchronized get() = plainText ?: throw closed()

    /** Contenido nuevo con la misma sal y un nonce nuevo, como `Reprotect` de Core. No deriva la clave. */
    @Synchronized
    fun reprotect(newText: String): ProtectedNoteContent {
        val current = key ?: throw closed()
        val encrypted = ContentCipher.encrypt(current, newText)
        val encoder = Base64.getEncoder()
        return ProtectedNoteContent(
            salt = encoder.encodeToString(salt),
            cipherText = encoder.encodeToString(encrypted.cipherText),
            nonce = encoder.encodeToString(encrypted.nonce),
            tag = encoder.encodeToString(encrypted.tag),
        )
    }

    /** Idempotente: cerrar dos veces (salir de la nota y luego `ON_STOP`) no es un error. */
    @Synchronized
    override fun close() {
        key?.let { Arrays.fill(it, 0) }
        key = null
        plainText = null
    }

    private fun closed() = IllegalStateException("La sesión de la nota protegida está cerrada")
}
