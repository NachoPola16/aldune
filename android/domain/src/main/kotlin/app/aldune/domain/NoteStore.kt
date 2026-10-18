package app.aldune.domain

import java.time.Instant

/**
 * Dónde viven las notas. Todo síncrono y pensado para un hilo de E/S; el `Flow` de «observar» lo añade `app` encima.
 * Lo implementan Room (en `app`) y [InMemoryNoteStore] (en los tests).
 */
interface NoteStore {
    /** Todas las notas, sin orden garantizado (el orden del mazo lo pone [NoteService.list]). */
    fun all(): List<Note>

    fun get(id: String): Note?

    /** Inserta o reemplaza por id. No toca las lápidas: resucitar una nota borrada es cosa de la sync. */
    fun put(note: Note)

    /** Borra la nota y deja una lápida local. `false` (y ninguna lápida) si no existía. */
    fun delete(id: String, deletedAt: Instant): Boolean

    fun tombstones(): List<Tombstone>
}

/** Almacén en memoria para los tests. Cuenta las escrituras para poder comprobar que «no escribir» no escribe. */
class InMemoryNoteStore : NoteStore {
    private val notes = LinkedHashMap<String, Note>()
    private val deleted = LinkedHashMap<String, Tombstone>()

    /** Cuántas veces se ha llamado a [put] o a un [delete] efectivo. */
    var writeCount: Int = 0
        private set

    @Synchronized override fun all(): List<Note> = notes.values.toList()

    @Synchronized override fun get(id: String): Note? = notes[id]

    // Se copia la lista de etiquetas: si el que llama conserva su MutableList y la cambia, no debe cambiar lo guardado.
    @Synchronized override fun put(note: Note) {
        notes[note.id] = note.copy(tags = note.tags.toList())
        writeCount++
    }

    @Synchronized override fun delete(id: String, deletedAt: Instant): Boolean {
        if (notes.remove(id) == null) return false
        deleted[id] = Tombstone(id, deletedAt, Tombstone.LOCAL_DEVICE)
        writeCount++
        return true
    }

    @Synchronized override fun tombstones(): List<Tombstone> = deleted.values.toList()

    // --- Lo que la sync necesita y el NoteStore normal no ofrece. Room lo hará con SQL; aquí, para el almacén de pruebas. ---

    /** Guarda una lápida tal cual (la de un borrado recibido lleva el `deviceId` de su autor, no el marcador `local`). */
    @Synchronized fun putTombstone(tombstone: Tombstone) {
        deleted[tombstone.noteId] = tombstone
        writeCount++
    }

    /** Quita la lápida de [id]; no hace nada si no había. Es lo que pasa cuando una nota borrada vuelve por sync. */
    @Synchronized fun removeTombstone(id: String) {
        if (deleted.remove(id) != null) writeCount++
    }

    /** Quita la nota **sin** dejar lápida: un borrado recibido deja la suya, con el autor remoto. */
    @Synchronized fun removeWithoutTombstone(id: String) {
        if (notes.remove(id) != null) writeCount++
    }

    /** Vacía notas y lápidas sin dejar rastro (primera conexión → «descartar»). */
    @Synchronized fun clear() {
        if (notes.isNotEmpty() || deleted.isNotEmpty()) writeCount++
        notes.clear()
        deleted.clear()
    }
}
