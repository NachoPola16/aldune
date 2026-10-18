package app.aldune.data

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query

/** Los recordatorios locales (F7). La tabla ya existía desde la v1 del esquema: esto no cambia la base. */
@Dao
interface ReminderDao {
    @Query("SELECT * FROM reminder WHERE noteId = :id")
    fun get(id: String): ReminderEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    fun put(reminder: ReminderEntity)

    @Query("DELETE FROM reminder WHERE noteId = :id")
    fun delete(id: String)

    @Query("SELECT * FROM reminder")
    fun all(): List<ReminderEntity>
}
