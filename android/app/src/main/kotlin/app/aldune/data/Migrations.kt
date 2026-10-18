package app.aldune.data

import androidx.room.migration.Migration
import androidx.sqlite.db.SupportSQLiteDatabase

/** v1 → v2 (F5): la tabla de conflictos de la sync. No toca ninguna fila existente. */
val MIGRATION_1_2 = object : Migration(1, 2) {
    override fun migrate(db: SupportSQLiteDatabase) {
        db.execSQL(
            "CREATE TABLE IF NOT EXISTS `sync_conflict` (`noteId` TEXT NOT NULL, `detectedAt` TEXT NOT NULL, " +
                "`payloadCipher` BLOB NOT NULL, `nonce` BLOB NOT NULL, `tag` BLOB NOT NULL, PRIMARY KEY(`noteId`))",
        )
    }
}
