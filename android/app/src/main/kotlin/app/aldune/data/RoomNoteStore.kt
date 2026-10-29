package app.aldune.data

import app.aldune.domain.EncryptedText
import app.aldune.domain.LineEnding
import app.aldune.domain.Note
import app.aldune.domain.NoteCipher
import app.aldune.domain.NoteState
import app.aldune.domain.NoteStore
import app.aldune.domain.Tombstone
import java.time.Instant
import org.json.JSONArray

/**
 * `NoteStore` sobre Room. Es la única pieza que conoce a la vez el esquema y el cifrado: el dominio recibe notas ya descifradas y la
 * base nunca ve el texto en claro. Todo es síncrono y bloqueante: se llama desde un hilo de E/S, nunca desde el de interfaz.
 *
 * Si una fila no descifra (clave perdida, fila manipulada) la excepción sube tal cual: decidir qué mostrar es cosa de quien custodia la
 * clave (ver la spec de F4, §2.1), no de un almacén que no debe tragarse errores de datos.
 */
class RoomNoteStore(private val database: AldunaDatabase, private val cipher: NoteCipher) : NoteStore {
    private val dao = database.notes()

    override fun all(): List<Note> = dao.all().map(::toNote)

    override fun get(id: String): Note? = dao.get(id)?.let(::toNote)

    override fun put(note: Note) = dao.upsert(toEntity(note))

    override fun delete(id: String, deletedAt: Instant): Boolean = database.runInTransaction<Boolean> {
        if (dao.delete(id) == 0) {
            false
        } else {
            dao.putTombstone(TombstoneEntity(id, deletedAt.toString(), Tombstone.LOCAL_DEVICE))
            true
        }
    }

    override fun tombstones(): List<Tombstone> =
        dao.tombstones().map { Tombstone(it.noteId, Instant.parse(it.deletedAt), it.deviceId) }

    private fun toEntity(note: Note): NoteEntity {
        val encrypted = cipher.encrypt(note.id, note.text)
        return NoteEntity(
            id = note.id, textCipher = encrypted.cipherText, nonce = encrypted.nonce, tag = encrypted.tag,
            color = note.color, createdAt = note.createdAt.toString(), updatedAt = note.updatedAt.toString(),
            state = note.state.code, lineEnding = if (note.lineEnding == LineEnding.CRLF) 1 else 0,
            tags = JSONArray(note.tags).toString(), dockPosition = note.dockPosition, screenOrigin = note.screenOrigin,
            syncAuthorDeviceId = note.syncAuthorDeviceId, isProtected = note.isProtected, protectedContent = note.protectedContentJson,
            protectedTitle = note.protectedTitle,
        )
    }

    private fun toNote(row: NoteEntity): Note {
        val tags = JSONArray(row.tags).let { array -> List(array.length()) { array.getString(it) } }
        return Note(
            id = row.id, text = cipher.decrypt(row.id, EncryptedText(row.textCipher, row.nonce, row.tag)),
            lineEnding = if (row.lineEnding == 1) LineEnding.CRLF else LineEnding.LF, color = row.color,
            createdAt = Instant.parse(row.createdAt), updatedAt = Instant.parse(row.updatedAt),
            // Un código desconocido no se adivina: una base de una versión futura no debe leerse como «activa» en silencio.
            state = NoteState.fromCode(row.state) ?: error("Estado de nota desconocido: ${row.state}"),
            tags = tags, dockPosition = row.dockPosition, screenOrigin = row.screenOrigin, syncAuthorDeviceId = row.syncAuthorDeviceId,
            isProtected = row.isProtected, protectedContentJson = row.protectedContent, protectedTitle = row.protectedTitle,
        )
    }
}
