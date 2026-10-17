package app.aldune.domain

import java.security.SecureRandom
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

/**
 * Cifrado local del texto de las notas: AES-256-GCM, nonce de 12 bytes aleatorio en cada cifrado, tag de 16 bytes y el id de la nota
 * como dato asociado (AAD), de modo que una fila copiada a otro id no descifra. Es cifrado en reposo, independiente del de sync.
 *
 * Solo usa `javax.crypto`, así que se prueba en la JVM; la clave de datos la custodia `app` (envuelta por el Keystore).
 */
class AesGcmNoteCipher(key: ByteArray, private val random: SecureRandom = SecureRandom()) : NoteCipher {
    private val secretKey: SecretKeySpec

    init {
        require(key.size == KEY_BYTES) { "La clave de datos debe ser de $KEY_BYTES bytes" }
        // SecretKeySpec copia la clave: quien la pasó puede borrar su array sin romper este objeto.
        secretKey = SecretKeySpec(key, "AES")
    }

    override fun encrypt(noteId: String, plainText: String): EncryptedText {
        val nonce = ByteArray(NONCE_BYTES).also(random::nextBytes)
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.ENCRYPT_MODE, secretKey, GCMParameterSpec(TAG_BITS, nonce))
        cipher.updateAAD(noteId.toByteArray(Charsets.UTF_8))
        // Java devuelve el texto cifrado seguido del tag; en la base van en columnas aparte, como en Windows.
        val sealed = cipher.doFinal(plainText.toByteArray(Charsets.UTF_8))
        val cut = sealed.size - TAG_BYTES
        return EncryptedText(sealed.copyOfRange(0, cut), nonce, sealed.copyOfRange(cut, sealed.size))
    }

    override fun decrypt(noteId: String, encrypted: EncryptedText): String {
        val cipher = Cipher.getInstance(TRANSFORMATION)
        cipher.init(Cipher.DECRYPT_MODE, secretKey, GCMParameterSpec(TAG_BITS, encrypted.nonce))
        cipher.updateAAD(noteId.toByteArray(Charsets.UTF_8))
        return String(cipher.doFinal(encrypted.cipherText + encrypted.tag), Charsets.UTF_8)
    }

    private companion object {
        const val KEY_BYTES = 32
        const val NONCE_BYTES = 12
        const val TAG_BYTES = 16
        const val TAG_BITS = TAG_BYTES * 8
        const val TRANSFORMATION = "AES/GCM/NoPadding"
    }
}
