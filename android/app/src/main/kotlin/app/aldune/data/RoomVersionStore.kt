package app.aldune.data

import app.aldune.domain.EncryptedText
import app.aldune.domain.NoteCipher
import app.aldune.domain.NoteVersion
import app.aldune.domain.VersionStore
import java.time.Instant

/** `VersionStore` sobre Room: el texto va cifrado igual que el de la nota; la base nunca ve una versión en claro. */
class RoomVersionStore(private val database: AldunaDatabase, private val cipher: NoteCipher) : VersionStore {
    private val dao = database.versions()

    override fun add(noteId: String, savedAt: Instant, text: String) {
        val encrypted = cipher.encrypt(noteId, text)
        dao.add(NoteVersionEntity(noteId = noteId, savedAt = savedAt.toString(), textCipher = encrypted.cipherText, nonce = encrypted.nonce, tag = encrypted.tag))
    }

    override fun list(noteId: String): List<NoteVersion> = dao.list(noteId).map {
        NoteVersion(it.id, it.noteId, Instant.parse(it.savedAt), cipher.decrypt(noteId, EncryptedText(it.textCipher, it.nonce, it.tag)))
    }

    override fun trim(noteId: String, keep: Int) = dao.trim(noteId, keep)

    override fun deleteAll(noteId: String) = dao.deleteAll(noteId)
}
