package app.aldune.syncclient

import java.time.Duration
import java.time.Instant

/**
 * Lo que `app` guarda de la sync para pintar el estado. Cuando una pasada acaba bien, [lastFailure] se pone a `null`; cuando falla,
 * [lastSuccessAt] se conserva (es lo que dice «última sync hace…» junto al error).
 */
data class SyncStatus(
    val lastSuccessAt: Instant? = null,
    val lastAttemptAt: Instant? = null,
    val lastFailure: SyncFailure? = null,
    val running: Boolean = false,
)

enum class StatusIcon { NONE, OK, SYNCING, OFFLINE, ERROR }

/** Qué dice la línea de estado. `app` lo traduce a un `R.string` (los textos viven en `strings.tsv`, no aquí). */
enum class StatusMessage {
    NOT_CONFIGURED,
    NEVER_SYNCED,
    SYNCING,
    LAST_SYNC,
    OFFLINE,
    UNAUTHORIZED,
    KEY_CHANGED,
    SERVER_ERROR,
    TOO_LARGE,
    UNSUPPORTED_FORMAT,
    UNKNOWN_ERROR,
}

/** «Hace…» como número: la unidad y la cantidad. La fecha la formatea `app` con el `Locale` del móvil. */
sealed interface RelativeTime {
    /** Menos de un minuto (también si la hora guardada va por delante de la del reloj: no se muestra «dentro de…»). */
    data object JustNow : RelativeTime

    data class Minutes(val count: Int) : RelativeTime
    data class Hours(val count: Int) : RelativeTime
    data class Days(val count: Int) : RelativeTime

    /** Hace una semana o más. */
    data class Date(val at: Instant) : RelativeTime
}

/**
 * @property relative cuándo fue la última sync buena, si hay y si el mensaje la usa («Última sync hace…», «Sin conexión · última sync hace…»).
 * @property failure el fallo completo para Ajustes (su [SyncFailure.detail] es el texto técnico); `null` si no hay.
 */
data class StatusLine(
    val icon: StatusIcon,
    val message: StatusMessage,
    val relative: RelativeTime? = null,
    val failure: SyncFailure? = null,
)

/**
 * El estado para la interfaz (spec F5 §4.5). [status] `null` = sin perfil configurado: no se dibuja nada.
 * Sin conexión lleva el icono de sin conexión y no el de error: no es culpa del usuario. Un perfil configurado que nunca
 * ha sincronizado lleva el icono normal: no hay nada roto.
 */
fun formatStatus(status: SyncStatus?, now: Instant): StatusLine {
    if (status == null) return StatusLine(StatusIcon.NONE, StatusMessage.NOT_CONFIGURED)
    val since = status.lastSuccessAt?.let { relativeTime(it, now) }

    if (status.running) return StatusLine(StatusIcon.SYNCING, StatusMessage.SYNCING, since)

    val failure = status.lastFailure
    if (failure != null) {
        val (icon, message) = when (failure.kind) {
            SyncFailureKind.OFFLINE -> StatusIcon.OFFLINE to StatusMessage.OFFLINE
            SyncFailureKind.UNAUTHORIZED -> StatusIcon.ERROR to StatusMessage.UNAUTHORIZED
            SyncFailureKind.KEY_CHANGED -> StatusIcon.ERROR to StatusMessage.KEY_CHANGED
            SyncFailureKind.SERVER_ERROR -> StatusIcon.ERROR to StatusMessage.SERVER_ERROR
            SyncFailureKind.TOO_LARGE -> StatusIcon.ERROR to StatusMessage.TOO_LARGE
            SyncFailureKind.UNSUPPORTED_FORMAT -> StatusIcon.ERROR to StatusMessage.UNSUPPORTED_FORMAT
            SyncFailureKind.UNKNOWN -> StatusIcon.ERROR to StatusMessage.UNKNOWN_ERROR
        }
        return StatusLine(icon, message, since, failure)
    }

    if (since == null) return StatusLine(StatusIcon.OK, StatusMessage.NEVER_SYNCED)
    return StatusLine(StatusIcon.OK, StatusMessage.LAST_SYNC, since)
}

/** Límites: < 1 min ahora mismo; < 60 min minutos; < 24 h horas; < 7 días días; a partir de ahí, la fecha. Siempre se redondea hacia abajo. */
fun relativeTime(then: Instant, now: Instant): RelativeTime {
    val elapsed = Duration.between(then, now)
    return when {
        elapsed < Duration.ofMinutes(1) -> RelativeTime.JustNow
        elapsed < Duration.ofHours(1) -> RelativeTime.Minutes(elapsed.toMinutes().toInt())
        elapsed < Duration.ofDays(1) -> RelativeTime.Hours(elapsed.toHours().toInt())
        elapsed < Duration.ofDays(7) -> RelativeTime.Days(elapsed.toDays().toInt())
        else -> RelativeTime.Date(then)
    }
}
