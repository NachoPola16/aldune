package app.aldune.data

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

/**
 * La base de Aldune. `exportSchema = true` y el esquema se versiona en `app/schemas/`: cada cambio lleva su migración probada.
 * `sync_conflict` no está en la v1: su forma depende del motor de sync (F5) y entrará con su migración.
 */
@Database(
    entities = [NoteEntity::class, TombstoneEntity::class, SyncBaseEntity::class, ReminderEntity::class],
    version = 1,
    exportSchema = true,
)
abstract class AldunaDatabase : RoomDatabase() {
    abstract fun notes(): NoteDao

    companion object {
        const val FILE_NAME = "aldune.db"

        fun open(context: Context): AldunaDatabase =
            Room.databaseBuilder(context.applicationContext, AldunaDatabase::class.java, FILE_NAME).build()
    }
}
