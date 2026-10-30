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

    @Test
    fun `los aspectos de terminal no tiñen la nota entera por defecto`() {
        // Bash lleva el color solo en una franja, Telecom en un tinte de canal y Fósforo no enseña colores: el fondo completo les choca.
        for (m in listOf(AppPalette.TELECOM_LIGHT, AppPalette.TELECOM_DARK, AppPalette.BASH, AppPalette.PHOSPHOR)) assertFalse(AspectSkin.colorFill(m), "fondo $m")
        for (m in listOf(AppPalette.DARK, AppPalette.LIGHT, AppPalette.SYSTEM, AppPalette.PASTEL, AppPalette.MIDNIGHT, AppPalette.XP_LIGHT, AppPalette.XP_DARK)) {
            assertTrue(AspectSkin.colorFill(m), "fondo $m")
        }
    }

    @Test fun `las libretas llevan el color como franja, bloqueado, y su papel`() {
        val notebooks = listOf(AppPalette.TRAVEL_NOTEBOOK_LIGHT, AppPalette.TRAVEL_NOTEBOOK_DARK, AppPalette.FIELD_NOTEBOOK_LIGHT, AppPalette.FIELD_NOTEBOOK_DARK)
        for (m in notebooks) {
            assertFalse(AspectSkin.colorFill(m), "fondo $m")
            assertTrue(AspectSkin.colorLocked(m), "bloqueado $m")
            assertTrue(AspectSkin.stripeCard(m), "franja $m")
            assertTrue(AspectSkin.serif(m), "serifa $m")
            assertFalse(AspectSkin.squareCorners(m), "esquinas $m")
            assertFalse(AspectSkin.syncSignal(m), "señal $m")
        }
        assertEquals(AspectSkin.Paper.FOLDS, AspectSkin.paper(AppPalette.TRAVEL_NOTEBOOK_DARK))
        assertEquals(AspectSkin.Paper.RULED, AspectSkin.paper(AppPalette.FIELD_NOTEBOOK_LIGHT))
        // El cuaderno cuadriculado lleva papel de cuadros y la letra de siempre (sin serifa), como en Windows.
        for (m in listOf(AppPalette.GRID_NOTEBOOK_LIGHT, AppPalette.GRID_NOTEBOOK_DARK)) {
            assertEquals(AspectSkin.Paper.GRID, AspectSkin.paper(m))
            assertTrue(AspectSkin.stripeCard(m) && AspectSkin.colorLocked(m) && !AspectSkin.colorFill(m))
            assertFalse(AspectSkin.serif(m))
        }
        assertEquals(AspectSkin.Paper.NONE, AspectSkin.paper(AppPalette.BASH))
        assertTrue(AspectSkin.colorFill(AppPalette.DARK))
    }
}
