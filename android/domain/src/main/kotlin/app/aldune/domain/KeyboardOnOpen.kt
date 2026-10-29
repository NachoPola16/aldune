package app.aldune.domain

/**
 * Cuándo se abre el teclado solo al abrir una nota (Ajustes → Notas). La regla vive aquí y no en el editor para poder probarla.
 * Por defecto nunca: el teclado tapa la mitad de la pantalla y se abre con un toque en el texto. Quien prefiera escribir ya lo activa
 * para las notas nuevas o para todas.
 */
object KeyboardOnOpen {
    const val NEVER = 0
    const val NEW_NOTES = 1
    const val ALWAYS = 2

    /** Un valor guardado que no es de los tres (un ajuste de otra versión) vale como el de por defecto. */
    fun normalize(mode: Int): Int = if (mode in NEVER..ALWAYS) mode else NEVER

    /**
     * Lo guardado en una instalación anterior al cambio del valor por defecto. Antes el defecto era «solo nuevas» (1) y `save` lo escribía
     * siempre, así que quien ya usaba la app tiene guardado un 1 que no eligió: sin esto el nuevo defecto («nunca») solo llegaba a las
     * instalaciones nuevas y el teclado seguía saliendo. Una vez migrado ([alreadyMigrated]), lo guardado manda.
     */
    fun migrate(stored: Int, alreadyMigrated: Boolean): Int = if (!alreadyMigrated && stored == NEW_NOTES) NEVER else normalize(stored)

    fun shouldShow(mode: Int, isNewNote: Boolean, isProtected: Boolean): Boolean {
        // El texto de una protegida no se enseña hasta desbloquearla: el teclado no tiene nada que hacer antes.
        if (isProtected) return false
        return when (normalize(mode)) {
            ALWAYS -> true
            NEW_NOTES -> isNewNote
            else -> false
        }
    }
}
