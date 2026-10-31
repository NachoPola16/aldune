package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class TaskGluedSignTest {
    @Test fun `un signo pegado a la casilla sigue siendo tarea`() {
        for (line in listOf("☐-", "☐- comprar", "☒.", "    ☐—", "→ ☐-")) assertTrue(TaskLines.isTaskLine(line), line)
    }

    @Test fun `una palabra o otra casilla pegada a la casilla la deja en texto`() {
        for (line in listOf("☐comprar", "☐1", "☐☐", "☐")) assertFalse(TaskLines.isTaskLine(line), line)
    }

    @Test fun `la casilla con guion pegado se marca`() {
        assertEquals("☒-", TaskLines.toggleCheckboxAt("☐-", 0))
    }

    @Test fun `un marcador de lista delante de la casilla no impide que sea tarea`() {
        assertEquals(2, TaskLines.glyphIndex("- ☐ comprar"))
        assertEquals(2, TaskLines.glyphIndex("• ☒ hecho"))
        assertEquals(4, TaskLines.glyphIndex("  * ☐ x"))
        assertEquals(3, TaskLines.glyphIndex("1. ☐ x"))
        assertEquals(4, TaskLines.glyphIndex("12) ☐ x"))
        assertEquals(2, TaskLines.glyphIndex("- ☐-"))
    }

    @Test fun `otra cosa delante de la casilla la deja en texto`() {
        for (line in listOf("-☐ x", "- - ☐ x", "-x ☐ x", "hola ☐ x", "1234. ☐ x", "1.☐ x")) assertFalse(TaskLines.isTaskLine(line), line)
    }

    @Test fun `el cursor entre la casilla y su espacio pasa al otro lado`() {
        assertEquals(2, TaskLines.clampCaret("☐ x", 1))
        assertEquals(4, TaskLines.clampCaret("- ☐ x", 3))
        assertEquals(4, TaskLines.clampCaret("→ ☐ x", 3))
        assertEquals(4, TaskLines.clampCaret("  ☒ x", 3))
        assertEquals(2, TaskLines.clampCaret("☐ ", 1))
    }

    @Test fun `cualquier otra posicion del cursor se deja como esta`() {
        for ((line, caret) in listOf("☐ x" to 0, "☐ x" to 2, "☐ x" to 3, "- ☐ x" to 2, "- ☐ x" to 1, "☐x" to 1, "texto" to 3)) {
            assertEquals(caret, TaskLines.clampCaret(line, caret), "$line @ $caret")
        }
    }

    @Test fun `con la flecha izquierda el cursor salta delante de la casilla`() {
        assertEquals(0, TaskLines.clampCaret("☐ x", 1, movingLeft = true))
        assertEquals(2, TaskLines.clampCaret("- ☐ x", 3, movingLeft = true))
    }

    @Test fun `solo se mira la linea del cursor`() {
        assertEquals(6, TaskLines.clampCaret("☐ a\n☐ b", 5))
        assertEquals(1, TaskLines.clampCaret("x\n☐ b", 1))
    }

    @Test fun `una tarea con un signo pegado recibe su espacio`() {
        assertEquals(TextAndCaret("☐ -", 3), TaskLines.ensureGlyphSpace("☐-", 2))
        assertEquals(TextAndCaret("- ☐ -x", 0), TaskLines.ensureGlyphSpace("- ☐-x", 0))
    }

    @Test fun `si no hay nada que arreglar el texto no cambia`() {
        for (line in listOf("☐ x", "☐comprar", "☐", "hola")) assertEquals(TextAndCaret(line, 1), TaskLines.ensureGlyphSpace(line, 1), line)
    }
}
