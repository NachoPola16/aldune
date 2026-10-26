package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class HiddenTasksTest {
    private val hour = 3_600_000L

    private fun keyOf(task: String) = HiddenTasks.key(task)

    @Test fun `la clave no depende de la casilla ni de la sangria`() {
        assertEquals(keyOf("☐ comprar pan"), keyOf("☒ comprar pan"))
        assertEquals(keyOf("☐ comprar pan"), keyOf("  ☑ comprar pan"))
        assertTrue(keyOf("☐ comprar pan") != keyOf("☐ comprar leche"))
    }

    @Test fun `track apunta la hora de una tarea hecha sin registro`() {
        val out = HiddenTasks.track("☒ a\n☐ b", emptyMap(), now = 1000)
        assertEquals(mapOf(keyOf("☒ a") to 1000L), out)
    }

    @Test fun `track conserva la hora que ya tenia`() {
        val before = mapOf(keyOf("☒ a") to 5L)
        assertEquals(before, HiddenTasks.track("☒ a", before, now = 1000))
    }

    @Test fun `track olvida lo desmarcado o borrado`() {
        val before = mapOf(keyOf("☒ a") to 5L, keyOf("☒ b") to 6L)
        assertEquals(mapOf(keyOf("☒ b") to 6L), HiddenTasks.track("☐ a\n☒ b", before, now = 1000))
    }

    @Test fun `solo se oculta la tarea hecha cuyo plazo ya vencio`() {
        val text = "☒ a\n☒ b\n☐ c\n"
        val done = mapOf(keyOf("☒ a") to 0L, keyOf("☒ b") to 90 * 60_000L)
        val spans = HiddenTasks.hiddenSpans(text, done, now = 2 * hour, delayMs = hour)
        assertEquals(listOf(HiddenTasks.Span(0, 4)), spans)
    }

    @Test fun `sin plazo se oculta en cuanto esta apuntada`() {
        val text = "x\n☒ a\ny"
        val spans = HiddenTasks.hiddenSpans(text, mapOf(keyOf("☒ a") to 7L), now = 7L, delayMs = 0)
        assertEquals(listOf(HiddenTasks.Span(2, 6)), spans)
    }

    @Test fun `una tarea hecha sin registro no se oculta`() {
        assertTrue(HiddenTasks.hiddenSpans("☒ a", emptyMap(), now = 99 * hour, delayMs = 0).isEmpty())
    }

    @Test fun `la ultima linea sin salto se lleva el salto de antes`() {
        val spans = HiddenTasks.hiddenSpans("x\n☒ a", mapOf(keyOf("☒ a") to 0L), now = 0, delayMs = 0)
        assertEquals(listOf(HiddenTasks.Span(1, 5)), spans)
    }

    @Test fun `con CRLF el salto entero se oculta`() {
        val spans = HiddenTasks.hiddenSpans("x\r\n☒ a\r\ny", mapOf(keyOf("☒ a") to 0L), now = 0, delayMs = 0)
        assertEquals(listOf(HiddenTasks.Span(3, 8)), spans)
    }

    @Test fun `lineas seguidas se juntan en un solo tramo`() {
        val done = mapOf(keyOf("☒ a") to 0L, keyOf("☒ b") to 0L)
        val spans = HiddenTasks.hiddenSpans("☒ a\n☒ b", done, now = 0, delayMs = 0)
        assertEquals(listOf(HiddenTasks.Span(0, 7)), spans)
    }

    @Test fun `del texto visible al real se suma lo oculto antes`() {
        val spans = listOf(HiddenTasks.Span(2, 6), HiddenTasks.Span(10, 12))
        assertEquals(0, HiddenTasks.toRaw(0, spans))
        assertEquals(1, HiddenTasks.toRaw(1, spans))
        // Justo en el hueco: pasa al texto que sigue.
        assertEquals(6, HiddenTasks.toRaw(2, spans))
        assertEquals(9, HiddenTasks.toRaw(5, spans))
        assertEquals(12, HiddenTasks.toRaw(6, spans))
    }
}
