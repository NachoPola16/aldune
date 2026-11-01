package app.aldune.domain

import java.time.Duration
import kotlin.test.Test
import kotlin.test.assertEquals

class NoteServiceTagManageTest {
    private fun note(f: Fixture, id: String, tags: List<String>, state: NoteState = NoteState.ACTIVE, color: String = "#FFDDD4") {
        f.store.put(Note(id, "text $id", LineEnding.LF, color, f.clock.current, f.clock.current, state, tags = tags, syncAuthorDeviceId = "pc"))
        f.clock.advance(Duration.ofMinutes(1))
    }

    @Test fun `renombrar una etiqueta la cambia en todas las notas, sin distinguir mayusculas, y no toca colores`() {
        val f = Fixture()
        f.service.tagColors = mapOf("nueva" to "#A8E0B4")
        note(f, "a", listOf("Casa", "otra"))
        note(f, "b", listOf("casa"), NoteState.ARCHIVED)
        note(f, "c", listOf("casa"), NoteState.TRASHED)
        note(f, "d", listOf("ideas"))

        assertEquals(3, f.service.renameTag("CASA", "Nueva"))

        assertEquals(listOf("Nueva", "otra"), f.stored("a").tags)
        assertEquals(listOf("Nueva"), f.stored("b").tags)
        assertEquals(listOf("Nueva"), f.stored("c").tags)
        assertEquals(listOf("ideas"), f.stored("d").tags)
        // a rename is not «gaining» a tag: the notes keep the colour they had
        assertEquals("#FFDDD4", f.stored("a").color)
    }

    @Test fun `renombrar a una etiqueta que la nota ya tiene no la duplica`() {
        val f = Fixture()
        note(f, "a", listOf("x", "y"))
        f.service.renameTag("x", "y")
        assertEquals(listOf("y"), f.stored("a").tags)
    }

    @Test fun `un nombre vacio no renombra nada`() {
        val f = Fixture()
        note(f, "a", listOf("x"))
        assertEquals(0, f.service.renameTag("x", "   "))
        assertEquals(listOf("x"), f.stored("a").tags)
    }

    @Test fun `borrar una etiqueta la quita de todas las notas y deja las notas`() {
        val f = Fixture()
        note(f, "a", listOf("Casa", "otra"))
        note(f, "b", listOf("casa"), NoteState.ARCHIVED)
        note(f, "d", listOf("ideas"))

        assertEquals(2, f.service.removeTag("casa"))

        assertEquals(listOf("otra"), f.stored("a").tags)
        assertEquals(emptyList(), f.stored("b").tags)
        assertEquals(NoteState.ARCHIVED, f.stored("b").state)
        assertEquals(listOf("ideas"), f.stored("d").tags)
    }

    @Test fun `borrar la etiqueta y sus notas manda a la papelera las activas y archivadas`() {
        val f = Fixture()
        note(f, "a", listOf("casa"))
        note(f, "b", listOf("Casa"), NoteState.ARCHIVED)
        note(f, "c", listOf("casa"), NoteState.TRASHED)
        note(f, "d", listOf("ideas"))

        assertEquals(2, f.service.trashNotesWithTag("casa"))

        assertEquals(NoteState.TRASHED, f.stored("a").state)
        assertEquals(NoteState.TRASHED, f.stored("b").state)
        assertEquals(NoteState.ACTIVE, f.stored("d").state)
    }
}
