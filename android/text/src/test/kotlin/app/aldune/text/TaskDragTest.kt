package app.aldune.text

import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Test

class TaskDragTest {
    @Test fun `la linea de un desplazamiento cuenta los saltos anteriores`() {
        val text = "a\nbb\nccc"
        assertEquals(0, TaskDrag.lineOf(text, 0))
        assertEquals(0, TaskDrag.lineOf(text, 1))
        assertEquals(1, TaskDrag.lineOf(text, 2))
        assertEquals(2, TaskDrag.lineOf(text, text.length))
    }

    @Test fun `el inicio de una linea y el numero de lineas`() {
        val text = "a\nbb\nccc"
        assertEquals(0, TaskDrag.lineStart(text, 0))
        assertEquals(2, TaskDrag.lineStart(text, 1))
        assertEquals(5, TaskDrag.lineStart(text, 2))
        assertEquals(3, TaskDrag.lineCount(text))
        assertEquals(1, TaskDrag.lineCount(""))
    }

    @Test fun `sin tareas ocultas la linea visible es la real`() {
        val raw = "☐ a\n☐ b\n☐ c"
        assertEquals(2, TaskDrag.rawLine(raw, emptyList(), raw, shownLine = 2))
    }

    @Test fun `con una tarea oculta en medio las siguientes se corren una linea`() {
        val raw = "☐ a\n☒ hecha\n☐ b\n☐ c"
        val hidden = listOf(HiddenTasks.Span(4, 4 + "☒ hecha\n".length))
        val shown = "☐ a\n☐ b\n☐ c"
        assertEquals(0, TaskDrag.rawLine(shown, hidden, raw, 0))
        assertEquals(2, TaskDrag.rawLine(shown, hidden, raw, 1))
        assertEquals(3, TaskDrag.rawLine(shown, hidden, raw, 2))
    }

    @Test fun `una linea visible que no existe se queda en la ultima`() {
        val raw = "☐ a\n☐ b"
        assertEquals(1, TaskDrag.rawLine(raw, emptyList(), raw, shownLine = 9))
    }
}
