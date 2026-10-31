package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

/** El widget enseña cuántas tareas van hechas y si la nota está fijada; de una protegida, nunca nada. */
class WidgetTaskProgressTest {
    private fun note(text: String, pinned: Boolean = false, protectedNote: Boolean = false) =
        Note(id = "n", text = text, lineEnding = LineEnding.LF, color = "#FFDDD4", state = NoteState.ACTIVE, createdAt = java.time.Instant.EPOCH, updatedAt = java.time.Instant.EPOCH,
            isProtected = protectedNote, protectedContentJson = if (protectedNote) "{}" else null, pinnedToTop = pinned)

    private fun open(n: Note) = WidgetContentBuilder.build(listOf(n), WidgetOptions("x")).strips.single() as WidgetStrip.Open

    @Test fun `una lista de tareas lleva sus hechas y su total`() {
        val strip = open(note("Compra\n☒ leche\n☐ pan\n☐ huevos"))
        assertEquals(1, strip.tasksDone)
        assertEquals(3, strip.tasksTotal)
    }

    @Test fun `una nota sin tareas no lleva progreso`() {
        val strip = open(note("Solo texto"))
        assertEquals(0, strip.tasksTotal)
    }

    @Test fun `el pin viaja a la tira`() {
        assertTrue(open(note("a", pinned = true)).pinned)
        assertEquals(false, open(note("a")).pinned)
    }

    @Test fun `una protegida solo conserva el color y el nombre y nada de tareas`() {
        val strip = WidgetContentBuilder.build(listOf(note("☒ secreto", protectedNote = true)), WidgetOptions("x")).strips.single()
        assertTrue(strip is WidgetStrip.Locked)
    }
}
