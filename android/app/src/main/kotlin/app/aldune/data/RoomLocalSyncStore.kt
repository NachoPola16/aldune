package app.aldune.data

import app.aldune.core.SyncNote
import app.aldune.domain.EncryptedText
import app.aldune.domain.NoteCipher
import app.aldune.domain.NoteState
import app.aldune.sync.SyncBase
import app.aldune.sync.SyncConflict
import app.aldune.syncclient.CommitReport
import app.aldune.syncclient.ExpectedVersion
import app.aldune.syncclient.LocalAction
import app.aldune.syncclient.LocalCommit
import app.aldune.syncclient.LocalSnapshot
import app.aldune.syncclient.LocalSyncStore
import java.time.OffsetDateTime

/**
 * `LocalSyncStore` sobre la misma base que [RoomNoteStore]: una sola base para las notas y para la sync, y cada operación en **una**
 * transacción de Room (`runInTransaction` serializa las escrituras: mientras dura, el editor espera). Por eso aquí nunca hay red.
 * Pasa la misma batería (`LocalSyncStoreContract`) que el almacén en memoria.
 */
class RoomLocalSyncStore(
    private val database: AldunaDatabase,
    private val notes: RoomNoteStore,
    private val cipher: NoteCipher,
    /** El perfil de esta vista: bases y conflictos son suyos. Notas y lápidas son de todos. */
    private val profileId: String,
    /** El historial local de versiones (opcional): lo que la sync va a pisar se guarda antes. */
    private val history: app.aldune.domain.NoteHistory? = null,
) : LocalSyncStore {
    private val noteDao = database.notes()
    private val syncDao = database.sync()

    override fun snapshot(): LocalSnapshot = database.runInTransaction<LocalSnapshot> {
        LocalSnapshot(
            notes.all(),
            notes.tombstones(),
            bases(),
            baseNotes(),
        )
    }

    /** El contenido de la última versión acordada de cada nota de este perfil. Una fila que no se descifra solo significa «sin base»: esa nota cae al flujo del conflicto. */
    private fun baseNotes(): Map<String, SyncNote> {
        val result = LinkedHashMap<String, SyncNote>()
        for (row in syncDao.bases(profileId)) {
            val cipherText = row.baseCipher ?: continue
            val nonce = row.baseNonce ?: continue
            val tag = row.baseTag ?: continue
            try {
                result[row.noteId] = ConflictPayload.readNote(cipher.decrypt("base:${row.noteId}", EncryptedText(cipherText, nonce, tag)))
            } catch (e: Exception) {
                // Sin copia legible: la fusión no se intenta.
            }
        }
        return result
    }

    override fun bases(): Map<String, SyncBase> =
        syncDao.bases(profileId).associate { it.noteId to SyncBase(OffsetDateTime.parse(it.updatedAt), it.deviceId) }

    override fun commit(commit: LocalCommit): CommitReport = database.runInTransaction<CommitReport> {
        // Dentro de la transacción nadie escribe entre la comparación y la aplicación. Una versión distinta de la que vio la foto
        // significa que el usuario editó mientras la sync hablaba con el servidor (concurrencia optimista, ver LocalCommit).
        val skipped = commit.actions.map { it.noteId }.toSet().filter { currentVersion(it) != commit.expected.getValue(it) }.toSet()

        var appliedNotes = 0
        var appliedTombstones = 0
        var trashed = 0
        var conflictsSaved = 0
        var mergedNotes = 0
        for (action in commit.actions) {
            if (action.noteId in skipped) continue
            when (action) {
                is LocalAction.ApplyNote -> {
                    notes.get(action.note.id)?.let { local ->
                        history?.keepBeforeOverwrite(local.id, local.text, local.isProtected, action.note.text, action.note.isProtected, java.time.Instant.now())
                    }
                    notes.put(action.note)
                    noteDao.deleteTombstone(action.note.id)
                    appliedNotes++
                }
                is LocalAction.ApplyTombstone -> {
                    noteDao.delete(action.noteId)
                    noteDao.putTombstone(TombstoneEntity(action.noteId, action.deletedAt.toString(), action.deviceId))
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
                    syncDao.putConflict(toEntity(action.conflict))
                    conflictsSaved++
                }
                is LocalAction.ApplyMerged -> {
                    // Neither side's text may be lost to a merge: the local one and the other side's both go to the history (no 5-minute rule).
                    val now = java.time.Instant.now()
                    notes.get(action.note.id)?.let { local ->
                        history?.keepBeforeOverwrite(local.id, local.text, local.isProtected, action.note.text, false, now)
                    }
                    history?.keepBeforeOverwrite(action.note.id, action.otherText, false, action.note.text, false, now)
                    notes.put(action.note)
                    noteDao.deleteTombstone(action.note.id)
                    mergedNotes++
                }
            }
        }
        val bases = commit.newBases.filterKeys { it !in skipped }
        val keptNotes = commit.newBaseNotes.filterKeys { it !in skipped }
        for ((id, base) in bases) putBaseRow(id, base, keptNotes[id])
        // A note whose base was already right but had no content stored yet: keep the base and add the content.
        for ((id, content) in keptNotes) {
            if (id in bases) continue
            val existing = syncDao.base(profileId, id) ?: continue
            putBaseRow(id, SyncBase(OffsetDateTime.parse(existing.updatedAt), existing.deviceId), content)
        }
        CommitReport(appliedNotes, appliedTombstones, trashed, conflictsSaved, bases.size, skipped, mergedNotes)
    }

    private fun putBaseRow(noteId: String, base: SyncBase, content: SyncNote?) {
        val encrypted = content?.let { cipher.encrypt("base:$noteId", ConflictPayload.writeNote(it)) }
        syncDao.putBase(
            SyncBaseEntity(profileId, noteId, base.updatedAt.toString(), base.deviceId, encrypted?.cipherText, encrypted?.nonce, encrypted?.tag),
        )
    }

    override fun conflictedNoteIds(): Set<String> = syncDao.conflictedIds(profileId).toSet()

    override fun discardAllLocal() = database.runInTransaction {
        noteDao.clearNotes()
        noteDao.clearTombstones()
        // Solo lo llama el primer perfil (ver firstConnectDecision): borra las bases y conflictos de todos, que ya no tienen notas a las que referirse.
        syncDao.clearAllBases()
        syncDao.clearAllConflicts()
        Unit
    }

    override fun discardTombstones() = database.runInTransaction {
        noteDao.clearTombstones()
        syncDao.clearAllBases()
        syncDao.clearAllConflicts()
        Unit
    }

    override fun forgetSyncState() = database.runInTransaction {
        syncDao.clearBases(profileId)
        syncDao.clearConflicts(profileId)
        Unit
    }

    private fun currentVersion(noteId: String): ExpectedVersion {
        val note = notes.get(noteId)
        return when {
            note != null -> ExpectedVersion.Existing(note.updatedAt, note.syncAuthorDeviceId)
            noteDao.tombstones().any { it.noteId == noteId } -> ExpectedVersion.Tombstoned
            else -> ExpectedVersion.Absent
        }
    }

    private fun toEntity(conflict: SyncConflict): SyncConflictEntity {
        val id = conflict.noteId.toString()
        // El id va como dato asociado, con un prefijo: así una fila de conflicto no descifra como si fuera el texto de la nota.
        val encrypted = cipher.encrypt("conflict:$id", ConflictPayload.write(conflict))
        return SyncConflictEntity(profileId, id, conflict.detectedAt.toString(), encrypted.cipherText, encrypted.nonce, encrypted.tag)
    }
}
