package app.aldune.domain

/**
 * Cuándo se abre el teclado solo al abrir una nota (Ajustes → Notas). La regla vive aquí y no en el editor para poder probarla.
 * Por defecto solo en las notas nuevas (es para escribir ya); leer una nota existente no debe taparle la mitad del texto.
 */
object KeyboardOnOpen {
    const val NEVER = 0
    const val NEW_NOTES = 1
    const val ALWAYS = 2

    /** Un valor guardado que no es de los tres (un ajuste de otra versión) vale como el de por defecto. */
    fun normalize(mode: Int): Int = if (mode in NEVER..ALWAYS) mode else NEW_NOTES

    fun shouldShow(mode: Int, isNewNote: Boolean, isProtected: Boolean): Boolean {
        // El texto de una protegida no se enseña hasta desbloquearla: el teclado no tiene nada que hacer antes.
        if (isProtected) return false
        return when (normalize(mode)) {
            NEVER -> false
            ALWAYS -> true
            else -> isNewNote
        }
    }
}
