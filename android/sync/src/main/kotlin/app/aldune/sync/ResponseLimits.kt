package app.aldune.sync

import okhttp3.Response
import java.io.IOException

/**
 * Tope de lo que se lee de una respuesta (el mismo que `MaxResponseBytes` de Core): el servidor propio ya
 * rechaza objetos de más de 5 MB, y uno ajeno (WebDAV) no puede agotar la memoria con ocho descargas a la vez.
 */
internal const val MAX_RESPONSE_BYTES = 8L * 1024 * 1024

/** Una respuesta por encima del tope: en WebDAV un objeto así se salta (como uno ilegible); en una lista o un listado sigue siendo un fallo. */
internal class ResponseTooLargeException : IOException("The server's response is larger than the allowed $MAX_RESPONSE_BYTES bytes.")

/** Lee el cuerpo como texto sin pasar de [MAX_RESPONSE_BYTES]; si lo supera, falla en vez de cargarlo entero. */
internal fun Response.readLimitedString(): String {
    val source = body?.source() ?: return ""
    source.request(MAX_RESPONSE_BYTES + 1)
    if (source.buffer.size > MAX_RESPONSE_BYTES) throw ResponseTooLargeException()
    return source.readString(Charsets.UTF_8)
}
