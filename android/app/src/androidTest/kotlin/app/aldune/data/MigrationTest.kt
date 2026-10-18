package app.aldune.data

import androidx.room.testing.MigrationTestHelper
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Infraestructura de migraciones desde el primer día: cada versión del esquema exportado en `app/schemas/` se abre y se valida.
 * La 1 → 2 (F5, `sync_conflict`) se prueba con datos de por medio: no debe tocar ninguna fila existente.
 */
@RunWith(AndroidJUnit4::class)
class MigrationTest {
    @get:Rule val helper = MigrationTestHelper(InstrumentationRegistry.getInstrumentation(), AldunaDatabase::class.java)

    @Test fun version1OpensAndValidates() {
        helper.createDatabase("migration-test", 1).use { db ->
            db.execSQL("INSERT INTO tombstone (noteId, deletedAt, deviceId) VALUES ('x', '2026-10-17T10:00:00Z', 'local')")
        }
        helper.runMigrationsAndValidate("migration-test", 1, true).use { }
    }

    @Test fun migration1To2KeepsRowsAndAddsConflictTable() {
        helper.createDatabase("migration-12", 1).use { db ->
            db.execSQL("INSERT INTO tombstone (noteId, deletedAt, deviceId) VALUES ('x', '2026-10-17T10:00:00Z', 'local')")
            db.execSQL("INSERT INTO sync_base (noteId, updatedAt, deviceId) VALUES ('x', '2026-10-17T10:00:00Z', 'pc')")
        }
        helper.runMigrationsAndValidate("migration-12", 2, true, MIGRATION_1_2).use { db ->
            db.query("SELECT deviceId FROM sync_base WHERE noteId = 'x'").use { c ->
                assertTrue(c.moveToFirst())
                assertEquals("pc", c.getString(0))
            }
            db.query("SELECT count(*) FROM tombstone").use { c ->
                c.moveToFirst()
                assertEquals(1, c.getInt(0))
            }
            db.execSQL("INSERT INTO sync_conflict (noteId, detectedAt, payloadCipher, nonce, tag) VALUES ('x', 't', x'00', x'00', x'00')")
        }
    }
}
