package app.aldune.domain

import java.time.Duration
import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNull

class NoteServiceOrderTest {
    private fun put(
        f: Fixture, id: String, createdMinute: Long = 0, position: Double? = null, state: NoteState = NoteState.ACTIVE,
        text: String = id, tags: List<String> = emptyList(),
    ): Note {
        val created = Instant.parse("2026-01-01T00:00:00Z").plus(Duration.ofMinutes(createdMinute))
        return Note(id, text, LineEnding.LF, "#FFDDD4", created, created, state, tags, position, syncAuthorDeviceId = "pc")
            .also { f.store.put(it) }
    }

    private fun ids(f: Fixture, state: NoteState = NoteState.ACTIVE, query: String = "", tag: String? = null) =
        f.service.list(state, query, tag).map { it.id }

    // --- list: orden del mazo ---

    @Test fun `sin posiciones ordena por fecha de creacion`() {
        val f = Fixture()
        put(f, "c", 3); put(f, "a", 1); put(f, "b", 2)
        assertEquals(listOf("a", "b", "c"), ids(f))
    }

    @Test fun `las que tienen posicion van antes, por posicion, y las demas despues por creacion`() {
        val f = Fixture()
        put(f, "sin1", 1); put(f, "p2", 5, 20.0); put(f, "sin2", 2); put(f, "p1", 9, 10.0); put(f, "neg", 7, -5.0)
        assertEquals(listOf("neg", "p1", "p2", "sin1", "sin2"), ids(f))
    }

    @Test fun `la posicion le gana a la fecha de creacion`() {
        val f = Fixture()
        put(f, "vieja", 1, 2048.0); put(f, "nueva", 100, 0.0)
        assertEquals(listOf("nueva", "vieja"), ids(f))
    }

    @Test fun `copia el borde de Core, una posicion mayor que 1e18 queda despues de las que no tienen`() {
        val f = Fixture()
        put(f, "enorme", 1, 2e18); put(f, "sin", 2)
        assertEquals(listOf("sin", "enorme"), ids(f))
    }

    @Test fun `un empate total se resuelve por id, siempre igual`() {
        val f = Fixture()
        put(f, "b", 1); put(f, "a", 1)
        assertEquals(listOf("a", "b"), ids(f))
    }

    @Test fun `cada filtro enseña solo su estado`() {
        val f = Fixture()
        put(f, "a"); put(f, "r", state = NoteState.ARCHIVED); put(f, "t", state = NoteState.TRASHED)
        assertEquals(listOf("a"), ids(f, NoteState.ACTIVE))
        assertEquals(listOf("r"), ids(f, NoteState.ARCHIVED))
        assertEquals(listOf("t"), ids(f, NoteState.TRASHED))
    }

    @Test fun `la etiqueta filtra sin distinguir mayusculas y mantiene el orden`() {
        val f = Fixture()
        put(f, "a", 1, tags = listOf("Casa")); put(f, "b", 2, tags = listOf("Trabajo")); put(f, "c", 3, tags = listOf("trabajo", "Casa"))
        assertEquals(listOf("b", "c"), ids(f, tag = "TRABAJO"))
        assertEquals(listOf("a", "c"), ids(f, tag = "casa"))
        assertEquals(emptyList(), ids(f, tag = "nada"))
    }

    @Test fun `la busqueda mira el texto y las etiquetas, sin acentos ni mayusculas`() {
        val f = Fixture()
        put(f, "a", 1, text = "Café con leche"); put(f, "b", 2, text = "otra", tags = listOf("Cafetería")); put(f, "c", 3, text = "nada")
        assertEquals(listOf("a", "b"), ids(f, query = "cafe"))
        assertEquals(listOf("a", "b", "c"), ids(f, query = "   "))
    }

    @Test fun `estado, etiqueta y busqueda se combinan`() {
        val f = Fixture()
        put(f, "a", 1, text = "uno", tags = listOf("x")); put(f, "b", 2, text = "uno", tags = listOf("x"), state = NoteState.ARCHIVED)
        put(f, "c", 3, text = "dos", tags = listOf("x"))
        assertEquals(listOf("a"), ids(f, NoteState.ACTIVE, "uno", "x"))
    }

    // --- move ---

    @Test fun `la primera vez que se arrastra se numera todo el mazo y se tocan todas`() {
        val f = Fixture()
        put(f, "a", 1); put(f, "b", 2); put(f, "c", 3)
        f.clock.advance(Duration.ofHours(1))
        f.service.move("c", beforeId = null, afterId = "a").getOrThrow()
        assertEquals(listOf("c", "a", "b"), ids(f))
        assertEquals(listOf(0.0, 1024.0, 2048.0), f.service.list(NoteState.ACTIVE).map { it.dockPosition })
        f.store.all().forEach {
            assertEquals(f.clock.current, it.updatedAt)
            assertNull(it.syncAuthorDeviceId)
        }
    }

    @Test fun `con el mazo ya numerado solo se escribe la nota movida, en el punto medio`() {
        val f = Fixture()
        put(f, "a", 1, 0.0); put(f, "b", 2, 1024.0); put(f, "c", 3, 2048.0)
        f.clock.advance(Duration.ofHours(1))
        val writes = f.store.writeCount
        val moved = f.service.move("c", beforeId = "a", afterId = "b").getOrThrow()
        assertEquals(512.0, moved.dockPosition)
        assertEquals(f.clock.current, moved.updatedAt)
        assertNull(moved.syncAuthorDeviceId)
        assertEquals(writes + 1, f.store.writeCount)
        assertEquals(listOf("a", "c", "b"), ids(f))
        assertEquals("pc", f.stored("a").syncAuthorDeviceId)
        assertEquals(Instant.parse("2026-01-01T00:01:00Z"), f.stored("a").updatedAt)
    }

    @Test fun `al principio y al final se deja un hueco de 1024`() {
        val f = Fixture()
        put(f, "a", 1, 0.0); put(f, "b", 2, 1024.0); put(f, "c", 3, 2048.0)
        f.service.move("c", beforeId = null, afterId = "a")
        assertEquals(-1024.0, f.stored("c").dockPosition)
        f.service.move("a", beforeId = "b", afterId = null)
        assertEquals(2048.0, f.stored("a").dockPosition)
        assertEquals(listOf("c", "b", "a"), ids(f))
    }

    @Test fun `si el hueco se agota se renumera todo y se tocan todas`() {
        val f = Fixture()
        val next = Math.nextUp(1.0)
        put(f, "a", 1, 1.0); put(f, "b", 2, next); put(f, "c", 3, 5000.0)
        f.clock.advance(Duration.ofHours(1))
        f.service.move("c", beforeId = "a", afterId = "b").getOrThrow()
        assertEquals(listOf("a", "c", "b"), ids(f))
        assertEquals(listOf(0.0, 1024.0, 2048.0), f.service.list(NoteState.ACTIVE).map { it.dockPosition })
        f.store.all().forEach { assertEquals(f.clock.current, it.updatedAt) }
    }

    @Test fun `soltarla donde ya esta no escribe nada`() {
        val f = Fixture()
        put(f, "a", 1, 0.0); put(f, "b", 2, 1024.0); put(f, "c", 3, 2048.0)
        val writes = f.store.writeCount
        f.service.move("b", beforeId = "a", afterId = "c").getOrThrow()
        f.service.move("a", beforeId = null, afterId = "b").getOrThrow()
        f.service.move("c", beforeId = "b", afterId = null).getOrThrow()
        assertEquals(writes, f.store.writeCount)
    }

    @Test fun `una nota sola no necesita vecinas ni se escribe`() {
        val f = Fixture()
        put(f, "a", 1)
        val writes = f.store.writeCount
        f.service.move("a", null, null).getOrThrow()
        assertEquals(writes, f.store.writeCount)
    }

    @Test fun `mover solo reordena dentro de su estado`() {
        val f = Fixture()
        put(f, "a", 1, 0.0); put(f, "b", 2, 1024.0); put(f, "arch", 3, state = NoteState.ARCHIVED)
        f.service.move("b", null, "a").getOrThrow()
        assertEquals(listOf("b", "a"), ids(f))
        assertNull(f.stored("arch").dockPosition)
    }

    @Test fun `errores de move`() {
        val f = Fixture()
        put(f, "a", 1, 0.0); put(f, "b", 2, 1024.0); put(f, "arch", 3, 5.0, NoteState.ARCHIVED)
        assertEquals(NoteError.NotFound("zzz"), f.service.move("zzz", "a", "b").errorOrNull())
        assertEquals(NoteError.NotFound("zzz"), f.service.move("a", "zzz", null).errorOrNull())
        assertEquals(NoteError.NotFound("zzz"), f.service.move("a", null, "zzz").errorOrNull())
        assertIs<NoteError.InvalidArgument>(f.service.move("a", "a", null).errorOrNull())
        assertIs<NoteError.InvalidArgument>(f.service.move("a", null, "a").errorOrNull())
        assertIs<NoteError.InvalidArgument>(f.service.move("a", "arch", null).errorOrNull())
        assertIs<NoteError.InvalidArgument>(f.service.move("a", null, null).errorOrNull())
        put(f, "c", 4, 2048.0)
        assertIs<NoteError.InvalidArgument>(f.service.move("a", beforeId = "c", afterId = "b").errorOrNull())
        assertEquals(listOf("a", "b", "c"), ids(f))
    }

    @Test fun `un error de move no escribe nada`() {
        val f = Fixture()
        put(f, "a", 1, 0.0); put(f, "b", 2, 1024.0)
        val writes = f.store.writeCount
        f.service.move("a", "zzz", "b")
        f.service.move("a", null, null)
        assertEquals(writes, f.store.writeCount)
    }
}
