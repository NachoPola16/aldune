package app.aldune.sync

import java.io.IOException
import java.util.Base64
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody

/** Lo que lleva una invitación: la dirección del servidor y el código de un solo uso. Nunca una clave ni un token. */
data class Invitation(val serverUrl: String, val code: String)

enum class InviteFailure { OFFLINE, INVALID_CODE, NOT_SUPPORTED, SERVER_FULL, TOO_MANY_ATTEMPTS, SERVER }

class InviteException(val failure: InviteFailure) : Exception("Invitation failed: $failure")

/**
 * Las invitaciones al servidor de sync (docs/SYNC.md → «Invitations»), lado invitado: leer el texto que dio quien aloja y canjearlo por el token
 * de un espacio nuevo. Es el mismo texto que escribe `SyncInvite` de Core (`aldune-invite:` + JSON `{"U","C"}` en base64url): la
 * prueba lleva un valor generado por C#. La clave de sync **no** forma parte de esto: la genera el dispositivo.
 */
object Invitations {
    const val PREFIX = "aldune-invite:"
    private const val MAX_LENGTH = 4_096

    /** Nulo si no es una invitación bien formada (un chat puede haber añadido espacios o saltos de línea: se ignoran). */
    fun parse(text: String?): Invitation? {
        val clean = (text ?: return null).filterNot { it.isWhitespace() }
        if (clean.isEmpty() || clean.length > MAX_LENGTH || !clean.startsWith(PREFIX, ignoreCase = true)) return null
        return try {
            val payload = clean.substring(PREFIX.length)
            val json = Json.parseToJsonElement(String(Base64.getUrlDecoder().decode(payload), Charsets.UTF_8)).jsonObject
            val url = json["U"]?.jsonPrimitive?.content?.trim().orEmpty()
            val code = json["C"]?.jsonPrimitive?.content?.trim().orEmpty()
            // Solo http(s): una invitación nunca apunta a otro esquema.
            if (url.isEmpty() || code.isEmpty() || url.toHttpUrlOrNull() == null) null else Invitation(url, code)
        } catch (e: Exception) {
            null
        }
    }

    /** Canjea el código y devuelve el token del espacio nuevo. Bloqueante: se llama desde un hilo de fondo. */
    fun redeem(invitation: Invitation, client: OkHttpClient = OkHttpClient()): String {
        val base = invitation.serverUrl.trim().let { if (it.endsWith("/")) it else "$it/" }.toHttpUrlOrNull()
            ?: throw InviteException(InviteFailure.SERVER)
        val body = JsonObject(mapOf("code" to JsonPrimitive(invitation.code))).toString().toRequestBody("application/json".toMediaType())
        val request = Request.Builder().url(base.resolve("invites/v1/redeem") ?: throw InviteException(InviteFailure.SERVER)).post(body).build()
        try {
            client.newCall(request).execute().use { response ->
                when (response.code) {
                    400 -> throw InviteException(InviteFailure.INVALID_CODE)
                    404 -> throw InviteException(InviteFailure.NOT_SUPPORTED)
                    409 -> throw InviteException(InviteFailure.SERVER_FULL)
                    429 -> throw InviteException(InviteFailure.TOO_MANY_ATTEMPTS)
                }
                if (!response.isSuccessful) throw InviteException(InviteFailure.SERVER)
                val token = Json.parseToJsonElement(response.body?.string().orEmpty()).jsonObject["token"]?.jsonPrimitive?.content
                return token?.takeIf { it.isNotEmpty() } ?: throw InviteException(InviteFailure.SERVER)
            }
        } catch (e: IOException) {
            throw InviteException(InviteFailure.OFFLINE)
        }
    }
}
