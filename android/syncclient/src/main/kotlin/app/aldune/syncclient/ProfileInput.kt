package app.aldune.syncclient

import app.aldune.core.ProfileCodes
import app.aldune.core.SyncShareCodeData
import app.aldune.sync.SyncScope
import java.net.URI
import java.net.URISyntaxException
import java.util.Locale

/** Por qué se rechaza un texto pegado como código de perfil. `app` elige el mensaje (spec F5 §4.7). */
enum class ProfileParseError {
    EMPTY,

    /** No empieza por un prefijo conocido: se pegó otra cosa. */
    NOT_A_CODE,

    /** Empieza como un código pero está roto: base64 o JSON dañado, versión no soportada, clave mala. */
    INVALID,

    /** Es de una carpeta compartida (transporte 0): el móvil no las tiene; usa servidor o WebDAV. */
    FOLDER_NOT_SUPPORTED,
}

sealed interface ProfileParse {
    /** Un código bien leído. La clave está en [profile]; nunca se registra ni se muestra. */
    sealed interface Parsed : ProfileParse {
        val profile: SyncShareCodeData

        /** El alcance del código, ya en el tipo del motor. Android no lo edita: se muestra y se respeta. */
        val scope: SyncScope
            get() = SyncClientSettings.scopeOf(profile.scope, profile.noteIds, profile.tag)
    }

    /** Servidor de Aldune: falta pedir el token. */
    data class Server(val url: String, override val profile: SyncShareCodeData) : Parsed

    /** WebDAV: falta pedir el usuario y la contraseña de aplicación. */
    data class WebDav(val url: String, override val profile: SyncShareCodeData) : Parsed

    /** Un código antiguo sin transporte: la interfaz pide elegir servidor o WebDAV y escribir la URL. */
    data class NeedsTransport(override val profile: SyncShareCodeData) : Parsed

    data class Rejected(val error: ProfileParseError) : ProfileParse
}

// Lo que copiar de un chat, un correo o un PDF suele añadir: espacios (también los duros), saltos, tabuladores y caracteres de
// ancho cero. El base64url del código no lleva ninguno, así que se quitan en cualquier sitio.
private val INVISIBLE = Regex("[\\s\\u00A0\\u200B-\\u200D\\u2060\\uFEFF]")

// Comillas rectas y tipográficas, comillas invertidas y <> alrededor del código (un enlace entre ángulos, una cita).
private const val WRAPPERS = "\"'`“”‘’«»<>"

/**
 * Lo que el usuario pegó → qué código es. Solo se limpia lo que añade copiar y pegar y se llama a `ProfileCodes.decode`; el texto de
 * delante o de detrás del código («Mi código es: …») **no** se intenta separar: no se puede saber dónde acaba sin adivinar.
 */
fun parseProfileInput(pasted: String): ProfileParse {
    val cleaned = pasted.replace(INVISIBLE, "").trim { it in WRAPPERS }
    if (cleaned.isEmpty()) return ProfileParse.Rejected(ProfileParseError.EMPTY)
    if (!ProfileCodes.isShareCode(cleaned)) return ProfileParse.Rejected(ProfileParseError.NOT_A_CODE)

    val profile = try {
        ProfileCodes.decode(cleaned)
    } catch (e: Exception) {
        return ProfileParse.Rejected(ProfileParseError.INVALID)
    }
    return when (profile.transport) {
        ProfileCodes.TRANSPORT_FOLDER -> ProfileParse.Rejected(ProfileParseError.FOLDER_NOT_SUPPORTED)
        ProfileCodes.TRANSPORT_SERVER -> ProfileParse.Server(profile.serverUrl!!, profile)
        ProfileCodes.TRANSPORT_WEBDAV -> ProfileParse.WebDav(profile.serverUrl!!, profile)
        else -> ProfileParse.NeedsTransport(profile)
    }
}

// --- credenciales escritas a mano ---

sealed interface ProfileCredentials {
    /** Servidor de Aldune. */
    data class Token(val token: String) : ProfileCredentials {
        // Un volcado de crash o un log con la petición no debe filtrar el secreto.
        override fun toString() = "Token(***)"
    }

    /** WebDAV: usuario y contraseña de aplicación. */
    data class Basic(val username: String, val password: String) : ProfileCredentials {
        override fun toString() = "Basic($username, ***)"
    }
}

enum class CredentialsError { URL_EMPTY, URL_INVALID, TOKEN_EMPTY, USERNAME_EMPTY, PASSWORD_EMPTY }

sealed interface CredentialsCheck {
    /** Con el valor ya limpio, que es el que hay que guardar y usar. */
    data class Valid(val url: String, val credentials: ProfileCredentials) : CredentialsCheck

    /** Todos los fallos a la vez, para marcar cada campo en una sola pasada. */
    data class Invalid(val errors: Set<CredentialsError>) : CredentialsCheck
}

private const val TOKEN_LINE_PREFIX = "ALDUNE_SYNC_TOKEN="

/**
 * URL absoluta `http` o `https` (se admite `http`: el servidor de casa va por la LAN) y credenciales no vacías tras recortar. Para el
 * token, si se pegó la línea entera `ALDUNE_SYNC_TOKEN=…` del `.env`, se queda con lo de después del `=` (es el error típico que ya avisa el
 * mensaje del 401). La contraseña no se recorta: solo se exige que no esté vacía, porque quitar un espacio cambiaría una contraseña válida.
 *
 * La URL se devuelve recortada pero sin más cambios, como en Windows: una ruta sin barra final sustituye su último segmento al resolver
 * las rutas del servidor (`https://host/aldune` → `https://host/api/v1/…`); quien necesite una subcarpeta debe terminarla en «/».
 */
fun validateCredentials(url: String, credentials: ProfileCredentials): CredentialsCheck {
    val errors = LinkedHashSet<CredentialsError>()
    val cleanUrl = url.trim()
    when {
        cleanUrl.isEmpty() -> errors += CredentialsError.URL_EMPTY
        !isAbsoluteHttpUrl(cleanUrl) -> errors += CredentialsError.URL_INVALID
    }

    val cleaned = when (credentials) {
        is ProfileCredentials.Token -> {
            val token = credentials.token.trim().let { if (it.startsWith(TOKEN_LINE_PREFIX)) it.removePrefix(TOKEN_LINE_PREFIX).trim() else it }
            if (token.isEmpty()) errors += CredentialsError.TOKEN_EMPTY
            ProfileCredentials.Token(token)
        }
        is ProfileCredentials.Basic -> {
            val user = credentials.username.trim()
            if (user.isEmpty()) errors += CredentialsError.USERNAME_EMPTY
            if (credentials.password.isEmpty()) errors += CredentialsError.PASSWORD_EMPTY
            ProfileCredentials.Basic(user, credentials.password)
        }
    }
    return if (errors.isEmpty()) CredentialsCheck.Valid(cleanUrl, cleaned) else CredentialsCheck.Invalid(errors)
}

/**
 * `http` hacia algo que no es la red local (ni bucle local, ni `.local`, ni un nombre sin puntos como `nas`): ahí el token viaja en claro por
 * Internet o por una wifi ajena. Por la LAN de casa (192.168.x, 10.x, 172.16-31.x) se sigue admitiendo sin más que el aviso de siempre.
 */
fun isCleartextOutsideLan(url: String): Boolean {
    val uri = try { java.net.URI(url.trim()) } catch (e: java.net.URISyntaxException) { return false }
    if (!uri.scheme.equals("http", ignoreCase = true)) return false
    val host = uri.host?.lowercase(java.util.Locale.ROOT)?.removeSurrounding("[", "]") ?: return false
    if (host == "localhost" || host == "::1" || host.endsWith(".local") || !host.contains('.') && !host.contains(':')) return false
    val octets = host.split('.').mapNotNull { it.toIntOrNull() }.takeIf { it.size == 4 && host.split('.').size == 4 } ?: return true
    val (a, b) = octets
    return !(a == 10 || a == 127 || (a == 192 && b == 168) || (a == 172 && b in 16..31) || (a == 169 && b == 254))
}

private fun isAbsoluteHttpUrl(value: String): Boolean {
    val uri = try {
        URI(value)
    } catch (e: URISyntaxException) {
        return false
    }
    val scheme = uri.scheme?.lowercase(Locale.ROOT)
    return (scheme == "http" || scheme == "https") && !uri.host.isNullOrEmpty()
}

// --- primera conexión ---

sealed interface FirstConnectDecision {
    /**
     * Se conecta sin preguntar. [dropLocalTombstones]: no hay notas pero sí lápidas; eran borrados de notas que el perfil no conoce y
     * se descartan en silencio (si no, subirían borrados firmados de ids ajenos).
     */
    data class Proceed(val dropLocalTombstones: Boolean) : FirstConnectDecision

    /** Hay [noteCount] notas locales (en cualquier estado): «Subir estas N notas al perfil» o «Descartarlas y quedarme con las del perfil». */
    data class AskUser(val noteCount: Int) : FirstConnectDecision
}

enum class FirstConnectChoice { UPLOAD, DISCARD }

/**
 * Pregunta si hay **alguna nota** local, activa, archivada o en la papelera. Las bases y los conflictos no cuentan: son de un perfil anterior.
 *
 * Un perfil **adicional** ([isFirstProfile] falso) nunca pregunta ni descarta: «descartar» borraría las notas de los demás perfiles. Solo
 * se sube lo que su alcance incluye y se baja lo que haya en su almacén. Tampoco descarta lápidas: son borrados que otros perfiles aún
 * pueden tener que firmar.
 */
fun firstConnectDecision(snapshot: LocalSnapshot, isFirstProfile: Boolean = true): FirstConnectDecision = when {
    !isFirstProfile -> FirstConnectDecision.Proceed(dropLocalTombstones = false)
    snapshot.notes.isNotEmpty() -> FirstConnectDecision.AskUser(snapshot.notes.size)
    else -> FirstConnectDecision.Proceed(dropLocalTombstones = snapshot.tombstones.isNotEmpty())
}

/**
 * Deja el almacén listo para la primera pasada. Las bases son de un perfil concreto y no valen para otro, así que siempre se olvidan.
 * - «Subir»: se conservan las notas; la pasada normal sube lo local sin base (el almacén no tiene esos ids, no hay conflicto).
 * - «Descartar»: `discardAllLocal`, **sin** crear lápidas, o subirían borrados firmados al perfil. El caso sin notas pero con lápidas usa
 *   `discardTombstones`: no toca las notas, por si el usuario escribió una mientras se guardaba el perfil.
 *
 * Con [FirstConnectDecision.AskUser] hace falta la elección; es un fallo de programa no pasarla.
 */
fun completeFirstConnect(store: LocalSyncStore, decision: FirstConnectDecision, choice: FirstConnectChoice? = null) {
    when (decision) {
        is FirstConnectDecision.Proceed -> if (decision.dropLocalTombstones) store.discardTombstones() else store.forgetSyncState()
        is FirstConnectDecision.AskUser -> when (requireNotNull(choice) { "Hay notas locales: hace falta la elección del usuario" }) {
            FirstConnectChoice.UPLOAD -> store.forgetSyncState()
            FirstConnectChoice.DISCARD -> store.discardAllLocal()
        }
    }
}
