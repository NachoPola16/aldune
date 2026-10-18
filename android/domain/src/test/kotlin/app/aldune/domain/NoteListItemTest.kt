package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class NoteListItemTest {
    private fun note(text: String, tags: List<String> = emptyList()) = Note(
        id = "n1", text = text, lineEnding = LineEnding.LF, color = "#EBD38B", createdAt = Instant.EPOCH, updatedAt = Instant.EPOCH,
        state = NoteState.ACTIVE, tags = tags,
    )

    @Test
    fun `titulo sin almohadillas, vista previa sin casillas y contador de tareas`() {
        val item = NoteListItem.from(note("# Compra\n☒ leche\n☐ pan\n→ nota suelta", listOf("casa")), "Nota nueva")

        assertEquals("Compra", item.title)
        assertEquals("leche pan → nota suelta", item.preview)
        assertEquals(1, item.tasksDone)
        assertEquals(2, item.tasksTotal)
        assertTrue(item.hasTasks)
        assertEquals(listOf("casa"), item.tags)
    }

    @Test
    fun `nota de una sola linea no tiene vista previa ni tareas`() {
        val item = NoteListItem.from(note("solo titulo"), "Nota nueva")

        assertEquals("solo titulo", item.title)
        assertEquals("", item.preview)
        assertFalse(item.hasTasks)
    }

    @Test
    fun `una nota sin texto usa el titulo de relleno que se le pase`() {
        assertEquals("Nueva nota", NoteListItem.from(note("  \n  "), "Nueva nota").title)
    }

    @Test
    fun `una nota protegida sale con el titulo de relleno, sin vista previa ni tareas`() {
        val protected = note("", listOf("casa")).copy(isProtected = true, protectedContentJson = "{}")
        val item = NoteListItem.from(protected, "Nota nueva", protectedTitle = "Nota protegida")

        assertTrue(item.isProtected)
        assertEquals("Nota protegida", item.title)
        assertEquals("", item.preview)
        assertFalse(item.hasTasks)
        assertEquals(listOf("casa"), item.tags)
    }

    @Test
    fun `una nota normal no es protegida`() {
        assertFalse(NoteListItem.from(note("hola"), "Nota nueva", protectedTitle = "Nota protegida").isProtected)
    }
}
