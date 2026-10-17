package app.aldune.data

import androidx.room.testing.MigrationTestHelper
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Rule
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Infraestructura de migraciones desde el primer día: cada versión del esquema exportado en `app/schemas/` se abre y se valida.
 * Cuando exista la v2 se añade aquí la prueba 1 → 2 con datos de por medio.
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
}
