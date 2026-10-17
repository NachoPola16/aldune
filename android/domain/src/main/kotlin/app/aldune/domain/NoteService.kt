package app.aldune.domain

import java.time.Duration

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

    fun setColor(id: String, color: String): NoteResult<Note> = synchronized(lock) {
        // Core no valida el color, pero una nota sin color no se puede pintar: lo único que se rechaza es el vacío.
        if (color.isBlank()) return NoteResult.Err(NoteError.InvalidArgument("El color no puede estar vacío"))
        change(id) { if (it.color == color) it else it.copy(color = color) }
    }

    /**
     * Copia de `SetTags` de Core: recorta, quita vacías y repetidas (sin distinguir mayúsculas, gana la primera), y se queda con 20.
     * Conserva la forma recibida: `NoteEquivalence` compara las etiquetas distinguiendo mayúsculas, y cambiarlas crearía conflictos falsos.
     */
    fun setTags(id: String, tags: List<String>): NoteResult<Note> = synchronized(lock) {
        val normalized = normalizeTags(tags)
        change(id) { if (it.tags == normalized) it else it.copy(tags = normalized) }
    }

    fun archive(id: String): NoteResult<Note> = setState(id, NoteState.ARCHIVED)

    fun trash(id: String): NoteResult<Note> = setState(id, NoteState.TRASHED)

    /** Saca la nota del archivo o de la papelera. */
    fun restore(id: String): NoteResult<Note> = setState(id, NoteState.ACTIVE)

    private fun setState(id: String, state: NoteState): NoteResult<Note> = synchronized(lock) {
        change(id) { if (it.state == state) it else it.copy(state = state) }
    }

    /**
     * Borra del todo una nota que está en la papelera y deja la lápida local. Desde cualquier otro estado es un error:
     * saltarse la papelera quitaría la red de seguridad que hace perdonable el borrado (comentario de `NotesRepository.Delete`).
     */
    fun deleteForever(id: String): NoteResult<Unit> = synchronized(lock) {
        val note = store.get(id) ?: return NoteResult.Err(NoteError.NotFound(id))
        if (note.state != NoteState.TRASHED) return NoteResult.Err(NoteError.NotInTrash(id, note.state))
        store.delete(id, clock.now())
        NoteResult.Ok(Unit)
    }

    /** Borra las de la papelera con más de [days] días sin cambios (estrictamente más viejas, como `PurgeExpiredTrash`). Devuelve cuántas. */
    fun purgeTrashed(days: Int = DEFAULT_TRASH_RETENTION_DAYS): NoteResult<Int> = synchronized(lock) {
        if (days < 0) return NoteResult.Err(NoteError.InvalidArgument("Los días de papelera no pueden ser negativos"))
        val now = clock.now()
        val cutoff = now.minus(Duration.ofDays(days.toLong()))
        val expired = store.all().filter { it.state == NoteState.TRASHED && it.updatedAt < cutoff }
        expired.forEach { store.delete(it.id, now) }
        NoteResult.Ok(expired.size)
    }

    /**
     * Aplica un cambio local a una nota. Si [transform] devuelve la misma nota, no hay cambio y no se escribe; si devuelve otra,
     * se sella como edición de este equipo (`UpdatedAt` y autor), que es lo que hace cada `Set*` de `NotesRepository`.
     */
    private inline fun change(id: String, transform: (Note) -> Note): NoteResult<Note> {
        val note = store.get(id) ?: return NoteResult.Err(NoteError.NotFound(id))
        val changed = transform(note)
        if (changed == note) return NoteResult.Ok(note)

        val stamped = changed.copy(updatedAt = clock.now(), syncAuthorDeviceId = null)
        store.put(stamped)
        return NoteResult.Ok(stamped)
    }

    companion object {
        const val DEFAULT_TRASH_RETENTION_DAYS = 30
        const val MAX_TAGS = 20

        internal fun normalizeTags(tags: List<String>): List<String> {
            val seen = HashSet<String>()
            val result = ArrayList<String>()
            for (raw in tags) {
                val tag = raw.trimDotNet()
                if (tag.isEmpty() || !seen.add(ordinalIgnoreCaseKey(tag))) continue
                result.add(tag)
                if (result.size == MAX_TAGS) break
            }
            return result
        }

        /**
         * `StringComparer.OrdinalIgnoreCase` de .NET: pasa a mayúsculas carácter a carácter (mapa simple, sin cambiar la longitud),
         * así que «ß» no es «SS» como sí lo sería con `String.uppercase()`.
         */
        private fun ordinalIgnoreCaseKey(text: String): String {
            val builder = StringBuilder(text.length)
            text.codePoints().forEach { builder.appendCodePoint(Character.toUpperCase(it)) }
            return builder.toString()
        }
    }
}
