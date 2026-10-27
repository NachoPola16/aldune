package app.aldune.sync

import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncEnvelopeCodec
import app.aldune.core.SyncFormatException
import okhttp3.Credentials
import okhttp3.HttpUrl
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import org.w3c.dom.Element
import org.w3c.dom.Node
import org.xml.sax.InputSource
import java.io.IOException
import java.io.StringReader
import java.time.Duration
import java.util.UUID
import javax.xml.parsers.DocumentBuilderFactory

/**
 * Transporte WebDAV para Nextcloud, ownCloud y servidores compatibles (`WebDavSyncTransport` de Core).
 * Solo sube sobres cifrados; la clave de sincronización nunca sale del dispositivo.
 *
 * Es bloqueante, como [SyncTransport]: se llama desde un hilo de fondo, nunca desde el de UI.
 * Los errores siguen el patrón de [HttpSyncTransport]:
 * - 401 → [IllegalStateException] (Core: `InvalidOperationException`);
 * - otro código que no sea 2xx → [IOException] (Core: `HttpRequestException`);
 * - un listado que no es XML → [IOException];
 * - un objeto ilegible o de formato desconocido se **salta** al leer, a diferencia de HTTP, que aborta la pasada:
 *   en una carpeta compartida puede haber ficheros a medio escribir o de una versión futura, y no deben
 *   impedir sincronizar el resto (es lo que hace Core con WebDAV).
 */
class WebDavSyncTransport(
    serverUrl: String,
    username: String,
    password: String,
    timeout: Duration = HttpSyncTransport.DEFAULT_TIMEOUT,
    client: OkHttpClient = OkHttpClient(),
) : SyncTransport {
    private val objectsUrl: HttpUrl
    private val authorization: String
    private val client: OkHttpClient
    private var collectionReady = false

    init {
        // Sin barra final, `resolve("objects/")` sustituiría el último segmento de la ruta (RFC 3986), así que se añade.
        val trimmed = serverUrl.trim()
        val base = (if (trimmed.endsWith("/")) trimmed else "$trimmed/").toHttpUrlOrNull()
        requireNotNull(base) { "The WebDAV URL must be an absolute HTTP(S) URL." }
        require(username.isNotBlank()) { "A WebDAV username is required." }
        require(password.isNotBlank()) { "A WebDAV password is required." }
        objectsUrl = base.resolve("objects/")!!
        // UTF-8 explícito: con ISO-8859-1 (el valor por defecto de OkHttp) una contraseña con tildes no coincidiría con la de Core.
        authorization = Credentials.basic(username, password, Charsets.UTF_8)
        // callTimeout cubre la llamada entera (conectar, enviar y leer), como HttpClient.Timeout en Core.
        this.client = client.newBuilder().callTimeout(timeout).build()
    }

    override fun readAll(): List<SyncEnvelope> {
        ensureCollection()
        val propfind = PROPFIND_BODY.toRequestBody(XML)
        val request = request(objectsUrl).method("PROPFIND", propfind).header("Depth", "1").build()
        val listing = execute(request) { it }
        val document = try {
            newDocumentBuilderFactory().newDocumentBuilder().parse(InputSource(StringReader(listing)))
        } catch (e: Exception) {
            throw IOException("The WebDAV server answered an invalid XML listing.", e)
        }

        val fileUrls = ArrayList<okhttp3.HttpUrl>()
        for (resource in document.getElementsByTagNameNS(DAV, "response").elements()) {
            val href = resource.child("href")?.textContent?.trim()
            if (href.isNullOrEmpty() || resource.hasDescendant("collection")) continue
            // Un 404 propio de la entrada (el fichero desapareció entre el listado y ahora) no es un objeto que leer.
            if (resource.child("status")?.textContent?.let { !isSuccessStatus(it) } == true) continue

            val fileUrl = resolveHref(href) ?: continue
            if (!fileUrl.pathSegments.last().endsWith(".json", ignoreCase = true)) continue

            fileUrls.add(fileUrl)
        }
        return parallelMap(fileUrls) { fileUrl ->
            val bytes = execute(request(fileUrl).get().build()) { it }
            try {
                SyncEnvelopeCodec.deserialize(bytes)
            } catch (_: SyncFormatException) {
                // Un archivo incompleto o de otra versión no debe impedir el resto de la sincronización.
                null
            } catch (_: IllegalArgumentException) {
                // Igual que arriba: JSON dañado o con campos que no son lo que dicen (un guid roto, un número que no lo es).
                null
            } catch (_: kotlinx.serialization.SerializationException) {
                // Una escritura interrumpida no invalida todo el almacén.
                null
            }
        }
    }

    override fun write(envelope: SyncEnvelope) {
        ensureCollection()
        val body = SyncEnvelopeCodec.serialize(envelope).toRequestBody(JSON)
        execute(request(objectUrl(envelope.noteId)).put(body).build()) { }
    }

    override fun delete(noteId: UUID) {
        ensureCollection()
        // 404 se tolera: otro dispositivo pudo borrarlo ya, y el resultado buscado es el mismo.
        client.newCall(request(objectUrl(noteId)).delete().build()).execute().use { response ->
            if (response.code != 404) ensureSuccess(response)
        }
    }

    /** Crea la carpeta `objects/` la primera vez; 405 (Method Not Allowed) es lo que contestan los servidores cuando ya existe. */
    private fun ensureCollection() {
        if (collectionReady) return
        client.newCall(request(objectsUrl).method("MKCOL", null).build()).execute().use { response ->
            if (response.code != 201 && response.code != 405) ensureSuccess(response)
        }
        collectionReady = true
    }

    private fun request(url: HttpUrl): Request.Builder = Request.Builder().url(url).header("Authorization", authorization)

    private fun objectUrl(noteId: UUID): HttpUrl =
        objectsUrl.resolve("${noteId.toString().replace("-", "")}.json") ?: throw IllegalArgumentException("Invalid WebDAV path.")

    /**
     * Los servidores contestan con la ruta absoluta (`/remote.php/dav/files/u/objects/x.json`) o con la URL entera, y
     * con la ruta codificada en porcentaje; `resolve` conserva esa codificación. De una URL entera solo se usa la ruta:
     * un servidor tras un proxy inverso puede anunciar su nombre interno, y seguirla enviaría las credenciales a otro host.
     */
    private fun resolveHref(href: String): HttpUrl? {
        val absolute = href.toHttpUrlOrNull()
        val resolved = objectsUrl.resolve(absolute?.encodedPath ?: href) ?: return null
        // Un href relativo al esquema (`//otro-host/x.json`) cambia de host al resolverse: se descarta, igual
        // que en Windows, para que la cabecera Authorization no salga hacia un servidor ajeno.
        if (resolved.scheme != objectsUrl.scheme || resolved.host != objectsUrl.host || resolved.port != objectsUrl.port) return null
        return resolved
    }

    private fun <T> execute(request: Request, read: (String) -> T): T =
        client.newCall(request).execute().use { response ->
            ensureSuccess(response)
            read(response.readLimitedString())
        }

    private fun ensureSuccess(response: Response) {
        if (response.code == 401) {
            throw IllegalStateException("WebDAV rejected the credentials (401). Check the username and the app password.")
        }
        if (!response.isSuccessful) {
            throw IOException("The WebDAV server answered ${response.code} ${response.message}".trim())
        }
    }

    companion object {
        private const val DAV = "DAV:"
        private val JSON = "application/json".toMediaType()
        private val XML = "application/xml; charset=utf-8".toMediaType()
        private const val PROPFIND_BODY =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?><d:propfind xmlns:d=\"DAV:\"><d:prop><d:resourcetype/></d:prop></d:propfind>"

        /** `HTTP/1.1 404 Not Found` → falso; si no se entiende la línea, se da por buena como hace Core, que no la mira. */
        private fun isSuccessStatus(line: String): Boolean {
            val code = line.trim().split(' ').getOrNull(1)?.toIntOrNull() ?: return true
            return code in 200..299
        }

        /** El XML viene de un servidor ajeno: sin DTD ni entidades externas (XXE, «billion laughs»). */
        private fun newDocumentBuilderFactory(): DocumentBuilderFactory = DocumentBuilderFactory.newInstance().apply {
            // Los espacios de nombres permiten aceptar `d:`, `D:` o ningún prefijo: lo que cuenta es que sea DAV:.
            isNamespaceAware = true
            // Cada ajuste va aparte y tolerante: el analizador de Android no admite todos estos nombres, pero tampoco resuelve entidades externas.
            for ((feature, value) in listOf(
                "http://apache.org/xml/features/disallow-doctype-decl" to true,
                "http://xml.org/sax/features/external-general-entities" to false,
                "http://xml.org/sax/features/external-parameter-entities" to false,
                "http://apache.org/xml/features/nonvalidating/load-external-dtd" to false,
            )) runCatching { setFeature(feature, value) }
            runCatching { isXIncludeAware = false }
            runCatching { isExpandEntityReferences = false }
        }

        private fun org.w3c.dom.NodeList.elements(): List<Element> =
            (0 until length).mapNotNull { item(it) as? Element }

        private fun Element.child(localName: String): Element? {
            var node: Node? = firstChild
            while (node != null) {
                if (node is Element && node.namespaceURI == DAV && node.localName == localName) return node
                node = node.nextSibling
            }
            return null
        }

        private fun Element.hasDescendant(localName: String) = getElementsByTagNameNS(DAV, localName).length > 0
    }
}
