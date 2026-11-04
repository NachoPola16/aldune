package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

/** The widget can list a note's pending tasks (to tick them there) and can be limited to one tag. */
class WidgetTasksAndTagTest {
    private fun note(id: String, text: String, tags: List<String> = emptyList(), protectedNote: Boolean = false) =
        Note(id = id, text = text, lineEnding = LineEnding.LF, color = "#FFDDD4", state = NoteState.ACTIVE, createdAt = java.time.Instant.EPOCH, updatedAt = java.time.Instant.EPOCH,
            isProtected = protectedNote, protectedContentJson = if (protectedNote) "{}" else null, tags = tags)

    private fun open(n: Note, tasks: Int) =
        WidgetContentBuilder.build(listOf(n), WidgetOptions("x", tasksPerNote = tasks)).strips.single() as WidgetStrip.Open

    @Test fun `por defecto la tira no lleva tareas`() {
        assertTrue(open(note("n", "Compra\n☐ pan"), tasks = 0).tasks.isEmpty())
    }

    @Test fun `lleva las pendientes en orden con su indice y su linea y salta las hechas`() {
        val text = "Compra\n☒ leche\n☐ pan\n☐ huevos"
        val strip = open(note("n", text), tasks = 3)

        assertEquals(listOf("pan", "huevos"), strip.tasks.map { it.text })
        assertEquals(listOf("☐ pan", "☐ huevos"), strip.tasks.map { it.line })
        assertEquals(listOf(text.indexOf("☐ pan"), text.indexOf("☐ huevos")), strip.tasks.map { it.index })
    }

    @Test fun `respeta el tope por nota`() {
        val strip = open(note("n", "☐ a\n☐ b\n☐ c\n☐ d"), tasks = 2)
        assertEquals(listOf("a", "b"), strip.tasks.map { it.text })
    }

    @Test fun `el total de tareas del widget tiene tope, las primeras notas las llevan y las demas se quedan sin casillas`() {
        // With thousands of notes, 50 strips x 3 task rows made the widget's view tree bigger than the 1 MB the launcher accepts
        // (TransactionTooLargeException, "Can't show content"). The number of strips stays; the task rows are budgeted across them.
        val notes = (1..30).map { note("n$it", "Nota $it\n☐ a\n☐ b\n☐ c") }
        val content = WidgetContentBuilder.build(notes, WidgetOptions("x", tasksPerNote = 3, taskBudget = 10))
        val perStrip = content.strips.map { (it as WidgetStrip.Open).tasks.size }

        assertEquals(10, perStrip.sum())
        assertEquals(listOf(3, 3, 3, 1), perStrip.take(4))
        assertTrue(perStrip.drop(4).all { it == 0 })
        assertEquals(30, content.strips.size)
    }

    @Test fun `con casillas el widget enseña menos tiras, y sin casillas conserva el tope de siempre`() {
        // Measured on an emulator with ~2000 notes: a strip costs ~12 kB of the launcher's ~1 MB limit and a task row ~9 kB, so the strips are
        // capped lower when the tasks are on (50 strips + 40 task rows was 940 kB and still refused).
        val notes = (1..80).map { note("n$it", "Nota $it\n☐ a") }
        val withTasks = WidgetContentBuilder.build(notes, WidgetOptions("x", tasksPerNote = 3))
        assertEquals(WidgetOptions.TASK_MODE_LIMIT, withTasks.strips.size)
        assertEquals(80 - WidgetOptions.TASK_MODE_LIMIT, withTasks.hiddenCount)
        assertEquals(WidgetOptions.DEFAULT_TASK_BUDGET, withTasks.strips.sumOf { (it as WidgetStrip.Open).tasks.size })

        val withoutTasks = WidgetContentBuilder.build(notes, WidgetOptions("x"))
        assertEquals(WidgetOptions.DEFAULT_LIMIT, withoutTasks.strips.size)
    }

    @Test fun `una protegida no lleva tareas`() {
        val strip = WidgetContentBuilder.build(listOf(note("n", "☐ secreto", protectedNote = true)), WidgetOptions("x", tasksPerNote = 3)).strips.single()
        assertTrue(strip is WidgetStrip.Locked)
    }

    @Test fun `con etiqueta solo entran las notas que la llevan, sin distinguir mayusculas`() {
        val notes = listOf(note("a", "A", listOf("Casa")), note("b", "B", listOf("trabajo")), note("c", "C"))
        val content = WidgetContentBuilder.build(notes, WidgetOptions("x", tag = "casa"))
        assertEquals(listOf("a"), content.strips.map { it.id })
        assertEquals(0, content.hiddenCount)
    }

    @Test fun `el contador de ocultas cuenta solo las de la etiqueta`() {
        val notes = (1..4).map { note("n$it", "N$it", listOf("x")) } + note("otra", "O")
        val content = WidgetContentBuilder.build(notes, WidgetOptions("x", limit = 2, tag = "x"))
        assertEquals(2, content.strips.size)
        assertEquals(2, content.hiddenCount)
    }
}
