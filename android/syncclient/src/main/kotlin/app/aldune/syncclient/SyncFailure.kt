package app.aldune.syncclient

import app.aldune.core.SyncCompatibility
import app.aldune.core.SyncFormatException
import app.aldune.sync.NOTHING_READABLE_ERROR
import java.io.InterruptedIOException
import java.net.SocketException

/**
 * Por qué falló una pasada, en términos que la interfaz puede traducir. Nada de texto de interfaz aquí: `app` elige el `R.string`
 * (spec F5 §4.5), y para [UNAUTHORIZED] además mira qué transporte tiene el perfil (token o usuario y contraseña).
 */
enum class SyncFailureKind {
    /** Sin red, sin ruta, DNS que no resuelve o un servidor que no contesta a tiempo. No es culpa del usuario ni de la configuración. */
    OFFLINE,
    UNAUTHORIZED,

    /** Ninguna nota del almacén descifra con la clave del perfil: se cambió y hay que pegar el código nuevo. */
    KEY_CHANGED,
    SERVER_ERROR,
    TOO_LARGE,

    /** El almacén tiene sobres de un formato más nuevo que el que entiende esta versión. */
    UNSUPPORTED_FORMAT,
    UNKNOWN,
}

/**
 * [detail] es el mensaje técnico (en inglés, el del transporte) para Ajustes y el diagnóstico; nunca contenido de notas.
 * [httpStatus] si el fallo vino de un código HTTP.
 */
data class SyncFailure(val kind: SyncFailureKind, val detail: String? = null, val httpStatus: Int? = null)

private const val MAX_DETAIL_LENGTH = 500

// «The sync server answered 413 Payload Too Large» / «The WebDAV server answered 500 …»: el código HTTP va en el mensaje (spec §4.6).
private val STATUS_IN_MESSAGE = Regex("""answered (\d{3})\b""")

// 401: el transporte HTTP escribe «(401)» y el de WebDAV también, ambos como IllegalStateException.
private val UNAUTHORIZED_IN_MESSAGE = Regex("""\(401\)""")

// Respuestas que no son lo que se esperaba (portal cautivo, página de un proxy, listado roto): el servidor está mal, no la red.
private val MALFORMED_ANSWERS = listOf(
    "something that is not JSON", "something that is not a list", "invalid XML listing", "without a valid noteId",
)

/**
 * Clasifica lo que lanzó el transporte (o el almacén). Se mira primero el tipo de la excepción y, para los códigos HTTP, su mensaje: con
 * los transportes reales de `android/sync` (las pruebas los usan contra MockWebServer, no mensajes escritos a mano). Si el mensaje de
 * un transporte cambiara, esas pruebas fallan y es el momento de añadir la excepción tipada de §4.6.
 */
fun classifyFailure(cause: Throwable): SyncFailure {
    val detail = describe(cause)

    // Las causas se miran también: OkHttp y las capas de Android envuelven un UnknownHostException o un ConnectException.
    val chain = generateSequence(cause) { it.cause }.take(8).toList()
    if (chain.any { it is SyncFormatException && it.message?.startsWith(NEWER_FORMAT_PREFIX) == true }) {
        return SyncFailure(SyncFailureKind.UNSUPPORTED_FORMAT, detail)
    }
    if (cause is IllegalStateException && cause.message?.let(UNAUTHORIZED_IN_MESSAGE::containsMatchIn) == true) {
        return SyncFailure(SyncFailureKind.UNAUTHORIZED, detail, 401)
    }

    val message = cause.message.orEmpty()
    val status = STATUS_IN_MESSAGE.find(message)?.groupValues?.get(1)?.toInt()
    if (status != null) {
        val kind = when {
            status == 401 -> SyncFailureKind.UNAUTHORIZED
            status == 413 -> SyncFailureKind.TOO_LARGE
            status == 404 || status in 500..599 -> SyncFailureKind.SERVER_ERROR
            else -> SyncFailureKind.UNKNOWN
        }
        return SyncFailure(kind, detail, status)
    }
    if (MALFORMED_ANSWERS.any { message.contains(it) }) return SyncFailure(SyncFailureKind.SERVER_ERROR, detail)

    // UnknownHostException, ConnectException, NoRouteToHostException, SocketTimeoutException y «Connection reset» son SocketException o
    // InterruptedIOException (el callTimeout de OkHttp lanza esta última sin ser un SocketTimeoutException).
    if (chain.any { it is SocketException || it is InterruptedIOException || it is java.net.UnknownHostException }) {
        return SyncFailure(SyncFailureKind.OFFLINE, detail)
    }
    return SyncFailure(SyncFailureKind.UNKNOWN, detail)
}

/** El error del plan (`SyncPlan.error`): hoy solo uno, «ninguna nota descifra». Otro texto se trata como desconocido, sin adivinar. */
fun classifyPlanError(error: String): SyncFailure =
    SyncFailure(if (error == NOTHING_READABLE_ERROR) SyncFailureKind.KEY_CHANGED else SyncFailureKind.UNKNOWN, error.take(MAX_DETAIL_LENGTH))

private val NEWER_FORMAT_PREFIX = SyncCompatibility.unsupportedFormatMessage(SyncCompatibility.CURRENT_FORMAT + 1).substringBefore("(format")

private fun describe(cause: Throwable): String {
    val message = cause.message?.takeIf { it.isNotBlank() }
    return (if (message != null) "${cause.javaClass.simpleName}: $message" else cause.javaClass.name).take(MAX_DETAIL_LENGTH)
}
