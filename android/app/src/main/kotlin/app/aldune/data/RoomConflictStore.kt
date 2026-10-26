package app.aldune.data

import app.aldune.domain.EncryptedText
import app.aldune.domain.NoteCipher
import app.aldune.sync.SyncConflict
import app.aldune.syncclient.ConflictStore

/**
 * `ConflictStore` sobre Room: los conflictos que guardó la sync, descifrados al leerlos. Mismo cifrado y mismo dato asociado
 * (`conflict:<id>`) que usa `RoomLocalSyncStore` al escribirlos; aquí solo se leen y se descartan.
 *
 * El contrato es «uno por nota», pero en Room hay uno por (perfil, nota): si dos perfiles tienen conflicto de la misma nota se enseña el
 * más reciente, y resolverlo borra **solo ese**. El del otro perfil aparece a continuación: así no se pierde en silencio una versión
 * perdedora distinta (decisión del 26-10, spec de varios perfiles §7).
 */
class RoomConflictStore(database: AldunaDatabase, private val cipher: NoteCipher) : ConflictStore {
    private val dao = database.sync()

    // Mismo orden que `SyncDao.latestConflict`: el más reciente y, a igual fecha, el perfil de id menor.
    private val newestFirst = compareByDescending<SyncConflictEntity> { it.detectedAt }.thenBy { it.profileId }

    override fun all(): List<SyncConflict> =
        dao.conflicts().groupBy { it.noteId }.values.map { rows -> rows.sortedWith(newestFirst).first() }.map(::toConflict)

    override fun get(noteId: String): SyncConflict? = dao.latestConflict(noteId)?.let(::toConflict)

    override fun delete(noteId: String) {
        dao.latestConflict(noteId)?.let { dao.deleteConflict(it.profileId, it.noteId) }
    }

    private fun toConflict(row: SyncConflictEntity): SyncConflict = ConflictPayload.read(
        row.noteId, row.detectedAt,
        cipher.decrypt("conflict:${row.noteId}", EncryptedText(row.payloadCipher, row.nonce, row.tag)),
    )
}
