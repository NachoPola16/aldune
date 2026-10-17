package app.aldune.domain

/** Texto cifrado con AES-GCM: cada parte va en su columna, como en Windows. */
class EncryptedText(val cipherText: ByteArray, val nonce: ByteArray, val tag: ByteArray)

/**
 * Cifrado local del texto en reposo (no es el de sync). Solo la interfaz: la implementa `app` con la clave de datos del Keystore.
 * El id de la nota va como dato asociado (AAD) para que una fila copiada a otro id no descifre.
 */
interface NoteCipher {
    fun encrypt(noteId: String, plainText: String): EncryptedText

    /** Lanza si la clave, el id o los datos no cuadran; quien lo llama decide cómo mostrarlo. */
    fun decrypt(noteId: String, encrypted: EncryptedText): String
}
