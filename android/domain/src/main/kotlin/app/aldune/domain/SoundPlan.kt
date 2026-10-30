package app.aldune.domain

/** Momentos de la app que pueden sonar. Las claves son las de Windows (`SoundPlan.Id`): no se renombran. */
enum class SoundEvent(val id: String) {
    REMINDER("reminder"),
    SYNC_DONE("syncDone"),
    SYNC_FAILED("syncFailed"),
    NOTE_CREATED("noteCreated"),
    NOTE_ARCHIVED("noteArchived"),
    NOTE_OPENED("noteOpened"),
    NOTE_CLOSED("noteClosed"),
    TASK_CHECKED("taskChecked"),
    NOTE_DELETED("noteDeleted"),
    TYPING("typing"),
    TEXT_DELETED("textDeleted"),
}

/**
 * Qué suena. Los números son los de `SoundChoice` en Windows. Android solo tiene los cinco sonidos propios de papel y pluma
 * (del 7 al 12): los del sistema de Windows (1-5) y el .wav propio (6) no existen aquí y se quedan en silencio.
 */
enum class SoundChoice(val code: Int) {
    NONE(0),
    PAGE_TURN(7),
    PAPER_FOLD(8),
    PEN_TAP(9),
    PAPER_CRUMPLE(10),
    PEN_SCRATCH(11),
    ERASER(12);

    companion object {
        fun fromCode(code: Int): SoundChoice? = entries.firstOrNull { it.code == code }
    }
}

/** Lo que el usuario tiene guardado: el interruptor general, el volumen (0-100) y la elección por clave de evento. */
data class SoundSettings(
    val enabled: Boolean = false,
    val volume: Int = 100,
    val choices: Map<String, Int> = emptyMap(),
) {
    val volumeFactor: Float get() = volume.coerceIn(0, 100) / 100f
}

/**
 * Decide qué suena para cada evento (el mismo modelo que `SoundPlan` de Windows). Apagado de fábrica y, aunque se encienda el
 * interruptor, ningún evento suena hasta que se le elige un sonido: los recordatorios ya traen el de su notificación.
 */
object SoundPlan {
    private val PAPER_AND_PEN = mapOf(
        SoundEvent.NOTE_OPENED to SoundChoice.PAGE_TURN,
        SoundEvent.NOTE_CLOSED to SoundChoice.PAPER_FOLD,
        SoundEvent.TASK_CHECKED to SoundChoice.PEN_TAP,
        SoundEvent.NOTE_DELETED to SoundChoice.PAPER_CRUMPLE,
    )

    /** El sonido a reproducir, o `null` si no debe sonar nada. */
    fun resolve(settings: SoundSettings, event: SoundEvent): SoundChoice? {
        if (!settings.enabled) return null
        val choice = settings.choices[event.id]?.let(SoundChoice::fromCode) ?: return null
        return choice.takeIf { it != SoundChoice.NONE }
    }

    /**
     * Los sonidos sugeridos: los cuatro eventos de las notas que se oyen de vez en cuando. Escribir y borrar texto suenan en cada
     * pulsación y se activan a mano; lo demás (avisos, sync) queda como estaba.
     */
    fun paperAndPen(choices: Map<String, Int>): Map<String, Int> =
        choices + PAPER_AND_PEN.map { (event, choice) -> event.id to choice.code }

    /** Al encender los sonidos la primera vez no hay nada elegido: se aplican los sugeridos. Con algo ya elegido, `null` (no tocar nada). */
    fun paperAndPenIfUnset(choices: Map<String, Int>): Map<String, Int>? =
        if (PAPER_AND_PEN.keys.any { it.id in choices }) null else paperAndPen(choices)
}

/** Frena el sonido de escribir: como mucho uno cada 70 ms y solo con caracteres imprimibles. */
class TypingThrottle {
    private var last = Long.MIN_VALUE

    fun tryAcquire(nowMs: Long): Boolean {
        if (last != Long.MIN_VALUE && nowMs - last < MIN_INTERVAL_MS) return false
        last = nowMs
        return true
    }

    companion object {
        const val MIN_INTERVAL_MS = 70L

        fun isPrintable(c: Char): Boolean = !c.isISOControl()

        /** ¿Este cambio de texto es borrar? El texto queda más corto: Retroceso, Supr, cortar o borrar una selección. */
        fun isDeleting(before: String, after: String): Boolean = after.length < before.length

        /**
         * ¿Este cambio de texto es escribir? El texto es más largo y lo añadido es imprimible (un intro, un pegado de varias
         * líneas con saltos o borrar no suenan). Se compara por prefijo y sufijo comunes para hallar lo insertado.
         */
        fun isTyping(before: String, after: String): Boolean {
            if (after.length <= before.length) return false
            var start = 0
            while (start < before.length && before[start] == after[start]) start++
            var endBefore = before.length
            var endAfter = after.length
            while (endBefore > start && before[endBefore - 1] == after[endAfter - 1]) { endBefore--; endAfter-- }
            val inserted = after.substring(start, endAfter)
            return inserted.isNotEmpty() && inserted.all(::isPrintable)
        }
    }
}
