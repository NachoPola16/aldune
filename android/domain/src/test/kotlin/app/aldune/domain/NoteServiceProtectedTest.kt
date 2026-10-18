package app.aldune.domain

import java.time.Duration
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

/** Notas protegidas que llegan por sync: en F5 se guardan enteras y no se tocan (spec F5 §3). Sin abrirlas ni cambiar su texto. */
class NoteServiceProtectedTest {
    private val content = """{"Version":1,"Salt":"c2FsdA==","CipherText":"Y2lwaGVy","Nonce":"bm9uY2U=","Tag":"dGFn"}"""

    /** Una protegida que llegó del PC: sin texto y con el JSON opaco. */
    private fun protectedFromPc(f: Fixture, id: String = "p1", tags: List<String> = emptyList()): Note {
        val note = Note(
            id, "", LineEnding.LF, "#FFDDD4", f.clock.current, f.clock.current, NoteState.ACTIVE, tags = tags,
            syncAuthorDeviceId = "pc", isProtected = true, protectedContentJson = content,
        )
        f.store.put(note)
        f.clock.advance(Duration.ofHours(2))
        return note
    }

    @Test fun `una nota normal no es protegida y no lleva contenido`() {
        val note = Fixture().create()
        assertFalse(note.isProtected)
        assertNull(note.protectedContentJson)
    }

    @Test fun `editar una protegida es un error y no escribe`() {
        val f = Fixture()
        val before = protectedFromPc(f)
        val writes = f.store.writeCount
        val error = f.service.edit("p1", "texto nuevo").errorOrNull()
        assertIs<NoteError.Protected>(error)
        assertEquals(writes, f.store.writeCount)
        assertEquals(before, f.stored("p1"))
    }

    @Test fun `editar una protegida con el mismo texto vacio tampoco es un exito silencioso`() {
        val f = Fixture()
        protectedFromPc(f)
        assertIs<NoteError.Protected>(f.service.edit("p1", "").errorOrNull())
    }

    @Test fun `color, etiquetas, estado y orden conservan el contenido protegido y ponen fecha y autor`() {
        val f = Fixture()
        protectedFromPc(f)
        val other = f.create("otra")

        val colored = f.service.setColor("p1", "#aabbcc").getOrThrow()
        val tagged = f.service.setTags("p1", listOf("trabajo")).getOrThrow()
        val moved = f.service.move("p1", beforeId = other.id, afterId = null).getOrThrow()
        for (note in listOf(colored, tagged, moved, f.stored("p1"))) {
            assertTrue(note.isProtected)
            assertEquals(content, note.protectedContentJson)
            assertEquals("", note.text)
        }
        assertEquals("#aabbcc", f.stored("p1").color)
        assertEquals(listOf("trabajo"), f.stored("p1").tags)
        assertNull(f.stored("p1").syncAuthorDeviceId)
        assertEquals(f.clock.current, f.stored("p1").updatedAt)
    }

    @Test fun `archivar, mandar a la papelera, restaurar y borrar para siempre funcionan igual que con una normal`() {
        val f = Fixture()
        protectedFromPc(f)

        assertEquals(NoteState.ARCHIVED, f.service.archive("p1").getOrThrow().state)
        assertEquals(NoteState.TRASHED, f.service.trash("p1").getOrThrow().state)
        assertEquals(content, f.stored("p1").protectedContentJson)
        assertEquals(NoteState.ACTIVE, f.service.restore("p1").getOrThrow().state)
        f.service.trash("p1")
        f.service.deleteForever("p1").getOrThrow()
        assertNull(f.store.get("p1"))
        assertEquals(listOf("p1"), f.store.tombstones().map { it.noteId })
    }

    @Test fun `purgar la papelera borra tambien las protegidas caducadas`() {
        val f = Fixture()
        protectedFromPc(f)
        f.service.trash("p1")
        f.clock.advance(Duration.ofDays(31))
        assertEquals(1, f.service.purgeTrashed().getOrThrow())
        assertNull(f.store.get("p1"))
    }

    @Test fun `con la busqueda vacia salen y con cualquier busqueda no`() {
        val f = Fixture()
        protectedFromPc(f)
        f.create("hola mundo")

        assertEquals(2, f.service.list(NoteState.ACTIVE).size)
        assertEquals(2, f.service.list(NoteState.ACTIVE, query = "   ").size)
        assertEquals(listOf("n1"), f.service.list(NoteState.ACTIVE, query = "hola").map { it.id })
        assertEquals(listOf("n1"), f.service.list(NoteState.ACTIVE, query = "o").map { it.id })
    }

    @Test fun `una busqueda no encuentra una protegida ni por sus etiquetas`() {
        val f = Fixture()
        protectedFromPc(f, tags = listOf("trabajo"))
        assertTrue(f.service.list(NoteState.ACTIVE, query = "trabajo").isEmpty())
        // El filtro por etiqueta (sin búsqueda) sí la deja: es navegación, no mira el texto.
        assertEquals(listOf("p1"), f.service.list(NoteState.ACTIVE, tag = "trabajo").map { it.id })
    }
}
