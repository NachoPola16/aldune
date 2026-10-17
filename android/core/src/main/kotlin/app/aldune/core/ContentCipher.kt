package app.aldune.core

import java.security.SecureRandom
import java.util.Arrays
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

data class EncryptedContent(val cipherText: ByteArray, val nonce: ByteArray, val tag: ByteArray) {
    override fun equals(other: Any?): Boolean = other is EncryptedContent &&
        cipherText.contentEquals(other.cipherText) && nonce.contentEquals(other.nonce) && tag.contentEquals(other.tag)

    override fun hashCode(): Int =
        31 * (31 * Arrays.hashCode(cipherText) + Arrays.hashCode(nonce)) + Arrays.hashCode(tag)
}

/**
 * AES-256-GCM como lo hace ContentCipher de Core: nonce de 12 bytes, etiqueta de 16, sin datos
 * asociados. Java devuelve cifrado‖etiqueta juntos; aquí se separan para que el sobre los lleve
 * en campos distintos.
 */
object ContentCipher {
    const val KEY_SIZE = 32
    const val NONCE_SIZE = 12
    const val TAG_SIZE = 16

    private val random = SecureRandom()

    /**
     * Cifra el texto (UTF-8) con nonce aleatorio nuevo. El parámetro [nonce] existe para los tests
     * con vectores (AES-GCM es determinista con nonce fijo); en producción no se pasa nunca, porque
     * repetir un nonce con la misma clave rompe GCM.
     */
    fun encrypt(key: ByteArray, plainText: String, nonce: ByteArray = ByteArray(NONCE_SIZE).also(random::nextBytes)): EncryptedContent {
        require(key.size == KEY_SIZE) { "Key must be $KEY_SIZE bytes (AES-256)." }
        require(nonce.size == NONCE_SIZE) { "The nonce must be $NONCE_SIZE bytes." }
        val plain = plainText.toByteArray(Charsets.UTF_8)
        try {
            val cipher = Cipher.getInstance("AES/GCM/NoPadding")
            cipher.init(Cipher.ENCRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(TAG_SIZE * 8, nonce))
            val joined = cipher.doFinal(plain)
            val split = joined.size - TAG_SIZE
            return EncryptedContent(joined.copyOfRange(0, split), nonce.copyOf(), joined.copyOfRange(split, joined.size))
        } finally {
            Arrays.fill(plain, 0)
        }
    }

    /** @throws java.security.GeneralSecurityException (AEADBadTagException) si la clave o los datos no son los que cifraron. */
    fun decrypt(key: ByteArray, encrypted: EncryptedContent): String {
        require(key.size == KEY_SIZE) { "Key must be $KEY_SIZE bytes (AES-256)." }
        require(encrypted.nonce.size == NONCE_SIZE) { "The nonce must be $NONCE_SIZE bytes." }
        require(encrypted.tag.size == TAG_SIZE) { "The tag must be $TAG_SIZE bytes." }
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, SecretKeySpec(key, "AES"), GCMParameterSpec(TAG_SIZE * 8, encrypted.nonce))
        val plain = cipher.doFinal(encrypted.cipherText + encrypted.tag)
        try {
            return String(plain, Charsets.UTF_8)
        } finally {
            Arrays.fill(plain, 0)
        }
    }
}
