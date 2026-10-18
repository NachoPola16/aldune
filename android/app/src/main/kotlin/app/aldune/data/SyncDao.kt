package app.aldune.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

/** Bases y conflictos de la sync. Las notas y las lápidas están en [NoteDao]: son la misma base, y `RoomLocalSyncStore` las toca en una transacción. */
@Dao
interface SyncDao {
    @Query("SELECT * FROM sync_base")
    fun bases(): List<SyncBaseEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putBase(base: SyncBaseEntity)

    @Query("DELETE FROM sync_base")
    fun clearBases(): Int

    @Query("SELECT * FROM sync_conflict")
    fun conflicts(): List<SyncConflictEntity>

    @Query("SELECT * FROM sync_conflict WHERE noteId = :id")
    fun conflict(id: String): SyncConflictEntity?

    @Query("DELETE FROM sync_conflict WHERE noteId = :id")
    fun deleteConflict(id: String)

    @Query("SELECT noteId FROM sync_conflict")
    fun conflictedIds(): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putConflict(conflict: SyncConflictEntity)

    @Query("DELETE FROM sync_conflict")
    fun clearConflicts(): Int
}
