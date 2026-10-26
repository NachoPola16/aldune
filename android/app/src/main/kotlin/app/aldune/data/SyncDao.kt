package app.aldune.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

/** Bases y conflictos de la sync, por perfil. Las notas y las lápidas están en [NoteDao]: son la misma base, y `RoomLocalSyncStore` las toca en una transacción. */
@Dao
interface SyncDao {
    @Query("SELECT * FROM sync_base WHERE profileId = :profileId")
    fun bases(profileId: String): List<SyncBaseEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putBase(base: SyncBaseEntity)

    @Query("DELETE FROM sync_base WHERE profileId = :profileId")
    fun clearBases(profileId: String): Int

    @Query("DELETE FROM sync_base")
    fun clearAllBases(): Int

    @Query("SELECT * FROM sync_conflict")
    fun conflicts(): List<SyncConflictEntity>

    /** El conflicto más reciente de una nota entre todos los perfiles (el que se enseña). */
    @Query("SELECT * FROM sync_conflict WHERE noteId = :id ORDER BY detectedAt DESC, profileId LIMIT 1")
    fun latestConflict(id: String): SyncConflictEntity?

    @Query("DELETE FROM sync_conflict WHERE profileId = :profileId AND noteId = :id")
    fun deleteConflict(profileId: String, id: String)

    @Query("SELECT DISTINCT noteId FROM sync_conflict")
    fun conflictedIds(): List<String>

    @Query("SELECT noteId FROM sync_conflict WHERE profileId = :profileId")
    fun conflictedIds(profileId: String): List<String>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun putConflict(conflict: SyncConflictEntity)

    @Query("DELETE FROM sync_conflict WHERE profileId = :profileId")
    fun clearConflicts(profileId: String): Int

    @Query("DELETE FROM sync_conflict")
    fun clearAllConflicts(): Int
}
