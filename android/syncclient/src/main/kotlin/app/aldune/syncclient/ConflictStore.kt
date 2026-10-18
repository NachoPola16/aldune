package app.aldune.syncclient

import app.aldune.sync.SyncConflict

/**
 * Los conflictos que la sync guardó (la versión perdedora entera), a razón de **uno por nota**: uno nuevo de la misma nota reemplaza
 * al anterior (`LocalAction.SaveConflict`). Todo síncrono, como [LocalSyncStore]. Lo implementa Room (en `app`) y
 * [InMemoryConflictStore] (aquí).
 *
 * Los ids son los de nota en la forma canónica (minúsculas con guiones), los mismos que `Note.id`.
 */
interface ConflictStore {
    fun all(): List<SyncConflict>

    fun get(noteId: String): SyncConflict?

    /** No hace nada si no había conflicto de esa nota. */
    fun delete(noteId: String)
}

/** Almacén en memoria para los tests; cuenta las escrituras efectivas para poder comprobar que «no escribir» no escribe. */
class InMemoryConflictStore : ConflictStore {
    private val conflicts = LinkedHashMap<String, SyncConflict>()

    var writeCount: Int = 0
        private set

    /** Lo que haría la sync al guardar un conflicto; no forma parte de [ConflictStore] porque la app sólo los lee y los descarta. */
    @Synchronized fun put(conflict: SyncConflict) {
        conflicts[conflict.noteId.toString()] = conflict
        writeCount++
    }

    @Synchronized override fun all(): List<SyncConflict> = conflicts.values.toList()

    @Synchronized override fun get(noteId: String): SyncConflict? = conflicts[noteId]

    @Synchronized override fun delete(noteId: String) {
        if (conflicts.remove(noteId) != null) writeCount++
    }
}
