package app.aldune.data

import app.aldune.domain.EncryptedText
import app.aldune.domain.NoteCipher
import app.aldune.sync.SyncConflict
import app.aldune.syncclient.ConflictStore

/**
 * `ConflictStore` sobre Room: los conflictos que guardó la sync, descifrados al leerlos. Mismo cifrado y mismo dato asociado
 * (`conflict:<id>`) que usa `RoomLocalSyncStore` al escribirlos; aquí solo se leen y se descartan.
 */
class RoomConflictStore(database: AldunaDatabase, private val cipher: NoteCipher) : ConflictStore {
    private val dao = database.sync()

    override fun all(): List<SyncConflict> = dao.conflicts().map(::toConflict)

    override fun get(noteId: String): SyncConflict? = dao.conflict(noteId)?.let(::toConflict)

    override fun delete(noteId: String) = dao.deleteConflict(noteId)

    private fun toConflict(row: SyncConflictEntity): SyncConflict = ConflictPayload.read(
        row.noteId, row.detectedAt,
        cipher.decrypt("conflict:${row.noteId}", EncryptedText(row.payloadCipher, row.nonce, row.tag)),
    )
}
