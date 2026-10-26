package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs

class NoteServiceAppendTaskTest {
    @Test fun `anade el articulo a la lista y deja la nota como edicion normal`() {
        val f = Fixture()
        val note = f.create("Compra\n☐ pan")
        f.clock.current = f.clock.current.plusSeconds(60)

        val result = f.service.appendTask(note.id, "leche")

        assertEquals("Compra\n☐ pan\n☐ leche", (result as NoteResult.Ok).value.text)
        assertEquals("Compra\n☐ pan\n☐ leche", f.stored(note.id).text)
        assertEquals(f.clock.current, f.stored(note.id).updatedAt)
    }

    @Test fun `un articulo vacio no toca la nota ni la hora`() {
        val f = Fixture()
        val note = f.create("Compra\n☐ pan")
        val writes = f.store.writeCount
        f.clock.current = f.clock.current.plusSeconds(60)

        assertIs<NoteResult.Ok<*>>(f.service.appendTask(note.id, "   "))
        assertEquals(writes, f.store.writeCount)
        assertEquals(note.updatedAt, f.stored(note.id).updatedAt)
    }

    @Test fun `una nota que no existe no se inventa`() {
        assertEquals(NoteError.NotFound("x"), (Fixture().service.appendTask("x", "leche") as NoteResult.Err).error)
    }

    @Test fun `una nota en la papelera o archivada no recibe articulos`() {
        val f = Fixture()
        val trashed = f.create("☐ a").also { f.service.trash(it.id) }
        val archived = f.create("☐ b").also { f.service.archive(it.id) }
        for (note in listOf(trashed, archived)) {
            val error = (f.service.appendTask(note.id, "leche") as NoteResult.Err).error
            assertIs<NoteError.InvalidArgument>(error)
        }
        assertEquals("☐ a", f.stored(trashed.id).text)
    }

    @Test fun `una nota protegida no recibe articulos`() {
        val f = Fixture()
        val note = f.create("secreto")
        f.store.put(f.stored(note.id).copy(isProtected = true, protectedContentJson = "{}"))
        assertEquals(NoteError.Protected(note.id), (f.service.appendTask(note.id, "leche") as NoteResult.Err).error)
    }
}
