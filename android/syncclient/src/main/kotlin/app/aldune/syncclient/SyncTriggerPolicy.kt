package app.aldune.syncclient

import app.aldune.domain.Clock
import java.time.Duration
import java.time.Instant

/** Lo que le pasa a la app y puede pedir una sync. `app` los genera; la política decide qué hacer con ellos. */
sealed interface SyncEvent {
    /** Se abrió la app. */
    data object AppOpened : SyncEvent

    /** Volvió a primer plano. */
    data object Foreground : SyncEvent

    /** Un guardado del editor o un cambio de estado, color u orden. */
    data object LocalChange : SyncEvent

    data object PullToRefresh : SyncEvent
    data object ManualSync : SyncEvent

    /** Lo pide WorkManager (cada ≥15 min, con red). */
    data object Periodic : SyncEvent

    /** Venció el temporizador que la app armó por un [SyncDecision.RunAt]. */
    data object TimerFired : SyncEvent

    /** Acabó la pasada que la política mandó lanzar. Hay que enviarlo siempre, también si falló. */
    data class SyncFinished(val outcome: SyncOutcome) : SyncEvent
}

sealed interface SyncDecision {
    /** Lanza una pasada ahora. La política ya la da por en curso hasta que llegue [SyncEvent.SyncFinished]. */
    data object RunNow : SyncDecision

    /** Arma un temporizador y, al vencer, envía [SyncEvent.TimerFired]. Un `RunAt` nuevo sustituye al anterior. */
    data class RunAt(val at: Instant) : SyncDecision

    data object Nothing : SyncDecision
}

/**
 * Cuándo se sincroniza (spec F5 §4.4): una máquina de estados pura, con reloj inyectado, que no conoce Android ni WorkManager.
 * `app` le pasa eventos y obedece lo que decide.
 *
 * - Una sola pasada a la vez: un evento con una pasada en curso no lanza otra, deja «repetir al acabar» (una repetición, por
 *   muchos eventos que lleguen).
 * - Tras un fallo (cualquiera) las automáticas esperan [FAILURE_COOLDOWN] desde el fallo; las manuales nunca. No hay reintento creciente:
 *   la app es silenciosa y vuelve a probar en el siguiente disparador.
 * - Los cambios locales se agrupan: cada uno retrasa [LOCAL_CHANGE_DELAY] la pasada, para no sincronizar a cada letra.
 *
 * No es seguro para hilos: `app` la usa desde uno solo (el hilo principal o un despachador de un hilo).
 */
class SyncTriggerPolicy(private val clock: Clock, profileConfigured: Boolean = false) {
    private var configured = profileConfigured
    private var running = false
    private var lastAttemptAt: Instant? = null
    private var lastFailureAt: Instant? = null

    /** Cuándo toca la pasada por cambios locales o por ediciones concurrentes; `null` si no hay ninguna esperando. */
    private var dueAt: Instant? = null

    /** Llegó un evento manual con una pasada en curso. */
    private var repeatManual = false

    /** Llegó un evento automático con una pasada en curso. */
    private var repeatAuto = false

    /** Hay o no un perfil de sync configurado. Sin perfil la política no hace nada, y al quitarlo se olvida todo lo pendiente. */
    fun setProfileConfigured(value: Boolean) {
        configured = value
        if (!value) reset()
    }

    fun onEvent(event: SyncEvent): SyncDecision {
        if (!configured) return SyncDecision.Nothing
        val now = clock.now()
        return when (event) {
            SyncEvent.ManualSync, SyncEvent.PullToRefresh -> manual()
            SyncEvent.AppOpened, SyncEvent.Periodic -> automatic(now)
            SyncEvent.Foreground -> {
                val last = lastAttemptAt
                // «Hace más de 30 s»: a los 30 s justos todavía no.
                if (last != null && Duration.between(last, now) <= FOREGROUND_MIN_GAP) SyncDecision.Nothing else automatic(now)
            }
            SyncEvent.LocalChange -> localChange(now)
            SyncEvent.TimerFired -> timerFired(now)
            is SyncEvent.SyncFinished -> finished(event.outcome, now)
        }
    }

    private fun manual(): SyncDecision {
        if (running) {
            repeatManual = true
            return SyncDecision.Nothing
        }
        return start()
    }

    private fun automatic(now: Instant): SyncDecision {
        if (running) {
            repeatAuto = true
            return SyncDecision.Nothing
        }
        if (inCooldown(now)) return SyncDecision.Nothing
        return start()
    }

    private fun localChange(now: Instant): SyncDecision {
        val due = maxOf(now.plus(LOCAL_CHANGE_DELAY), cooldownEnd() ?: Instant.MIN)
        dueAt = due
        // Con una pasada en curso no se arma nada: al acabar se mira `dueAt` (el cambio llegó después de la foto, hace falta otra pasada).
        return if (running) SyncDecision.Nothing else SyncDecision.RunAt(due)
    }

    private fun timerFired(now: Instant): SyncDecision {
        val due = dueAt ?: return SyncDecision.Nothing
        if (running) return SyncDecision.Nothing
        // Otro cambio llegó mientras tanto y retrasó la hora: el temporizador viejo vence antes de tiempo.
        return if (now < due) SyncDecision.RunAt(due) else start()
    }

    private fun finished(outcome: SyncOutcome, now: Instant): SyncDecision {
        running = false
        val failed = outcome is SyncOutcome.Failure
        lastFailureAt = if (failed) now else null
        val manualRepeat = repeatManual
        val autoRepeat = repeatAuto
        repeatManual = false
        repeatAuto = false

        // Lo que pidió el usuario con la mano se hace aunque la pasada anterior fallara.
        if (manualRepeat) return start()

        if (failed) {
            // Silencioso: las automáticas que esperaban se descartan (el siguiente disparador lo reintentará); solo los cambios
            // locales pendientes se vuelven a armar, sin saltarse la espera.
            val pending = dueAt ?: return SyncDecision.Nothing
            val retryAt = maxOf(pending, now.plus(FAILURE_COOLDOWN))
            dueAt = retryAt
            return SyncDecision.RunAt(retryAt)
        }

        // Notas editadas mientras la sync hablaba con el servidor: se pide otra pasada pronto para que el conflicto, si lo hay, se vea.
        if (outcome is SyncOutcome.Success && outcome.skippedConcurrent > 0) {
            val due = maxOf(now.plus(LOCAL_CHANGE_DELAY), dueAt ?: Instant.MIN)
            dueAt = due
            return SyncDecision.RunAt(due)
        }

        if (autoRepeat) return start()
        val due = dueAt ?: return SyncDecision.Nothing
        return if (due <= now) start() else SyncDecision.RunAt(due)
    }

    private fun start(): SyncDecision {
        running = true
        lastAttemptAt = clock.now()
        // Esta pasada recoge lo pendiente; lo que llegue durante ella vuelve a poner su marca.
        dueAt = null
        repeatManual = false
        repeatAuto = false
        return SyncDecision.RunNow
    }

    private fun cooldownEnd(): Instant? = lastFailureAt?.plus(FAILURE_COOLDOWN)

    private fun inCooldown(now: Instant): Boolean = cooldownEnd()?.let { now < it } == true

    private fun reset() {
        running = false
        lastAttemptAt = null
        lastFailureAt = null
        dueAt = null
        repeatManual = false
        repeatAuto = false
    }

    companion object {
        val FOREGROUND_MIN_GAP: Duration = Duration.ofSeconds(30)
        val LOCAL_CHANGE_DELAY: Duration = Duration.ofSeconds(5)
        val FAILURE_COOLDOWN: Duration = Duration.ofSeconds(60)
    }
}
