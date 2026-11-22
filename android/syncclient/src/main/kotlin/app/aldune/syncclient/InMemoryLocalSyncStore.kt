package app.aldune.syncclient

import app.aldune.core.SyncNote
import app.aldune.domain.InMemoryNoteStore
import app.aldune.domain.NoteState
import app.aldune.domain.Tombstone
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncConflict

/**
 * [LocalSyncStore] en memoria sobre un [InMemoryNoteStore]: el mismo almacén que usa `NoteService`, como hará Room (una sola base
 * para las notas y para la sync). Sirve de referencia y para los tests de extremo a extremo.
 *
 * La «transacción» es el monitor del propio [InMemoryNoteStore] (sus métodos son `@Synchronized`): mientras `snapshot` o `commit`
 * la tienen cogida, nadie más escribe notas. Fuera de ellas sí, y por eso existe la comparación con [LocalCommit.expected].
 */
class InMemoryLocalSyncStore(val notes: InMemoryNoteStore = InMemoryNoteStore()) : LocalSyncStore {
    private val baseMap = LinkedHashMap<String, SyncBase>()
    private val conflictMap = LinkedHashMap<String, SyncConflict>()
    private val baseNoteMap = LinkedHashMap<String, SyncNote>()

    val bases: Map<String, SyncBase> get() = synchronized(notes) { LinkedHashMap(baseMap) }
    val conflicts: Map<String, SyncConflict> get() = synchronized(notes) { LinkedHashMap(conflictMap) }

    override fun snapshot(): LocalSnapshot = synchronized(notes) {
        LocalSnapshot(notes.all(), notes.tombstones(), LinkedHashMap(baseMap), LinkedHashMap(baseNoteMap))
    }

    override fun bases(): Map<String, SyncBase> = synchronized(notes) { LinkedHashMap(baseMap) }

    override fun commit(commit: LocalCommit): CommitReport = synchronized(notes) {
        val skipped = commit.actions.map { it.noteId }.toSet()
            .filter { currentVersion(it) != commit.expected.getValue(it) }.toSet()

        var appliedNotes = 0
        var appliedTombstones = 0
        var trashed = 0
        var conflictsSaved = 0
        var mergedNotes = 0
        for (action in commit.actions) {
            if (action.noteId in skipped) continue
            when (action) {
                is LocalAction.ApplyNote -> {
                    notes.put(action.note)
                    notes.removeTombstone(action.note.id)
                    appliedNotes++
                }
                is LocalAction.ApplyTombstone -> {
                    notes.removeWithoutTombstone(action.noteId)
                    notes.putTombstone(Tombstone(action.noteId, action.deletedAt, action.deviceId))
                    appliedTombstones++
                }
                is LocalAction.TrashNote -> {
                    val current = notes.get(action.noteId)
                    if (current != null) {
                        notes.put(current.copy(state = NoteState.TRASHED, updatedAt = action.at, syncAuthorDeviceId = null))
                        trashed++
                    }
                }
                is LocalAction.SaveConflict -> {
                    conflictMap[action.noteId] = action.conflict
                    conflictsSaved++
                }
                is LocalAction.ApplyMerged -> {
                    notes.put(action.note)
                    notes.removeTombstone(action.note.id)
                    mergedNotes++
                }
            }
        }
        val bases = commit.newBases.filterKeys { it !in skipped }
        baseMap.putAll(bases)
        baseNoteMap.putAll(commit.newBaseNotes.filterKeys { it !in skipped })
        CommitReport(appliedNotes, appliedTombstones, trashed, conflictsSaved, bases.size, skipped, mergedNotes)
    }

    override fun conflictedNoteIds(): Set<String> = synchronized(notes) { conflictMap.keys.toSet() }

    override fun discardAllLocal() = synchronized(notes) {
        notes.clear()
        baseMap.clear()
        baseNoteMap.clear()
        conflictMap.clear()
    }

    override fun discardTombstones() = synchronized(notes) {
        notes.clearTombstones()
        baseMap.clear()
        baseNoteMap.clear()
        conflictMap.clear()
    }

    override fun forgetSyncState() = synchronized(notes) {
        baseMap.clear()
        baseNoteMap.clear()
        conflictMap.clear()
    }

    private fun currentVersion(noteId: String): ExpectedVersion {
        val note = notes.get(noteId)
        return when {
            note != null -> ExpectedVersion.Existing(note.updatedAt, note.syncAuthorDeviceId)
            notes.tombstones().any { it.noteId == noteId } -> ExpectedVersion.Tombstoned
            else -> ExpectedVersion.Absent
        }
    }
}
