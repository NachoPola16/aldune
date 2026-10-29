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

    @Test fun migration2To3MovesRowsToTheLegacyProfile() {
        helper.createDatabase("migration-23", 2).use { db ->
            db.execSQL("INSERT INTO sync_base (noteId, updatedAt, deviceId) VALUES ('x', '2026-10-17T10:00:00Z', 'pc')")
            db.execSQL("INSERT INTO sync_conflict (noteId, detectedAt, payloadCipher, nonce, tag) VALUES ('x', 't', x'01', x'02', x'03')")
        }
        helper.runMigrationsAndValidate("migration-23", 3, true, MIGRATION_2_3).use { db ->
            db.query("SELECT profileId, deviceId FROM sync_base WHERE noteId = 'x'").use { c ->
                assertTrue(c.moveToFirst())
                assertEquals("default", c.getString(0))
                assertEquals("pc", c.getString(1))
            }
            db.query("SELECT profileId, detectedAt FROM sync_conflict WHERE noteId = 'x'").use { c ->
                assertTrue(c.moveToFirst())
                assertEquals("default", c.getString(0))
                assertEquals("t", c.getString(1))
            }
            // La misma nota ya puede tener base en otro perfil.
            db.execSQL("INSERT INTO sync_base (profileId, noteId, updatedAt, deviceId) VALUES ('otro', 'x', 't', 'pc')")
        }
    }

    @Test fun migration3To4AddsTheVisibleNameAndKeepsRows() {
        helper.createDatabase("migration-34", 3).use { db ->
            db.execSQL(
                "INSERT INTO note (id, textCipher, nonce, tag, color, createdAt, updatedAt, state, lineEnding, tags, dockPosition, " +
                    "screenOrigin, syncAuthorDeviceId, isProtected, protectedContent) VALUES ('x', x'00', x'00', x'00', '#EBD38B', 't', 't', 0, 0, '[]', " +
                    "NULL, '', NULL, 1, '{}')",
            )
        }
        helper.runMigrationsAndValidate("migration-34", 4, true, MIGRATION_3_4).use { db ->
            db.query("SELECT isProtected, protectedTitle FROM note WHERE id = 'x'").use { c ->
                assertTrue(c.moveToFirst())
                assertEquals(1, c.getInt(0))
                assertTrue(c.isNull(1))
            }
            db.execSQL("UPDATE note SET protectedTitle = 'Banco' WHERE id = 'x'")
        }
    }
}
