package app.aldune.sync

import app.aldune.core.NoteState
import app.aldune.core.SyncEnvelope
import app.aldune.core.SyncNote
import app.aldune.core.SyncVersion
import java.time.OffsetDateTime
import java.util.UUID

/**
 * Lado local en memoria, con la semántica del repositorio de Core: aplicar una nota borra la lápida de
 * su id, aplicar un borrado quita la nota y deja la lápida, y la papelera cambia estado, fecha y autor.
 * Sirve de doble en los tests y de referencia para la implementación con Room.
 */
class InMemoryLocalStore(
    notes: List<LocalNote> = emptyList(),
    tombstones: List<LocalTombstone> = emptyList(),
    bases: Map<UUID, SyncBase> = emptyMap(),
) : LocalStore {
    val notes: MutableMap<UUID, LocalNote> = linkedMapOf<UUID, LocalNote>().also { map -> notes.forEach { map[it.note.id] = it } }
    val tombstones: MutableMap<UUID, LocalTombstone> =
        linkedMapOf<UUID, LocalTombstone>().also { map -> tombstones.forEach { map[it.noteId] = it } }
    val bases: MutableMap<UUID, SyncBase> = LinkedHashMap(bases)
    val conflicts: MutableList<SyncConflict> = mutableListOf()

    /** Cuántas veces se llamó a [setBases]: el motor debe hacerlo a lo sumo una vez por pasada. */
    var setBasesCalls: Int = 0
        private set

    override fun applyNote(note: SyncNote, authorDeviceId: String) {
        notes[note.id] = LocalNote(note, authorDeviceId)
        tombstones.remove(note.id)
    }

    override fun applyTombstone(noteId: UUID, deletedAt: OffsetDateTime, deviceId: String) {
        notes.remove(noteId)
        tombstones[noteId] = LocalTombstone(noteId, deletedAt, deviceId)
    }

    override fun trashNote(noteId: UUID, at: OffsetDateTime) {
        val current = notes[noteId] ?: return
        notes[noteId] = LocalNote(current.note.copy(state = NoteState.TRASH, updatedAt = at), authorDeviceId = null)
    }

    override fun saveConflict(conflict: SyncConflict) {
        conflicts += conflict
    }

    override fun setBases(bases: Map<UUID, SyncBase>) {
        setBasesCalls++
        this.bases.putAll(bases)
    }
}

/**
 * Almacén remoto en memoria: un objeto por id, como la carpeta. [unreadable] son los objetos que el
 * transporte real saltaría (formato desconocido): se guardan aparte, intactos, y nunca se devuelven.
 */
class InMemorySyncTransport(
    objects: List<SyncEnvelope> = emptyList(),
    val unreadable: List<String> = emptyList(),
) : SyncTransport {
    val store: MutableMap<UUID, SyncEnvelope> = linkedMapOf<UUID, SyncEnvelope>().also { map -> objects.forEach { map[it.noteId] = it } }

    /** Los ids escritos por esta pasada (un id escrito dos veces cuenta una). */
    val written: MutableSet<UUID> = linkedSetOf()
    val deleted: MutableList<UUID> = mutableListOf()

    override fun readAll(): List<SyncEnvelope> = store.values.toList()

    /**
     * Como `FolderSyncTransport.Write`: un sobre más antiguo que el que ya hay no lo pisa (la subida cuenta
     * igualmente en el motor; E12c lo fija: un borrado sin firma más reciente que la nota en la papelera).
     */
    override fun write(envelope: SyncEnvelope) {
        val current = store[envelope.noteId]
        if (current != null && SyncVersion.compare(SyncVersion.of(envelope), SyncVersion.of(current)) < 0) return
        store[envelope.noteId] = envelope
        written += envelope.noteId
    }

    override fun delete(noteId: UUID) {
        store.remove(noteId)
        deleted += noteId
    }
}
