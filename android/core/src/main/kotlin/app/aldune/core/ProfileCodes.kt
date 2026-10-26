package app.aldune.core

import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonElement
import kotlinx.serialization.json.JsonNull
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.net.URI
import java.net.URISyntaxException
import java.util.Base64
import java.util.Locale
import java.util.UUID

/** Datos de un código de perfil. `scope` y `transport` son los números de Core (ver [ProfileCodes]). */
data class SyncShareCodeData(
    val key: ByteArray,
    val scope: Int,
    val noteIds: List<UUID>,
    val profileName: String? = null,
    val transport: Int? = null,
    val serverUrl: String? = null,
    val tag: String? = null,
) {
    // ByteArray en un data class: igualdad por contenido.
    override fun equals(other: Any?): Boolean = other is SyncShareCodeData && key.contentEquals(other.key) &&
        scope == other.scope && noteIds == other.noteIds && profileName == other.profileName &&
        transport == other.transport && serverUrl == other.serverUrl && tag == other.tag

    override fun hashCode(): Int = listOf(key.contentHashCode(), scope, noteIds, profileName, transport, serverUrl, tag).hashCode()
}

/**
 * Código opcional para compartir un perfil (SyncShareCodeCodec de Core): prefijo `aldune-profile-v2:`
 * y base64url sin relleno de un JSON UTF-8 con claves en **PascalCase**. Nunca lleva el token ni
 * el contenido de las notas.
 */
object ProfileCodes {
    // SyncScopeKind
    const val SCOPE_ALL = 0
    const val SCOPE_CHOSEN = 1
    const val SCOPE_TAG = 2

    // SyncTransportKind
    const val TRANSPORT_FOLDER = 0
    const val TRANSPORT_SERVER = 1
    const val TRANSPORT_WEBDAV = 2

    private const val PREFIX = "aldune-profile-v2:"
    private const val CURRENT_DOCUMENT_VERSION = 2
    private const val MAX_NOTE_IDS = 10_000
    private const val MAX_PROFILE_NAME_LENGTH = 120
    private const val MAX_SERVER_URL_LENGTH = 2_048

    fun isShareCode(value: String): Boolean = splitPrefix(value.trim()) != null

    private val SHARE_CODE_IN_TEXT = Regex(Regex.escape(PREFIX) + "[A-Za-z0-9_-]+", RegexOption.IGNORE_CASE)

    /**
     * El código de perfil que lleva un texto (lo normal es que llegue solo, pero un mensaje de chat trae más), o `null` si no hay ninguno.
     * Solo reconoce la forma; si el contenido no vale, lo dirá `decode` al continuar.
     */
    fun findShareCode(text: String?): String? = text?.let { SHARE_CODE_IN_TEXT.find(it)?.value }

    /** Separa prefijo y resto. */
    private fun splitPrefix(value: String): Pair<String, Int>? {
        if (value.startsWith(PREFIX, ignoreCase = true)) return value.substring(PREFIX.length) to CURRENT_DOCUMENT_VERSION
        return null
    }

    fun encode(
        key: ByteArray,
        scope: Int,
        noteIds: Iterable<UUID>,
        profileName: String? = null,
        transport: Int? = null,
        serverUrl: String? = null,
        tag: String? = null,
    ): String {
        val document = JsonObject(linkedMapOf(
            "Version" to JsonPrimitive(CURRENT_DOCUMENT_VERSION),
            "Key" to JsonPrimitive(SyncKeyFormat.encode(key)),
            "Scope" to JsonPrimitive(scope),
            "NoteIds" to JsonArray(noteIds.distinct().take(MAX_NOTE_IDS).map { JsonPrimitive(it.toString()) }),
            "Tag" to (tag?.let(::JsonPrimitive) ?: JsonNull),
            "ProfileName" to (normalizeProfileName(profileName)?.let(::JsonPrimitive) ?: JsonNull),
            "Transport" to (transport?.let(::JsonPrimitive) ?: JsonNull),
            "ServerUrl" to (normalizeServerUrl(serverUrl)?.let(::JsonPrimitive) ?: JsonNull),
        ))
        return PREFIX + Base64.getUrlEncoder().withoutPadding().encodeToString(document.toString().toByteArray(Charsets.UTF_8))
    }

    fun decode(value: String): SyncShareCodeData {
        val (payload, documentVersion) = splitPrefix(value.trim())
            ?: throw SyncFormatException("That is not an Aldune profile code.")

        val document = try {
            Json.parseToJsonElement(String(fromBase64Url(payload), Charsets.UTF_8)) as? JsonObject
                ?: throw SyncFormatException("The profile code is empty.")
        } catch (e: SyncFormatException) {
            throw e
        } catch (e: Exception) { // JSON roto o base64 roto
            throw SyncFormatException("The profile code is not valid.", e)
        }

        // Como Deserialize<T> con opciones por defecto en Core, los nombres distinguen mayúsculas.
        val version = document.number("Version") ?: 0
        val scope = document.number("Scope") ?: 0
        val keyText = document.text("Key")
        if (version != documentVersion || scope !in SCOPE_ALL..SCOPE_TAG || keyText.isNullOrBlank())
            throw SyncFormatException("The profile code version is not supported.")

        val noteIds = readNoteIds(document["NoteIds"])
        if (noteIds.size > MAX_NOTE_IDS) throw SyncFormatException("The profile code contains too many notes.")

        val profileName = document.text("ProfileName")
        val rawServerUrl = document.text("ServerUrl")
        val transport = document.number("Transport")
        if ((profileName != null && profileName.length > MAX_PROFILE_NAME_LENGTH) ||
            (rawServerUrl != null && rawServerUrl.length > MAX_SERVER_URL_LENGTH) ||
            (transport != null && transport !in TRANSPORT_FOLDER..TRANSPORT_WEBDAV))
            throw SyncFormatException("The profile code contains invalid connection details.")

        val serverUrl = normalizeServerUrl(rawServerUrl)
        if ((transport == TRANSPORT_SERVER || transport == TRANSPORT_WEBDAV) && serverUrl == null)
            throw SyncFormatException("The profile code does not contain a valid server URL.")

        val key = try {
            SyncKeyFormat.decode(keyText)
        } catch (e: IllegalArgumentException) {
            throw SyncFormatException(e.message ?: "The profile code key is not valid.", e)
        }
        return SyncShareCodeData(key, scope, noteIds.distinct(), normalizeProfileName(profileName), transport, serverUrl, document.text("Tag"))
    }

    private fun JsonObject.text(name: String): String? =
        (this[name] as? JsonPrimitive)?.takeIf { it.isString }?.content

    private fun JsonObject.number(name: String): Int? =
        (this[name] as? JsonPrimitive)?.takeIf { !it.isString && it !is JsonNull }?.content?.toIntOrNull()

    private fun readNoteIds(element: JsonElement?): List<UUID> {
        if (element == null || element is JsonNull) return emptyList()
        val array = element as? JsonArray ?: throw SyncFormatException("The profile code is not valid.")
        return array.map {
            val text = (it as? JsonPrimitive)?.takeIf { p -> p.isString }?.content
                ?: throw SyncFormatException("The profile code is not valid.")
            SyncEnvelopeCodec.parseUuid(text)
        }
    }

    private fun fromBase64Url(value: String): ByteArray =
        Base64.getDecoder().decode(value.replace('-', '+').replace('_', '/'))

    private fun normalizeProfileName(value: String?): String? {
        val normalized = value?.trim()
        return if (normalized.isNullOrBlank()) null else normalized.take(MAX_PROFILE_NAME_LENGTH)
    }

    /**
     * Solo http/https absolutas. Core usa `Uri.ToString()`, que pone en minúsculas esquema y servidor,
     * añade "/" a una ruta vacía y quita el puerto por defecto; aquí se hace lo mismo para las URL normales.
     */
    private fun normalizeServerUrl(value: String?): String? {
        val trimmed = value?.trim()
        if (trimmed.isNullOrBlank()) return null
        if (trimmed.length > MAX_SERVER_URL_LENGTH) throw SyncFormatException("The profile code contains an invalid server URL.")
        val uri = try {
            URI(trimmed)
        } catch (e: URISyntaxException) {
            throw SyncFormatException("The profile code contains an invalid server URL.", e)
        }
        val scheme = uri.scheme?.lowercase(Locale.ROOT)
        if ((scheme != "http" && scheme != "https") || uri.host.isNullOrEmpty())
            throw SyncFormatException("The profile code contains an invalid server URL.")
        val defaultPort = if (scheme == "https") 443 else 80
        val port = if (uri.port == defaultPort) -1 else uri.port
        val path = uri.rawPath.takeUnless { it.isNullOrEmpty() } ?: "/"
        return buildString {
            append(scheme).append("://")
            uri.rawUserInfo?.let { append(it).append('@') }
            append(uri.host.lowercase(Locale.ROOT))
            if (port != -1) append(':').append(port)
            append(path)
            uri.rawQuery?.let { append('?').append(it) }
            uri.rawFragment?.let { append('#').append(it) }
        }
    }
}
