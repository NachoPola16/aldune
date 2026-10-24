package app.aldune.core

import java.security.GeneralSecurityException
import java.security.SecureRandom
import java.util.Arrays
import java.util.Base64
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.PBEKeySpec

/**
 * Cifrado adicional de una nota protegida (ProtectedNoteContent de Core). La clave sale de la
 * contraseña con PBKDF2-HMAC-SHA256 (600.000 iteraciones, sal de 16 bytes, 32 de salida) y el texto
 * UTF-8 se cifra con AES-GCM **directamente**: aquí no hay capa base64 (a diferencia del sobre).
 */
object ProtectedNotes {
    // El número de iteraciones NO viaja en el dato: es una constante de compatibilidad con Core.
    const val PBKDF2_ITERATIONS = 600_000
    const val SALT_SIZE = 16
    const val MIN_PASSWORD_LENGTH = 4

    private val random = SecureRandom()

    fun protect(plainText: String, password: String): ProtectedNoteContent {
        validatePassword(password)
        return encrypt(plainText, password, ByteArray(SALT_SIZE).also(random::nextBytes))
    }

    /** Vuelve a cifrar con la misma sal (y nonce nuevo), como `Reprotect` de Core. */
    fun reprotect(current: ProtectedNoteContent, plainText: String, password: String): ProtectedNoteContent {
        validatePassword(password)
        val salt = try {
            Base64.getDecoder().decode(current.salt)
        } catch (e: IllegalArgumentException) {
            throw SyncFormatException("The protected note salt is invalid.", e)
        }
        if (salt.size != SALT_SIZE) throw SyncFormatException("The protected note salt is invalid.")
        return encrypt(plainText, password, salt)
    }

    /** El texto, o `null` si la contraseña no es la de la nota o el contenido está dañado. */
    fun tryUnprotect(content: ProtectedNoteContent, password: String): String? {
        if (content.version != ProtectedNoteContent.CURRENT_VERSION || password.isBlank()) return null
        return try {
            val decoder = Base64.getDecoder()
            val salt = decoder.decode(content.salt)
            val encrypted = EncryptedContent(decoder.decode(content.cipherText), decoder.decode(content.nonce), decoder.decode(content.tag))
            if (salt.size != SALT_SIZE || encrypted.nonce.size != ContentCipher.NONCE_SIZE ||
                encrypted.tag.size != ContentCipher.TAG_SIZE) return null
            val key = deriveKey(password, salt)
            try {
                ContentCipher.decrypt(key, encrypted)
            } finally {
                Arrays.fill(key, 0)
            }
        } catch (e: IllegalArgumentException) {
            null
        } catch (e: GeneralSecurityException) {
            null
        }
    }

    /**
     * Abre la nota y conserva la clave derivada para editarla sin derivar otra vez. `null` en los mismos casos que [tryUnprotect]
     * (contraseña que no es, vacía, versión desconocida, datos dañados).
     */
    fun unlock(content: ProtectedNoteContent, password: String): UnlockedProtectedNote? {
        if (content.version != ProtectedNoteContent.CURRENT_VERSION || password.isBlank()) return null
        return try {
            val decoder = Base64.getDecoder()
            val salt = decoder.decode(content.salt)
            val encrypted = EncryptedContent(decoder.decode(content.cipherText), decoder.decode(content.nonce), decoder.decode(content.tag))
            if (salt.size != SALT_SIZE || encrypted.nonce.size != ContentCipher.NONCE_SIZE ||
                encrypted.tag.size != ContentCipher.TAG_SIZE) return null
            val key = deriveKey(password, salt)
            var opened = false
            try {
                UnlockedProtectedNote(key, salt, ContentCipher.decrypt(key, encrypted)).also { opened = true }
            } catch (e: GeneralSecurityException) {
                null
            } finally {
                // Contraseña equivocada (o cualquier otro fallo al descifrar): la clave no sirve y no debe quedarse en memoria.
                if (!opened) Arrays.fill(key, 0)
            }
        } catch (e: IllegalArgumentException) {
            null
        }
    }

    private fun encrypt(plainText: String, password: String, salt: ByteArray): ProtectedNoteContent {
        val key = deriveKey(password, salt)
        try {
            val encrypted = ContentCipher.encrypt(key, plainText)
            val encoder = Base64.getEncoder()
            return ProtectedNoteContent(
                salt = encoder.encodeToString(salt),
                cipherText = encoder.encodeToString(encrypted.cipherText),
                nonce = encoder.encodeToString(encrypted.nonce),
                tag = encoder.encodeToString(encrypted.tag),
            )
        } finally {
            Arrays.fill(key, 0)
        }
    }

    // PBEKeySpec toma char[] y PBKDF2WithHmacSHA256 lo codifica en UTF-8 (también en Android 8+),
    // que es lo que hace Core con Encoding.UTF8.GetBytes(password).
    private fun deriveKey(password: String, salt: ByteArray): ByteArray {
        val spec = PBEKeySpec(password.toCharArray(), salt, PBKDF2_ITERATIONS, ContentCipher.KEY_SIZE * 8)
        try {
            return SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256").generateSecret(spec).encoded
        } finally {
            spec.clearPassword()
        }
    }

    private fun validatePassword(password: String) {
        require(password.isNotBlank() && password.length >= MIN_PASSWORD_LENGTH) {
            "A protected note password must contain at least four characters."
        }
    }
}
