package app.aldune.domain

/** Por qué no se pudo hacer algo con una nota. Cada caso lleva lo que la interfaz necesita para decirlo; el texto de `message` es para el log. */
sealed class NoteError(val message: String) {
    data class NotFound(val id: String) : NoteError("No existe la nota $id")

    /** Crear una nota exige al menos un carácter (la nota no existe hasta la primera edición con texto). */
    data object EmptyText : NoteError("No se crea una nota sin texto")

    /** Borrar para siempre solo se permite desde la papelera. */
    data class NotInTrash(val id: String, val state: NoteState) : NoteError("La nota $id está en $state: solo se borra para siempre desde la papelera")

    data class InvalidArgument(val reason: String) : NoteError(reason)

    /** Una escritura más vieja que otra ya aceptada para la misma nota; se descarta para no pisar la nueva. */
    data class StaleWrite(val id: String) : NoteError("La escritura sobre $id es más antigua que otra ya guardada")
}

/** Resultado de una operación del dominio: o el valor, o un [NoteError]. Nada lanza por datos del usuario. */
sealed class NoteResult<out T> {
    data class Ok<out T>(val value: T) : NoteResult<T>()
    data class Err(val error: NoteError) : NoteResult<Nothing>()

    /** Para tests y para quien ya comprobó que no falla; con un error lanza con su mensaje. */
    fun getOrThrow(): T = when (this) {
        is Ok -> value
        is Err -> throw IllegalStateException(error.message)
    }

    fun errorOrNull(): NoteError? = (this as? Err)?.error
}
