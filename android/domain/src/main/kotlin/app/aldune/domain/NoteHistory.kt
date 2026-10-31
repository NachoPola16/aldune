package app.aldune.domain

import java.time.Duration
import java.time.Instant

/** Una copia anterior del texto de una nota, tal como estaba en [savedAt]. */
class NoteVersion(val id: Long, val noteId: String, val savedAt: Instant, val text: String)

/** Dónde viven las versiones. Room lo implementa en `app` (cifradas); aquí, [InMemoryVersionStore] para los tests. */
interface VersionStore {
    fun add(noteId: String, savedAt: Instant, text: String)

    /** Las versiones de la nota, la más reciente primero. */
    fun list(noteId: String): List<NoteVersion>

    /** Deja solo las [keep] más recientes. */
    fun trim(noteId: String, keep: Int)

    fun deleteAll(noteId: String)
}

class InMemoryVersionStore : VersionStore {
    private val rows = ArrayList<NoteVersion>()
    private var nextId = 1L

    @Synchronized override fun add(noteId: String, savedAt: Instant, text: String) { rows += NoteVersion(nextId++, noteId, savedAt, text) }
    @Synchronized override fun list(noteId: String) = rows.filter { it.noteId == noteId }.sortedByDescending { it.id }
    @Synchronized override fun trim(noteId: String, keep: Int) {
        val drop = list(noteId).drop(keep).map { it.id }.toSet()
        rows.removeAll { it.id in drop }
    }
    @Synchronized override fun deleteAll(noteId: String) { rows.removeAll { it.noteId == noteId } }
}

/**
 * Historial de versiones de una nota (equivalente de `NoteVersionRules` y los métodos de versiones de `NotesRepository` en Core). Es LOCAL: no
 * viaja por la sync, va cifrado en reposo y una nota protegida no guarda nada. Se guarda el texto que se va a reemplazar, así la primera
 * edición de una nota deja la original.
 */
class NoteHistory(private val store: VersionStore) {
    /** Antes de reemplazar [previous] por [next]: guarda [previous] si la última copia es de hace más del plazo (o no hay ninguna). */
    fun keepPreviousIfDue(noteId: String, previous: String, next: String, at: Instant) {
        if (previous == next) return
        val last = store.list(noteId).firstOrNull()
        if (last != null && Duration.between(last.savedAt, at) < MIN_INTERVAL) return
        add(noteId, previous, at)
    }

    /** Guarda [text] saltándose el plazo (al cerrar la nota, o antes de restaurar). Dos copias iguales seguidas no se guardan. */
    fun record(noteId: String, text: String, at: Instant) = add(noteId, text, at)

    /** Lo que llega por sync de otro dispositivo va a reemplazar el texto local: se guarda antes, por si el reemplazo no era lo que se quería. */
    fun keepBeforeOverwrite(noteId: String, localText: String, localProtected: Boolean, incomingText: String, incomingProtected: Boolean, at: Instant) {
        if (localProtected || incomingProtected || localText == incomingText) return
        add(noteId, localText, at)
    }

    fun versions(noteId: String): List<NoteVersion> = store.list(noteId)

    fun forget(noteId: String) = store.deleteAll(noteId)

    private fun add(noteId: String, text: String, at: Instant) {
        if (store.list(noteId).firstOrNull()?.text == text) return
        store.add(noteId, at, text)
        store.trim(noteId, MAX_VERSIONS)
    }

    companion object {
        const val MAX_VERSIONS = 20
        val MIN_INTERVAL: Duration = Duration.ofMinutes(5)
    }
}
