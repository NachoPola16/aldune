package app.aldune.syncclient

import app.aldune.domain.KeyWrapper
import java.io.File
import java.io.FileOutputStream
import java.io.IOException
import java.nio.file.Files
import java.nio.file.StandardCopyOption
import java.security.GeneralSecurityException
import java.util.Base64
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.contentOrNull
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive

/** Lo que nunca puede ir en claro: la clave de sync del perfil y la credencial del transporte. */
class SyncSecrets(val key: ByteArray, val credentials: ProfileCredentials) {
    override fun equals(other: Any?): Boolean = other is SyncSecrets && key.contentEquals(other.key) && credentials == other.credentials

    override fun hashCode(): Int = 31 * key.contentHashCode() + credentials.hashCode()
}

sealed interface VaultState {
    /** No hay perfil guardado (nunca se conectó, o se desconectó). */
    data object Empty : VaultState

    class Ready(val secrets: SyncSecrets) : VaultState

    /**
     * Hay fichero pero no se puede abrir: el Keystore perdió la clave, el envoltorio no abre o el contenido no es de este formato.
     * No se borra nada por su cuenta; la salida es volver a pegar el código de perfil (la copia del sistema no guarda estos secretos).
     */
    data object Unreadable : VaultState
}

/**
 * Los secretos de la sync en un fichero envuelto por el Keystore (spec F5 §5.2), con el mismo modelo que la clave de datos
 * (`DataKeyManager`): un byte de versión + el blob del [KeyWrapper], escrito a un temporal y renombrado para que un corte a mitad no
 * deje un fichero a medias. `EncryptedSharedPreferences` está obsoleta y no se usa (spec madre §4.2).
 *
 * Usa su **propia** clave de envoltura (otro alias que la de las notas), para que «empezar de cero» con las notas no borre estos
 * secretos ni al revés.
 */
class SecretVault(private val file: File, private val wrapper: KeyWrapper) {
    fun load(): VaultState {
        if (!file.exists()) return VaultState.Empty
        val bytes = try {
            file.readBytes()
        } catch (e: IOException) {
            return VaultState.Unreadable
        }
        if (bytes.isEmpty() || bytes[0] != FORMAT_VERSION) return VaultState.Unreadable
        val json = try {
            wrapper.unwrap(bytes.copyOfRange(1, bytes.size))
        } catch (e: GeneralSecurityException) { // incluye KeyMissingException
            return VaultState.Unreadable
        }
        return try {
            VaultState.Ready(parse(json))
        } catch (e: Exception) {
            VaultState.Unreadable
        } finally {
            json.fill(0)
        }
    }

    fun save(secrets: SyncSecrets) {
        require(secrets.key.size == KEY_BYTES) { "La clave de sync tiene que ser de $KEY_BYTES bytes" }
        when (val c = secrets.credentials) {
            is ProfileCredentials.Token -> require(c.token.isNotBlank()) { "Falta el token" }
            is ProfileCredentials.Basic -> require(c.username.isNotBlank() && c.password.isNotEmpty()) { "Faltan usuario o contraseña" }
        }
        wrapper.ensureKey()
        val plain = serialize(secrets)
        try {
            writeAtomically(byteArrayOf(FORMAT_VERSION) + wrapper.wrap(plain))
        } finally {
            plain.fill(0)
        }
    }

    /** Desconectar: borra el fichero. La clave de envoltura se queda (se reutiliza al volver a conectar). */
    fun clear() {
        file.delete()
    }

    private fun serialize(secrets: SyncSecrets): ByteArray {
        val fields = linkedMapOf<String, kotlinx.serialization.json.JsonElement>(
            "key" to JsonPrimitive(Base64.getEncoder().encodeToString(secrets.key)),
        )
        when (val c = secrets.credentials) {
            is ProfileCredentials.Token -> {
                fields["kind"] = JsonPrimitive("token")
                fields["token"] = JsonPrimitive(c.token)
            }
            is ProfileCredentials.Basic -> {
                fields["kind"] = JsonPrimitive("basic")
                fields["username"] = JsonPrimitive(c.username)
                fields["password"] = JsonPrimitive(c.password)
            }
        }
        return JsonObject(fields).toString().toByteArray(Charsets.UTF_8)
    }

    private fun parse(bytes: ByteArray): SyncSecrets {
        val o = Json.parseToJsonElement(String(bytes, Charsets.UTF_8)).jsonObject
        val key = Base64.getDecoder().decode(o.getValue("key").jsonPrimitive.content)
        require(key.size == KEY_BYTES)
        val credentials = when (o.getValue("kind").jsonPrimitive.content) {
            "token" -> ProfileCredentials.Token(o.getValue("token").jsonPrimitive.content)
            "basic" -> ProfileCredentials.Basic(o.getValue("username").jsonPrimitive.content, o.getValue("password").jsonPrimitive.contentOrNull!!)
            else -> throw IllegalArgumentException("Tipo de credencial desconocido")
        }
        return SyncSecrets(key, credentials)
    }

    private fun writeAtomically(bytes: ByteArray) {
        val temp = File(file.parentFile, file.name + ".tmp")
        FileOutputStream(temp).use { out ->
            out.write(bytes)
            out.fd.sync()
        }
        try {
            Files.move(temp.toPath(), file.toPath(), StandardCopyOption.REPLACE_EXISTING, StandardCopyOption.ATOMIC_MOVE)
        } catch (e: IOException) {
            temp.delete()
            throw e
        }
    }

    companion object {
        const val FORMAT_VERSION: Byte = 1
        private const val KEY_BYTES = 32
    }
}
