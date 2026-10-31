package app.aldune.data

import androidx.room.ColumnInfo
import androidx.room.Dao
import androidx.room.Entity
import androidx.room.Index
import androidx.room.Insert
import androidx.room.PrimaryKey
import androidx.room.Query

/** Una versión anterior del texto de una nota (v6). Cifrada con la misma clave y el mismo id de nota como AAD que `note.textCipher`. */
@Entity(tableName = "note_version", indices = [Index("noteId", "id")])
class NoteVersionEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val noteId: String,
    val savedAt: String,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val textCipher: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val nonce: ByteArray,
    @ColumnInfo(typeAffinity = ColumnInfo.BLOB) val tag: ByteArray,
)

@Dao
interface VersionDao {
    @Insert
    fun add(version: NoteVersionEntity)

    @Query("SELECT * FROM note_version WHERE noteId = :noteId ORDER BY id DESC")
    fun list(noteId: String): List<NoteVersionEntity>

    @Query("DELETE FROM note_version WHERE noteId = :noteId AND id NOT IN (SELECT id FROM note_version WHERE noteId = :noteId ORDER BY id DESC LIMIT :keep)")
    fun trim(noteId: String, keep: Int)

    @Query("DELETE FROM note_version WHERE noteId = :noteId")
    fun deleteAll(noteId: String)
}
