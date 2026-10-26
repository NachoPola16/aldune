package app.aldune.syncclient

import java.time.Instant

/** La última vez que corrió el trabajo periódico de segundo plano y cómo acabó ([failure] `null` = bien). */
data class BackgroundRun(val at: Instant, val failure: SyncFailureKind? = null)

enum class BackgroundMessage {
    /** El interruptor está apagado: no hay nada que explicar. */
    DISABLED,

    /** Encendido, pero el sistema todavía no ha lanzado ninguna pasada. */
    NEVER_RAN,
    OK,

    /** Sin red en la última pasada: normal en segundo plano, no exige nada al usuario. */
    OFFLINE,

    /** La última pasada falló por algo que hay que mirar (credenciales, clave, servidor…): el tipo va en [BackgroundLine.failure]. */
    FAILED,
}

/**
 * @property restricted el sistema tiene limitada la app en segundo plano (modo restringido): las pasadas casi no corren y conviene decirlo.
 */
data class BackgroundLine(
    val message: BackgroundMessage,
    val relative: RelativeTime? = null,
    val failure: SyncFailureKind? = null,
    val restricted: Boolean = false,
)

/** Qué dice Ajustes bajo «Sincronizar en segundo plano». Los textos los pone `app`; aquí solo se decide el caso. */
fun formatBackground(enabled: Boolean, last: BackgroundRun?, restricted: Boolean, now: Instant): BackgroundLine {
    if (!enabled) return BackgroundLine(BackgroundMessage.DISABLED)
    if (last == null) return BackgroundLine(BackgroundMessage.NEVER_RAN, restricted = restricted)
    val since = relativeTime(last.at, now)
    return when (last.failure) {
        null -> BackgroundLine(BackgroundMessage.OK, since, restricted = restricted)
        SyncFailureKind.OFFLINE -> BackgroundLine(BackgroundMessage.OFFLINE, since, restricted = restricted)
        else -> BackgroundLine(BackgroundMessage.FAILED, since, last.failure, restricted)
    }
}
