package app.aldune.domain

/**
 * Las reglas de «qué pasa cuando el usuario toca una nota». Sin Android ni Room: el almacén, el reloj, el id del
 * dispositivo y el generador de ids se inyectan para poder probar cada regla con tiempo y datos controlados.
 *
 * Toda operación local sigue la regla de Core (`NotesRepository`): cuando algo cambia, `UpdatedAt = clock.now` y
 * `syncAuthorDeviceId = null`, para que la sync suba la versión con el `deviceId` de este equipo. Y cuando nada cambia, no se escribe.
 *
 * @param deviceId el de este equipo. Hoy no se usa: las lápidas locales llevan el marcador `local` como en Core, y es la sync la
 * que lo sustituye por el `deviceId` al firmar. Se recibe ya para que F5 no cambie la firma del constructor.
 */
class NoteService(
    private val store: NoteStore,
    private val clock: Clock,
    @Suppress("unused") private val deviceId: String,
    private val idFactory: IdFactory,
) {
    // Todas las operaciones leen, derivan y escriben dentro del mismo cerrojo: dos hilos que cambian cosas distintas de una nota
    // (el texto y el color, p. ej.) nunca se pisan, porque cada uno parte de lo último guardado y no de una copia vieja.
    private val lock = Any()

    /**
     * Crea la nota. Solo se llama en la primera edición con texto (spec Android §2.7): abrir una nota nueva y cerrarla sin escribir
     * no deja nada. Una nota creada aquí lleva siempre `LF`.
     */
    fun create(text: String, color: String, screenOrigin: String = ""): NoteResult<Note> = synchronized(lock) {
        val normalized = LineEnding.normalize(text)
        if (normalized.isEmpty()) return NoteResult.Err(NoteError.EmptyText)

        val now = clock.now()
        val note = Note(
            id = idFactory.newId(), text = normalized, lineEnding = LineEnding.LF, color = color,
            createdAt = now, updatedAt = now, state = NoteState.ACTIVE, screenOrigin = screenOrigin,
        )
        store.put(note)
        NoteResult.Ok(note)
    }

    /**
     * Guarda el texto que devuelve el editor. `\r\n` → `\n` solo para trabajar; la nota conserva el salto de línea con el que llegó.
     * Si el texto no cambia no se toca nada: abrir y cerrar una nota no genera versión.
     */
    fun edit(id: String, newText: String): NoteResult<Note> = synchronized(lock) {
        val note = store.get(id) ?: return NoteResult.Err(NoteError.NotFound(id))
        val text = LineEnding.normalize(newText)
        if (text == note.text) return NoteResult.Ok(note)

        val edited = note.copy(text = text, updatedAt = clock.now(), syncAuthorDeviceId = null)
        store.put(edited)
        NoteResult.Ok(edited)
    }
}
