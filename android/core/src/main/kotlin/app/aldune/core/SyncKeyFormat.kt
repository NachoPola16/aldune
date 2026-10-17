package app.aldune.core

import java.util.Base64

/**
 * Clave de sincronización: 32 bytes en base64url sin relleno. Al leer se acepta con o sin relleno
 * y también con el alfabeto estándar (Aldune.Core normaliza '-'→'+' y '_'→'/' antes de decodificar).
 */
object SyncKeyFormat {
    const val KEY_SIZE = 32

    fun encode(key: ByteArray): String {
        require(key.size == KEY_SIZE) { "A sync key must contain 32 bytes." }
        return Base64.getUrlEncoder().withoutPadding().encodeToString(key)
    }

    fun decode(value: String): ByteArray {
        val normalized = value.trim().replace('-', '+').replace('_', '/')
        // IllegalArgumentException si no es base64; el mismo tipo que la longitud incorrecta.
        val key = Base64.getDecoder().decode(normalized)
        require(key.size == KEY_SIZE) { "A sync key must contain 32 bytes." }
        return key
    }
}
