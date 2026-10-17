package app.aldune.domain

import java.time.Duration
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class NoteServiceStateTest {
    /** Una nota que llegó del PC y se pasó un rato: así se ve que el cambio local cambia fecha y autor. */
    private fun fromPc(f: Fixture, id: String = "p1", state: NoteState = NoteState.ACTIVE, tags: List<String> = emptyList()): Note {
        val note = Note(id, "del pc", LineEnding.LF, "#FFDDD4", f.clock.current, f.clock.current, state, tags = tags, syncAuthorDeviceId = "pc")
        f.store.put(note)
        f.clock.advance(Duration.ofHours(2))
        return note
    }

    // --- color ---

    @Test fun `cambiar el color guarda el hex tal cual y pone fecha y autor`() {
        val f = Fixture()
        val before = fromPc(f)
        val note = f.service.setColor("p1", "#aabbcc").getOrThrow()
        assertEquals("#aabbcc", note.color)
        assertEquals(f.clock.current, note.updatedAt)
        assertNull(note.syncAuthorDeviceId)
        assertEquals(before.text, note.text)
    }

    @Test fun `el mismo color no escribe`() {
        val f = Fixture()
        fromPc(f)
        val writes = f.store.writeCount
        f.service.setColor("p1", "#FFDDD4")
        assertEquals(writes, f.store.writeCount)
        assertEquals("pc", f.stored("p1").syncAuthorDeviceId)
    }

    @Test fun `un color vacio es un argumento no valido`() {
        val f = Fixture()
        fromPc(f)
        assertIs<NoteError.InvalidArgument>(f.service.setColor("p1", "  ").errorOrNull())
        assertEquals("#FFDDD4", f.stored("p1").color)
    }

    // --- etiquetas ---

    @Test fun `las etiquetas se recortan, sin vacias ni repetidas ignorando mayusculas, y conservan la primera forma`() {
        val f = Fixture()
        fromPc(f)
        val note = f.service.setTags("p1", listOf("  Trabajo ", "", "   ", "trabajo", "Casa", "TRABAJO", "casa")).getOrThrow()
        assertEquals(listOf("Trabajo", "Casa"), note.tags)
        assertEquals(f.clock.current, note.updatedAt)
        assertNull(note.syncAuthorDeviceId)
    }

    @Test fun `solo se quedan 20 etiquetas, contadas despues de quitar repetidas`() {
        val f = Fixture()
        fromPc(f)
        val tags = listOf("a", "A") + (1..25).map { "t$it" }
        val note = f.service.setTags("p1", tags).getOrThrow()
        assertEquals(20, note.tags.size)
        assertEquals("a", note.tags[0])
        assertEquals("t19", note.tags[19])
    }

    @Test fun `mayusculas no ASCII tambien cuentan como repetidas, pero la ß no es SS`() {
        val f = Fixture()
        fromPc(f)
        assertEquals(listOf("Ñandú", "ß", "SS"), f.service.setTags("p1", listOf("Ñandú", "ñANDÚ", "ß", "SS")).getOrThrow().tags)
    }

    @Test fun `las mismas etiquetas en el mismo orden no escriben`() {
        val f = Fixture()
        fromPc(f, tags = listOf("Casa"))
        val writes = f.store.writeCount
        f.service.setTags("p1", listOf(" Casa ", "casa"))
        assertEquals(writes, f.store.writeCount)
    }

    @Test fun `quitar todas las etiquetas es un cambio`() {
        val f = Fixture()
        fromPc(f, tags = listOf("Casa"))
        val note = f.service.setTags("p1", emptyList()).getOrThrow()
        assertTrue(note.tags.isEmpty())
        assertEquals(f.clock.current, note.updatedAt)
    }

    // --- estados ---

    @Test fun `archivar, papelera y restaurar cambian el estado y ponen fecha y autor`() {
        val f = Fixture()
        fromPc(f)
        assertEquals(NoteState.ARCHIVED, f.service.archive("p1").getOrThrow().state)
        f.clock.advance(Duration.ofMinutes(1))
        val trashed = f.service.trash("p1").getOrThrow()
        assertEquals(NoteState.TRASHED, trashed.state)
        assertEquals(f.clock.current, trashed.updatedAt)
        f.clock.advance(Duration.ofMinutes(1))
        val restored = f.service.restore("p1").getOrThrow()
        assertEquals(NoteState.ACTIVE, restored.state)
        assertEquals(f.clock.current, restored.updatedAt)
        assertNull(restored.syncAuthorDeviceId)
    }

    @Test fun `restaurar tambien saca una nota del archivo`() {
        val f = Fixture()
        fromPc(f, state = NoteState.ARCHIVED)
        assertEquals(NoteState.ACTIVE, f.service.restore("p1").getOrThrow().state)
    }

    @Test fun `pedir el estado que ya tiene no escribe`() {
        val f = Fixture()
        fromPc(f, state = NoteState.TRASHED)
        val writes = f.store.writeCount
        f.service.trash("p1")
        f.service.restore("p1").getOrThrow()
        f.service.restore("p1")
        assertEquals(writes + 1, f.store.writeCount)
    }

    @Test fun `restaurar una nota activa no cambia nada`() {
        val f = Fixture()
        val note = fromPc(f)
        val writes = f.store.writeCount
        assertEquals(note, f.service.restore("p1").getOrThrow())
        assertEquals(writes, f.store.writeCount)
    }

    @Test fun `cambiar de estado conserva el orden del mazo y el resto`() {
        val f = Fixture()
        fromPc(f)
        f.store.put(f.stored("p1").copy(dockPosition = 2048.0, tags = listOf("x")))
        val trashed = f.service.trash("p1").getOrThrow()
        assertEquals(2048.0, trashed.dockPosition)
        assertEquals(listOf("x"), trashed.tags)
    }

    // --- borrar para siempre ---

    @Test fun `solo se borra para siempre desde la papelera`() {
        val f = Fixture()
        fromPc(f)
        val error = f.service.deleteForever("p1").errorOrNull()
        assertEquals(NoteError.NotInTrash("p1", NoteState.ACTIVE), error)
        assertTrue(f.store.get("p1") != null)
        assertTrue(f.store.tombstones().isEmpty())
        f.service.archive("p1")
        assertIs<NoteError.NotInTrash>(f.service.deleteForever("p1").errorOrNull())
    }

    @Test fun `borrar para siempre quita la nota y deja una lapida local con la fecha`() {
        val f = Fixture()
        fromPc(f)
        f.service.trash("p1")
        f.clock.advance(Duration.ofMinutes(3))
        f.service.deleteForever("p1").getOrThrow()
        assertNull(f.store.get("p1"))
        assertEquals(listOf(Tombstone("p1", f.clock.current, Tombstone.LOCAL_DEVICE)), f.store.tombstones())
    }

    @Test fun `borrar dos veces la misma nota da no encontrada la segunda`() {
        val f = Fixture()
        fromPc(f)
        f.service.trash("p1")
        f.service.deleteForever("p1").getOrThrow()
        assertEquals(NoteError.NotFound("p1"), f.service.deleteForever("p1").errorOrNull())
        assertEquals(1, f.store.tombstones().size)
    }

    // --- purga ---

    private fun trashedAgo(f: Fixture, id: String, ago: Duration) {
        f.store.put(Note(id, "x", LineEnding.LF, "#FFDDD4", f.clock.current.minus(ago), f.clock.current.minus(ago), NoteState.TRASHED))
    }

    @Test fun `la purga por defecto es de 30 dias y solo borra lo mas viejo, estrictamente`() {
        val f = Fixture()
        trashedAgo(f, "viejo", Duration.ofDays(31))
        trashedAgo(f, "justo", Duration.ofDays(30))
        trashedAgo(f, "nuevo", Duration.ofDays(29))
        assertEquals(1, f.service.purgeTrashed().getOrThrow())
        assertNull(f.store.get("viejo"))
        assertTrue(f.store.get("justo") != null && f.store.get("nuevo") != null)
        assertEquals(listOf("viejo"), f.store.tombstones().map { it.noteId })
    }

    @Test fun `la purga nunca toca activas ni archivadas por viejas que sean`() {
        val f = Fixture()
        val old = f.clock.current.minus(Duration.ofDays(400))
        f.store.put(Note("a", "x", LineEnding.LF, "#FFDDD4", old, old, NoteState.ACTIVE))
        f.store.put(Note("b", "x", LineEnding.LF, "#FFDDD4", old, old, NoteState.ARCHIVED))
        assertEquals(0, f.service.purgeTrashed().getOrThrow())
        assertEquals(2, f.store.all().size)
        assertTrue(f.store.tombstones().isEmpty())
    }

    @Test fun `restaurar reinicia el plazo de la purga`() {
        val f = Fixture()
        trashedAgo(f, "n", Duration.ofDays(40))
        f.service.restore("n")
        f.service.trash("n")
        assertEquals(0, f.service.purgeTrashed().getOrThrow())
    }

    @Test fun `los dias de purga se pueden elegir y no pueden ser negativos`() {
        val f = Fixture()
        trashedAgo(f, "n", Duration.ofDays(8))
        assertEquals(0, f.service.purgeTrashed(days = 9).getOrThrow())
        assertEquals(1, f.service.purgeTrashed(days = 7).getOrThrow())
        assertIs<NoteError.InvalidArgument>(f.service.purgeTrashed(days = -1).errorOrNull())
    }

    // --- ids desconocidos ---

    @Test fun `todas las operaciones sobre un id desconocido devuelven NotFound`() {
        val f = Fixture()
        val unknown = NoteError.NotFound("zzz")
        assertEquals(unknown, f.service.setColor("zzz", "#000000").errorOrNull())
        assertEquals(unknown, f.service.setTags("zzz", listOf("a")).errorOrNull())
        assertEquals(unknown, f.service.archive("zzz").errorOrNull())
        assertEquals(unknown, f.service.trash("zzz").errorOrNull())
        assertEquals(unknown, f.service.restore("zzz").errorOrNull())
        assertEquals(unknown, f.service.deleteForever("zzz").errorOrNull())
        assertEquals(0, f.store.writeCount)
    }
}
