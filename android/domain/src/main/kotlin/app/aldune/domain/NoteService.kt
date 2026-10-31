package app.aldune.domain

import app.aldune.text.TaskLists
import app.aldune.text.NoteColorAssigner
import app.aldune.text.NoteColorAssignment
import app.aldune.text.NoteOrdering
import app.aldune.text.NoteSearch
import app.aldune.text.ProtectedTitleRules
import app.aldune.text.NoteThemes
import app.aldune.text.NoteTheme
import app.aldune.text.NoteTone
import java.time.Duration
import java.time.Instant

/**
 * Cómo se colorea una nota nueva: los ajustes de Windows (`ActiveThemeId`, `NewNoteTone`, `ColorAssignment`, `FixedNoteColor`) ya resueltos.
 * Los valores por defecto son los de Core cuando no hay ajustes (`AppCoordinator.NextNoteColor`).
 */
data class NewNoteColorSettings(
    val theme: NoteTheme = NoteThemes.builtIn[0],
    val tone: NoteTone = NoteTone.Light,
    val rule: NoteColorAssignment = NoteColorAssignment.RotateAvoidNeighbors,
    val fixedColor: String? = null,
)

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
    // Compartido con ProtectionService y ConflictResolver (AppContainer): los tres leen-modifican-escriben el mismo NoteStore.
    private val lock: Any = Any(),
) {
    // Todas las operaciones leen, derivan y escriben dentro del mismo cerrojo: dos hilos que cambian cosas distintas de una nota
    // (el texto y el color, p. ej.) nunca se pisan, porque cada uno parte de lo último guardado y no de una copia vieja.
    /** Última hora de edición de texto aceptada por nota (ver [edit]). */
    private val lastTextEdit = HashMap<String, Instant>()

    /**
     * Crea la nota. Solo se llama en la primera edición con texto (spec Android §2.7): abrir una nota nueva y cerrarla sin escribir
     * no deja nada. Una nota creada aquí lleva siempre `LF`.
     *
     * Con [color] `null` se elige como en Windows (`AppCoordinator.NextNoteColor`): [NoteColorAssigner] mirando los colores de las
     * notas activas en el orden del mazo. [viewTag] es la etiqueta de la vista actual: con ella, las vecinas de la nota nueva son
     * solo las de esa etiqueta.
     */
    fun create(
        text: String,
        color: String? = null,
        settings: NewNoteColorSettings = NewNoteColorSettings(),
        screenOrigin: String = "",
        viewTag: String? = null,
    ): NoteResult<Note> = synchronized(lock) {
        val normalized = LineEnding.normalize(text)
        if (normalized.isEmpty()) return NoteResult.Err(NoteError.EmptyText)

        val now = clock.now()
        val note = Note(
            id = idFactory.newId(), text = normalized, lineEnding = LineEnding.LF, color = color ?: nextColor(settings, viewTag),
            createdAt = now, updatedAt = now, state = NoteState.ACTIVE, screenOrigin = screenOrigin,
        )
        store.put(note)
        NoteResult.Ok(note)
    }

    private fun nextColor(settings: NewNoteColorSettings, viewTag: String?): String {
        val dockColors = list(NoteState.ACTIVE, tag = viewTag).map { it.color }
        return NoteColorAssigner.assign(settings.theme, settings.tone, settings.rule, settings.fixedColor, dockColors)
    }

    /**
     * Guarda el texto que devuelve el editor. `\r\n` → `\n` solo para trabajar; la nota conserva el salto de línea con el que llegó.
     * Si el texto no cambia no se toca nada: abrir y cerrar una nota no genera versión.
     *
     * **Escrituras desordenadas.** [editedAt] es el momento en que el usuario produjo ese texto (la hora de la pulsación, no la de la
     * escritura). Regla: para una misma nota, una escritura de texto con `editedAt` anterior al de otra ya aceptada se descarta con
     * [NoteError.StaleWrite]; con igual hora gana la última en llegar; sin `editedAt` no se comprueba nada (gana la última, como en Core).
     * La regla compara solo contra otras *escrituras de texto*: un cambio de color, etiquetas o estado posterior no hace «vieja» a una edición
     * de texto, porque se aplica sobre lo último guardado y ninguno pisa al otro. Ojo: la comparación es entre horas del mismo reloj del
     * editor; no debe usarse con la `updatedAt` de una versión llegada por sync, cuyo reloj es el de otro equipo.
     * El registro de la última escritura vive en memoria: tras reiniciar la app no hay nada con qué comparar y la primera escritura gana.
     */
    fun edit(id: String, newText: String, editedAt: Instant? = null): NoteResult<Note> = synchronized(lock) {
        val note = store.get(id) ?: return NoteResult.Err(NoteError.NotFound(id))
        // Antes de mirar editedAt: una escritura rechazada no debe quedar anotada como «última edición de texto».
        if (note.isProtected) return NoteResult.Err(NoteError.Protected(id))

        if (editedAt != null) {
            val last = lastTextEdit[id]
            if (last != null && editedAt < last) return NoteResult.Err(NoteError.StaleWrite(id))
            lastTextEdit[id] = editedAt
        }

        val text = LineEnding.normalize(newText)
        if (text == note.text) return NoteResult.Ok(note)

        val edited = note.copy(text = text, updatedAt = clock.now(), syncAuthorDeviceId = null)
        store.put(edited)
        NoteResult.Ok(edited)
    }

    /**
     * Añade `item` como tarea al final de la lista de la nota (la lista rápida de la notificación). Solo una nota activa y sin proteger la
     * recibe; un artículo vacío es un no-op (no escribe ni cambia la hora). Es una edición normal: sube por sync como cualquier otra.
     */
    fun appendTask(id: String, item: String): NoteResult<Note> = synchronized(lock) {
        val note = store.get(id) ?: return NoteResult.Err(NoteError.NotFound(id))
        if (note.isProtected) return NoteResult.Err(NoteError.Protected(id))
        if (note.state != NoteState.ACTIVE) return NoteResult.Err(NoteError.InvalidArgument("La nota $id no está activa"))
        edit(id, TaskLists.appendTask(note.text, item))
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

    /**
     * Fija o desfija la nota arriba de las listas. Cuenta como una edición (la sella `change`): sin refrescar `updatedAt` la sync no
     * volvería a publicar la nota y el otro equipo nunca vería el pin.
     */
    fun setPinned(id: String, pinned: Boolean): NoteResult<Note> = synchronized(lock) {
        change(id) { if (it.pinnedToTop == pinned) it else it.copy(pinnedToTop = pinned) }
    }

    fun archive(id: String): NoteResult<Note> = setState(id, NoteState.ARCHIVED)

    fun trash(id: String): NoteResult<Note> = setState(id, NoteState.TRASHED)

    /** Saca la nota del archivo o de la papelera. */
    fun restore(id: String): NoteResult<Note> = setState(id, NoteState.ACTIVE)

    /**
     * Como [archive]/[trash]/[restore] pero con el código numérico del estado (0, 1, 2), que es como llega de la base o de sync.
     * Un código que no existe es un error, no se redondea a ningún estado.
     */
    fun changeState(id: String, stateCode: Int): NoteResult<Note> {
        val state = NoteState.fromCode(stateCode)
            ?: return NoteResult.Err(NoteError.InvalidArgument("Estado desconocido: $stateCode"))
        return setState(id, state)
    }

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
        lastTextEdit.remove(id)
        NoteResult.Ok(Unit)
    }

    /** Borra las de la papelera con más de [days] días sin cambios (estrictamente más viejas, como `PurgeExpiredTrash`). Devuelve cuántas. */
    fun purgeTrashed(days: Int = DEFAULT_TRASH_RETENTION_DAYS): NoteResult<Int> = synchronized(lock) {
        if (days < 0) return NoteResult.Err(NoteError.InvalidArgument("Los días de papelera no pueden ser negativos"))
        val now = clock.now()
        val cutoff = now.minus(Duration.ofDays(days.toLong()))
        val expired = store.all().filter { it.state == NoteState.TRASHED && it.updatedAt < cutoff }
        expired.forEach { store.delete(it.id, now); lastTextEdit.remove(it.id) }
        NoteResult.Ok(expired.size)
    }

    private fun searchable(note: Note, query: String): Boolean {
        if (!note.isProtected) return NoteSearch.matchesNote(note.text, note.tags, query)
        if (query.trimDotNet().isEmpty()) return true
        val name = ProtectedTitleRules.visible(true, note.protectedTitle) ?: return false
        return NoteSearch.matchesNote(name, emptyList(), query)
    }

    /** La nota con ese id, o `null`. Es lo que lee el editor al abrir una nota. */
    fun get(id: String): Note? = synchronized(lock) { store.get(id) }

    /**
     * Las notas de un estado en el orden del mazo, filtradas por etiqueta (sin distinguir mayúsculas) y por búsqueda en texto y etiquetas.
     * Una etiqueta `null` o una búsqueda vacía no filtran. Con una búsqueda no vacía las protegidas solo salen si coincide su nombre
     * visible (que ya se ve en la lista): su texto es desconocido y buscar por sus etiquetas daría pistas de una nota que el usuario ha
     * cerrado con contraseña.
     */
    fun list(filter: NoteState, query: String = "", tag: String? = null): List<Note> = synchronized(lock) {
        dockOrdered(store.all().filter { it.state == filter })
            .filter { note -> tag == null || note.tags.any { it.equals(tag, ignoreCase = true) } }
            .filter { searchable(it, query) }
    }

    /**
     * Pone la nota [id] justo después de [beforeId] y antes de [afterId], que son sus vecinas en la lista que el usuario está viendo
     * (`null` de un lado = extremo de esa lista). Port de `MoveNote` de Core: escribe **una sola fila** si el mazo ya está numerado y
     * cabe un número entre las vecinas; si alguna nota del estado no tiene posición (primer arrastre) o el hueco se agotó, numera todo el
     * estado con [NoteOrdering.spaced] y toca todas, porque la posición viaja dentro de cada nota y una no tocada no se volvería a subir.
     * Soltarla donde ya está no escribe nada (Core la reescribiría con el punto medio: una versión de sync sin ningún cambio visible).
     */
    fun move(id: String, beforeId: String?, afterId: String?): NoteResult<Note> = synchronized(lock) {
        val note = store.get(id) ?: return NoteResult.Err(NoteError.NotFound(id))
        val before = beforeId?.let { neighbour(it, note) ?: return neighbourError(it, note) }
        val after = afterId?.let { neighbour(it, note) ?: return neighbourError(it, note) }

        val current = dockOrdered(store.all().filter { it.state == note.state })
        val others = current.filter { it.id != id }
        if (before == null && after == null) {
            if (others.isEmpty()) return NoteResult.Ok(note)
            return NoteResult.Err(NoteError.InvalidArgument("Hace falta al menos una nota vecina para colocar $id"))
        }

        val beforePosition = before?.dockPosition
        val afterPosition = after?.dockPosition
        if (beforePosition != null && afterPosition != null && beforePosition > afterPosition) {
            return NoteResult.Err(NoteError.InvalidArgument("Las vecinas de $id están en orden inverso"))
        }

        val insertAt = if (before != null) others.indexOfFirst { it.id == before.id } + 1 else others.indexOfFirst { it.id == after!!.id }
        val reordered = others.toMutableList().apply { add(insertAt, note) }
        if (reordered.map { it.id } == current.map { it.id }) return NoteResult.Ok(note)

        val now = clock.now()
        // Igual que Core: basta una nota sin posición en el estado para numerarlo entero (es la primera vez que se arrastra).
        val target = if (others.all { it.dockPosition != null }) NoteOrdering.between(beforePosition, afterPosition) else null
        if (target != null) {
            val moved = note.copy(dockPosition = target, updatedAt = now, syncAuthorDeviceId = null)
            store.put(moved)
            return NoteResult.Ok(moved)
        }

        val positions = NoteOrdering.spaced(reordered.size)
        var moved = note
        reordered.forEachIndexed { index, item ->
            val renumbered = item.copy(dockPosition = positions[index], updatedAt = now, syncAuthorDeviceId = null)
            store.put(renumbered)
            if (item.id == id) moved = renumbered
        }
        NoteResult.Ok(moved)
    }

    /** La vecina si existe, es de otra nota y está en el mismo estado (el orden del mazo es por estado). */
    private fun neighbour(neighbourId: String, moved: Note): Note? =
        store.get(neighbourId)?.takeIf { it.id != moved.id && it.state == moved.state }

    private fun neighbourError(neighbourId: String, moved: Note): NoteResult.Err {
        val found = store.get(neighbourId) ?: return NoteResult.Err(NoteError.NotFound(neighbourId))
        val reason = if (found.id == moved.id) "Una nota no puede ser vecina de sí misma" else "La vecina $neighbourId está en ${found.state}, no en ${moved.state}"
        return NoteResult.Err(NoteError.InvalidArgument(reason))
    }

    /** El orden del mazo de `NotesRepository.GetByState` (`ORDER BY COALESCE(Position, 1e18), CreatedAt`); la regla vive en [DockOrder]. */
    private fun dockOrdered(notes: List<Note>): List<Note> = DockOrder.sort(notes)

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

        fun normalizeTags(tags: List<String>): List<String> {
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
