package app.aldune.text

import java.time.Instant
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class NoteSyncSignalTest {
    private val t1 = Instant.parse("2026-10-25T10:00:00Z")
    private val t2 = Instant.parse("2026-10-25T11:00:00Z")

    @Test fun `sin sync configurada no se enseña nada`() {
        assertEquals(NoteSyncSignal.State.HIDDEN, NoteSyncSignal.of(false, false, t1, t1))
    }

    @Test fun `la base igual a la nota es sincronizada`() {
        assertEquals(NoteSyncSignal.State.SYNCED, NoteSyncSignal.of(true, false, t1, t1))
    }

    @Test fun `sin base o con base distinta esta pendiente`() {
        assertEquals(NoteSyncSignal.State.PENDING, NoteSyncSignal.of(true, false, null, t1))
        assertEquals(NoteSyncSignal.State.PENDING, NoteSyncSignal.of(true, false, t1, t2))
    }

    @Test fun `un conflicto gana a todo lo demas`() {
        assertEquals(NoteSyncSignal.State.CONFLICT, NoteSyncSignal.of(true, true, t1, t1))
    }

    @Test fun `los glifos son los de Windows`() {
        assertEquals("▂▄▆█", NoteSyncSignal.glyph(NoteSyncSignal.State.SYNCED))
        assertEquals("▂▄▆_", NoteSyncSignal.glyph(NoteSyncSignal.State.PENDING))
        assertEquals("▂▄!_", NoteSyncSignal.glyph(NoteSyncSignal.State.CONFLICT))
        assertEquals("", NoteSyncSignal.glyph(NoteSyncSignal.State.HIDDEN))
    }

    @Test fun `la piel decide el valor por defecto de esquinas y senal`() {
        // Como AppSkin de Windows: rectas en XP oscuro y en los de terminal; señal solo en los de terminal.
        for (m in listOf(AppPalette.XP_DARK, AppPalette.TELECOM_LIGHT, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR)) {
            assertTrue(AspectSkin.squareCorners(m), "esquinas $m")
        }
        for (m in listOf(AppPalette.DARK, AppPalette.LIGHT, AppPalette.SYSTEM, AppPalette.PASTEL, AppPalette.MIDNIGHT, AppPalette.XP_LIGHT)) {
            assertFalse(AspectSkin.squareCorners(m), "esquinas $m")
            assertFalse(AspectSkin.syncSignal(m), "señal $m")
        }
        assertFalse(AspectSkin.syncSignal(AppPalette.XP_DARK))
        for (m in listOf(AppPalette.TELECOM_LIGHT, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR)) assertTrue(AspectSkin.syncSignal(m), "señal $m")
    }
}
