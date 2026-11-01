package app.aldune.sync

import java.io.IOException
import java.time.Instant
import java.time.OffsetDateTime
import java.util.Base64
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.jsonArray
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

/** Un espacio creado con una invitación, tal como lo ve quien aloja (la etiqueta solo se le muestra a él). */
data class SpaceInfo(val name: String, val label: String?, val createdAt: Instant)

enum class InviteFailure { OFFLINE, INVALID_CODE, NOT_ALLOWED, NOT_SUPPORTED, SERVER_FULL, TOO_MANY_ATTEMPTS, SERVER }

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

    /** El texto que se entrega a quien recibe: lo que `SyncInvite.Format` de Core escribiría (base64url sin relleno de `{"U","C"}`). */
    fun format(serverUrl: String, code: String): String {
        val json = JsonObject(mapOf("U" to JsonPrimitive(serverUrl.trim()), "C" to JsonPrimitive(code.trim()))).toString()
        return PREFIX + Base64.getUrlEncoder().withoutPadding().encodeToString(json.toByteArray(Charsets.UTF_8))
    }

    /** Lado de quien aloja: pide al servidor un código de un solo uso (hace falta el token de administrador) y devuelve el texto a entregar. */
    fun create(serverUrl: String, adminToken: String, label: String?, client: OkHttpClient = OkHttpClient()): String {
        val fields = label?.trim()?.takeIf { it.isNotEmpty() }?.let { mapOf("label" to JsonPrimitive(it)) } ?: emptyMap()
        val body = JsonObject(fields).toString().toRequestBody("application/json".toMediaType())
        val request = adminRequest(serverUrl, "admin/v1/invites", adminToken).post(body).build()
        val text = adminCall(client, request) { it.body?.string().orEmpty() }
        val code = try {
            Json.parseToJsonElement(text).jsonObject["code"]?.jsonPrimitive?.content
        } catch (e: Exception) {
            null
        }
        return format(serverUrl, code?.takeIf { it.isNotEmpty() } ?: throw InviteException(InviteFailure.SERVER))
    }

    /** Los espacios creados con invitaciones (los que el token de administrador puede revocar), del más nuevo al más viejo. */
    fun listSpaces(serverUrl: String, adminToken: String, client: OkHttpClient = OkHttpClient()): List<SpaceInfo> {
        val request = adminRequest(serverUrl, "admin/v1/spaces", adminToken).get().build()
        val text = adminCall(client, request) { it.body?.string().orEmpty() }
        return try {
            Json.parseToJsonElement(text).jsonArray.map { item ->
                val obj = item.jsonObject
                SpaceInfo(
                    name = obj.getValue("name").jsonPrimitive.content,
                    label = (obj["label"] as? JsonPrimitive)?.takeIf { it.isString }?.content,
                    createdAt = OffsetDateTime.parse(obj.getValue("createdAt").jsonPrimitive.content).toInstant(),
                )
            }.sortedByDescending { it.createdAt }
        } catch (e: Exception) {
            throw InviteException(InviteFailure.SERVER)
        }
    }

    /** Corta el token de un espacio (sus archivos se quedan en el servidor). `false` si ya no existía. */
    fun revokeSpace(serverUrl: String, adminToken: String, name: String, client: OkHttpClient = OkHttpClient()): Boolean {
        // El nombre lo genera el servidor (`u-` + hex), pero se escapa igual: un nombre nunca debe poder cambiar la ruta.
        val request = adminRequest(serverUrl, "admin/v1/spaces/" + java.net.URLEncoder.encode(name, "UTF-8"), adminToken).delete().build()
        return adminCall(client, request, ifGone = false) { true }
    }

    private fun adminRequest(serverUrl: String, path: String, adminToken: String): Request.Builder {
        val base = serverUrl.trim().let { if (it.endsWith("/")) it else "$it/" }.toHttpUrlOrNull()
            ?: throw InviteException(InviteFailure.SERVER)
        return Request.Builder().url(base.resolve(path) ?: throw InviteException(InviteFailure.SERVER))
            .header("Authorization", "Bearer ${adminToken.trim()}")
    }

    /**
     * Ejecuta una llamada de administración y traduce los fallos. [ifGone] es lo que vale un 404 cuando el recurso concreto puede no existir
     * (revocar dos veces); sin él, un 404 significa que el servidor no tiene invitaciones.
     */
    private fun <T> adminCall(client: OkHttpClient, request: Request, ifGone: T? = null, read: (okhttp3.Response) -> T): T {
        try {
            client.newCall(request).execute().use { response ->
                when (response.code) {
                    401 -> throw InviteException(InviteFailure.NOT_ALLOWED)
                    404 -> return ifGone ?: throw InviteException(InviteFailure.NOT_SUPPORTED)
                    429 -> throw InviteException(InviteFailure.TOO_MANY_ATTEMPTS)
                }
                if (!response.isSuccessful) throw InviteException(InviteFailure.SERVER)
                return read(response)
            }
        } catch (e: IOException) {
            throw InviteException(InviteFailure.OFFLINE)
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
