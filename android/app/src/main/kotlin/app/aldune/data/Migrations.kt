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

/**
 * v2 → v3 (varios perfiles): `sync_base` y `sync_conflict` pasan a clave (profileId, noteId). Las filas que había son del único perfil de
 * entonces, que sigue existiendo con el id fijo `default` (ver `LEGACY_PROFILE_ID` en syncclient). SQLite no cambia una clave primaria: se
 * recrea la tabla y se copian las filas.
 */
val MIGRATION_2_3 = object : Migration(2, 3) {
    override fun migrate(db: SupportSQLiteDatabase) {
        db.execSQL("CREATE TABLE `sync_base_new` (`profileId` TEXT NOT NULL, `noteId` TEXT NOT NULL, `updatedAt` TEXT NOT NULL, `deviceId` TEXT NOT NULL, PRIMARY KEY(`profileId`, `noteId`))")
        db.execSQL("INSERT INTO `sync_base_new` (`profileId`, `noteId`, `updatedAt`, `deviceId`) SELECT 'default', `noteId`, `updatedAt`, `deviceId` FROM `sync_base`")
        db.execSQL("DROP TABLE `sync_base`")
        db.execSQL("ALTER TABLE `sync_base_new` RENAME TO `sync_base`")

        db.execSQL(
            "CREATE TABLE `sync_conflict_new` (`profileId` TEXT NOT NULL, `noteId` TEXT NOT NULL, `detectedAt` TEXT NOT NULL, " +
                "`payloadCipher` BLOB NOT NULL, `nonce` BLOB NOT NULL, `tag` BLOB NOT NULL, PRIMARY KEY(`profileId`, `noteId`))",
        )
        db.execSQL(
            "INSERT INTO `sync_conflict_new` (`profileId`, `noteId`, `detectedAt`, `payloadCipher`, `nonce`, `tag`) " +
                "SELECT 'default', `noteId`, `detectedAt`, `payloadCipher`, `nonce`, `tag` FROM `sync_conflict`",
        )
        db.execSQL("DROP TABLE `sync_conflict`")
        db.execSQL("ALTER TABLE `sync_conflict_new` RENAME TO `sync_conflict`")
    }
}

/** v3 → v4: el nombre visible de las notas protegidas (en claro, opcional). Las filas que había quedan con `NULL`: «Nota protegida», como antes. */
val MIGRATION_3_4 = object : Migration(3, 4) {
    override fun migrate(db: SupportSQLiteDatabase) {
        db.execSQL("ALTER TABLE `note` ADD COLUMN `protectedTitle` TEXT")
    }
}
