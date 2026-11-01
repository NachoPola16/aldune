package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs

/** Ticking a task from the widget: a normal edit, refused when the text moved since the widget was drawn. */
class NoteServiceToggleTaskTest {
    private val text = "Compra\n☐ pan\n☐ leche"
    private val boxOfLeche = text.indexOf("☐ leche")

    @Test fun `marca la casilla y deja la nota como edicion normal`() {
        val f = Fixture()
        val note = f.create(text)
        f.clock.current = f.clock.current.plusSeconds(60)

        val result = f.service.toggleTask(note.id, boxOfLeche, "☐ leche")

        assertEquals("Compra\n☐ pan\n☒ leche", (result as NoteResult.Ok).value.text)
        assertEquals(f.clock.current, f.stored(note.id).updatedAt)
    }

    @Test fun `una casilla marcada se desmarca`() {
        val f = Fixture()
        val note = f.create("☒ pan")
        val result = f.service.toggleTask(note.id, 0, "☒ pan")
        assertEquals("☐ pan", (result as NoteResult.Ok).value.text)
    }

    @Test fun `si la linea ya no es la que se vio no toca nada`() {
        // The note was edited on another device (or in the app) after the widget was drawn: the stored index may point at another task.
        val f = Fixture()
        val note = f.create("Compra\n☐ huevos\n☐ pan\n☐ leche")
        val writes = f.store.writeCount

        val error = (f.service.toggleTask(note.id, boxOfLeche, "☐ leche") as NoteResult.Err).error

        assertIs<NoteError.InvalidArgument>(error)
        assertEquals(writes, f.store.writeCount)
    }

    @Test fun `un indice que no es una casilla no toca nada`() {
        val f = Fixture()
        val note = f.create(text)
        assertIs<NoteError.InvalidArgument>((f.service.toggleTask(note.id, 2, "☐ pan") as NoteResult.Err).error)
        assertIs<NoteError.InvalidArgument>((f.service.toggleTask(note.id, 999, "☐ pan") as NoteResult.Err).error)
    }

    @Test fun `una nota que no existe, protegida o fuera de las activas no se toca`() {
        val f = Fixture()
        assertEquals(NoteError.NotFound("x"), (f.service.toggleTask("x", 0, "☐ a") as NoteResult.Err).error)

        val protectedNote = f.create("☐ a")
        f.store.put(f.stored(protectedNote.id).copy(isProtected = true, protectedContentJson = "{}"))
        assertEquals(NoteError.Protected(protectedNote.id), (f.service.toggleTask(protectedNote.id, 0, "☐ a") as NoteResult.Err).error)

        val trashed = f.create("☐ b").also { f.service.trash(it.id) }
        assertIs<NoteError.InvalidArgument>((f.service.toggleTask(trashed.id, 0, "☐ b") as NoteResult.Err).error)
        assertEquals("☐ b", f.stored(trashed.id).text)
    }
}
