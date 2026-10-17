package app.aldune.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

@Dao
interface NoteDao {
    @Query("SELECT * FROM note")
    fun all(): List<NoteEntity>

    @Query("SELECT * FROM note WHERE id = :id")
    fun get(id: String): NoteEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun upsert(note: NoteEntity)

    @Query("DELETE FROM note WHERE id = :id")
    fun delete(id: String): Int

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putTombstone(tombstone: TombstoneEntity)

    @Query("SELECT * FROM tombstone")
    fun tombstones(): List<TombstoneEntity>
}
