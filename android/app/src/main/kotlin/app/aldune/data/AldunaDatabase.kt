package app.aldune.data

import android.content.Context
import androidx.room.Database
import androidx.room.Room
import androidx.room.RoomDatabase

/**
 * La base de Aldune. `exportSchema = true` y el esquema se versiona en `app/schemas/`: cada cambio lleva su migración probada.
 * v2 (F5) añade `sync_conflict`, que no estaba en la v1 porque su forma dependía del motor de sync.
 */
@Database(
    entities = [NoteEntity::class, TombstoneEntity::class, SyncBaseEntity::class, SyncConflictEntity::class, ReminderEntity::class],
    version = 2,
    exportSchema = true,
)
abstract class AldunaDatabase : RoomDatabase() {
    abstract fun notes(): NoteDao

    abstract fun sync(): SyncDao

    abstract fun reminders(): ReminderDao

    companion object {
        const val FILE_NAME = "aldune.db"

        fun open(context: Context): AldunaDatabase =
            Room.databaseBuilder(context.applicationContext, AldunaDatabase::class.java, FILE_NAME)
                .addMigrations(MIGRATION_1_2)
                .build()
    }
}
