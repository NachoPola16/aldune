package app.aldune.domain

import java.time.Duration
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class NoteServiceEditTest {
    @Test fun `get devuelve la nota guardada o null si no existe`() {
        val f = Fixture()
        val note = f.create("hola")
        assertEquals(note, f.service.get(note.id))
        assertNull(f.service.get("no-existe"))
    }

    @Test fun `una nota nueva lleva LF, estado activo y las fechas del reloj`() {
        val f = Fixture()
        val note = f.create("hola")
        assertEquals("n1", note.id)
        assertEquals(LineEnding.LF, note.lineEnding)
        assertEquals(NoteState.ACTIVE, note.state)
        assertEquals(f.clock.current, note.createdAt)
        assertEquals(f.clock.current, note.updatedAt)
        assertNull(note.syncAuthorDeviceId)
        assertNull(note.dockPosition)
        assertEquals(note, f.stored("n1"))
    }

    @Test fun `no se crea una nota sin ningun caracter`() {
        val f = Fixture()
        assertEquals(NoteError.EmptyText, (f.service.create("", "#FFDDD4") as NoteResult.Err).error)
        assertTrue(f.store.all().isEmpty())
        assertEquals(0, f.store.writeCount)
    }

    @Test fun `un espacio ya es un caracter`() {
        assertEquals(" ", Fixture().create(" ").text)
    }

    @Test fun `el texto con CRLF de una nota nueva se guarda con LF`() {
        val note = Fixture().create("a\r\nb")
        assertEquals("a\nb", note.text)
        assertEquals(LineEnding.LF, note.lineEnding)
    }

    @Test fun `editar con el mismo texto no escribe ni toca UpdatedAt`() {
        val f = Fixture()
        val note = f.create("hola")
        f.clock.advance(Duration.ofHours(1))
        val writes = f.store.writeCount
        val result = f.service.edit("n1", "hola").getOrThrow()
        assertEquals(note, result)
        assertEquals(writes, f.store.writeCount)
        assertEquals(note.updatedAt, f.stored("n1").updatedAt)
    }

    @Test fun `editar con otro texto pone UpdatedAt y borra el autor de sync`() {
        val f = Fixture()
        f.store.put(Note("p1", "del pc", LineEnding.LF, "#FFDDD4", f.clock.current, f.clock.current, NoteState.ACTIVE, syncAuthorDeviceId = "pc"))
        f.clock.advance(Duration.ofMinutes(5))
        val edited = f.service.edit("p1", "del pc, y mas").getOrThrow()
        assertEquals("del pc, y mas", edited.text)
        assertEquals(f.clock.current, edited.updatedAt)
        assertNull(edited.syncAuthorDeviceId)
        assertEquals(edited, f.stored("p1"))
    }

    @Test fun `editar no cambia el resto de la nota`() {
        val f = Fixture()
        val original = f.create("hola").copy(tags = listOf("Trabajo"), dockPosition = 5.0, screenOrigin = "\\\\.\\DISPLAY2")
        f.store.put(original)
        f.clock.advance(Duration.ofMinutes(1))
        val edited = f.service.edit("n1", "adios").getOrThrow()
        assertEquals(original.copy(text = "adios", updatedAt = f.clock.current), edited)
    }

    @Test fun `una nota de Windows con CRLF se edita sin perder sus saltos`() {
        val f = Fixture()
        f.store.put(Note("w1", "uno\ndos", LineEnding.CRLF, "#FFDDD4", f.clock.current, f.clock.current, NoteState.ACTIVE, syncAuthorDeviceId = "pc"))
        f.clock.advance(Duration.ofMinutes(1))
        val edited = f.service.edit("w1", "uno\ndos\ntres").getOrThrow()
        assertEquals("uno\ndos\ntres", edited.text)
        assertEquals(LineEnding.CRLF, edited.lineEnding)
        assertEquals("uno\r\ndos\r\ntres", edited.wireText)
    }

    @Test fun `el editor puede devolver CRLF y si el texto es el mismo no hay version nueva`() {
        val f = Fixture()
        f.store.put(Note("w1", "uno\ndos", LineEnding.CRLF, "#FFDDD4", f.clock.current, f.clock.current, NoteState.ACTIVE, syncAuthorDeviceId = "pc"))
        f.clock.advance(Duration.ofMinutes(1))
        val writes = f.store.writeCount
        f.service.edit("w1", "uno\r\ndos")
        assertEquals(writes, f.store.writeCount)
        assertEquals("pc", f.stored("w1").syncAuthorDeviceId)
    }

    @Test fun `una nota LF no pasa a CRLF aunque el editor devuelva CRLF`() {
        val f = Fixture()
        f.create("uno")
        f.clock.advance(Duration.ofMinutes(1))
        val edited = f.service.edit("n1", "uno\r\ndos").getOrThrow()
        assertEquals("uno\ndos", edited.text)
        assertEquals(LineEnding.LF, edited.lineEnding)
    }

    @Test fun `se puede vaciar una nota que ya existe`() {
        val f = Fixture()
        f.create("hola")
        f.clock.advance(Duration.ofMinutes(1))
        assertEquals("", f.service.edit("n1", "").getOrThrow().text)
    }

    @Test fun `editar una nota que no existe devuelve un error claro`() {
        val f = Fixture()
        val error = (f.service.edit("fantasma", "x") as NoteResult.Err).error
        assertIs<NoteError.NotFound>(error)
        assertEquals("fantasma", error.id)
        assertEquals(0, f.store.writeCount)
    }
}
