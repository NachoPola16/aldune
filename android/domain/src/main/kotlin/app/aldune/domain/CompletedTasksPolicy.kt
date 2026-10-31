package app.aldune.domain

/**
 * Qué hace una nota con sus tareas hechas, por nota y no por dispositivo (equivalente de `CompletedTasksPolicy` de Core). Un ajuste local no
 * sirve en una lista compartida: lo que un dispositivo oculte o borre no debe vaciarla en los demás. Viaja como campo opcional `completedTasks`
 * del contenido cifrado (solo si no es 0), sin subir el formato 4. Los números son los del JSON y los de la base: no reordenar.
 * En Android «ocultar tareas hechas» solo oculta; las políticas 1 y 2 tampoco ocultan nunca.
 */
object CompletedTasksPolicy {
    /** Cada dispositivo según sus ajustes. */
    const val DEFAULT = 0
    /** La tarea hecha se queda donde está y la nota no oculta nada. */
    const val KEEP_IN_PLACE = 1
    /** La tarea hecha baja al final y la nota no oculta nada. */
    const val MOVE_TO_END = 2

    /** Un número desconocido (de una versión futura) se lee como «por defecto»: peor sería fallar al leer la nota. */
    fun normalize(value: Int): Int = if (value == KEEP_IN_PLACE || value == MOVE_TO_END) value else DEFAULT

    /** El elemento de menú que cicla: por defecto → mantener → al final → por defecto. */
    fun next(current: Int): Int = when (normalize(current)) {
        DEFAULT -> KEEP_IN_PLACE
        KEEP_IN_PLACE -> MOVE_TO_END
        else -> DEFAULT
    }

    /** La política de la nota manda sobre «reordenar al marcar» del dispositivo, en los dos sentidos. */
    fun shouldMoveToEnd(policy: Int, deviceSetting: Boolean): Boolean = when (normalize(policy)) {
        KEEP_IN_PLACE -> false
        MOVE_TO_END -> true
        else -> deviceSetting
    }

    /** ¿Puede «ocultar tareas hechas» actuar sobre esta nota? Solo con la política por defecto y si el dispositivo lo tiene activado. */
    fun mayHide(policy: Int, deviceSetting: Boolean): Boolean = normalize(policy) == DEFAULT && deviceSetting
}
