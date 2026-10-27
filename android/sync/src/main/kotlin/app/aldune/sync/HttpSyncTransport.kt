package app.aldune.sync

import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonArray
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.JsonPrimitive
import java.io.IOException
import java.time.Duration
import java.util.UUID

/**
 * Transporte contra el servidor autohosteable (`HttpSyncTransport` de Core; rutas de `Aldune.SyncServer`).
 * El servidor solo enumera y guarda sobres cifrados; no conoce la clave.
 *
 * Es bloqueante, como [SyncTransport]: se llama desde un hilo de fondo (WorkManager), nunca desde el de UI.
 * Los errores se parecen a los de Core para que la app los muestre igual:
 * - 401 → [IllegalStateException] con el mensaje del token (Core: `InvalidOperationException`);
 * - cualquier otro código que no sea 2xx → [IOException] (Core: `HttpRequestException`);
 * - sin respuesta a tiempo → [java.io.InterruptedIOException] (un `SocketTimeoutException`), también [IOException];
 * - un objeto de formato desconocido o dañado → `SyncFormatException` y **aborta la pasada entera**, a diferencia
 *   de la carpeta, que lo salta. Es lo que hace Core con HTTP: es mejor parar que sincronizar con datos a medias.
 */
class HttpSyncTransport(
    serverUrl: String,
    token: String,
    timeout: Duration = DEFAULT_TIMEOUT,
    client: OkHttpClient = OkHttpClient(),
) : SyncTransport {
    private val baseUrl: HttpUrl = serverUrl.trim().toHttpUrlOrNull()
        ?: throw IllegalArgumentException("The sync server URL must be an absolute HTTP(S) URL.")
    private val authorization: String
    private val client: OkHttpClient

    init {
        require(token.isNotBlank()) { "A sync server token is required." }
        authorization = "Bearer $token"
        // callTimeout cubre la llamada entera (conectar, enviar y leer), como HttpClient.Timeout en Core.
        this.client = client.newBuilder().callTimeout(timeout).build()
    }

    override fun readAll(): List<SyncEnvelope> {
        val descriptors = execute(get("api/v1/objects")) { body ->
            val parsed = try {
                Json.parseToJsonElement(body)
            } catch (e: Exception) {
                // Una respuesta que no es JSON (portal cautivo, página de un proxy) es un fallo normal de sync.
                throw IOException("The sync server answered with something that is not JSON.", e)
            }
            parsed as? JsonArray ?: throw IOException("The sync server answered with something that is not a list.")
        }
        return parallelMap(descriptors) { descriptor ->
            val id = ((descriptor as? JsonObject)?.entries?.firstOrNull { it.key.equals("noteId", ignoreCase = true) }?.value as? JsonPrimitive)
                ?.content?.let { runCatching { UUID.fromString(it) }.getOrNull() }
                ?: throw IOException("The sync server listed an object without a valid noteId.")
            execute(get("api/v1/objects/${compact(id)}")) { SyncEnvelopeCodec.deserialize(it) }
        }
    }

    override fun write(envelope: SyncEnvelope) {
        val body = SyncEnvelopeCodec.serialize(envelope).toRequestBody(JSON)
        val request = request("api/v1/objects/${compact(envelope.noteId)}").put(body).build()
        execute(request) { }
    }

    override fun delete(noteId: UUID) {
        execute(request("api/v1/objects/${compact(noteId)}").delete().build()) { }
    }

    private fun get(relative: String): Request = request(relative).get().build()

    private fun request(relative: String): Request.Builder {
        // `resolve` aplica RFC 3986 igual que `new Uri(base, relative)` de .NET: sin barra final en la ruta
        // base, el último segmento se sustituye. Core normaliza el código de perfil para que acabe en «/».
        val url = baseUrl.resolve(relative) ?: throw IllegalArgumentException("Invalid sync server path.")
        return Request.Builder().url(url).header("Authorization", authorization)
    }

    private fun <T> execute(request: Request, read: (String) -> T): T =
        client.newCall(request).execute().use { response ->
            ensureSuccess(response)
            read(response.body?.string().orEmpty())
        }

    private fun ensureSuccess(response: Response) {
        if (response.code == 401) {
            throw IllegalStateException(
                "The sync server rejected the token (401). Paste only the value after ALDUNE_SYNC_TOKEN=.",
            )
        }
        if (!response.isSuccessful) {
            throw IOException("The sync server answered ${response.code} ${response.message}".trim())
        }
    }

    /** El id sin guiones, que es como lo espera el servidor (`{noteId:N}`). */
    private fun compact(id: UUID) = id.toString().replace("-", "")

    companion object {
        /** Lo que se espera a un servidor que no contesta: corto para no dejar la sync colgada al salir de casa sin VPN. */
        val DEFAULT_TIMEOUT: Duration = Duration.ofSeconds(20)

        private val JSON = "application/json".toMediaType()
    }
}
