package app.aldune.domain

import java.time.Duration
import java.time.Instant
import java.util.concurrent.CountDownLatch
import java.util.concurrent.Executors
import java.util.concurrent.TimeUnit
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertIs
import kotlin.test.assertNull
import kotlin.test.assertTrue

class NoteServiceRobustnessTest {
    private val t0 = Instant.parse("2026-03-01T10:00:00Z")

    /** Una nota tal como la dejaría la sync: texto de sync → texto de trabajo + salto de línea detectado. */
    private fun fromWire(f: Fixture, id: String, wire: String): Note {
        val note = Note(id, LineEnding.normalize(wire), LineEnding.detect(wire), "#FFDDD4", t0, t0, NoteState.ACTIVE, syncAuthorDeviceId = "pc")
        f.store.put(note)
        return note
    }

    // --- abrir y cerrar sin editar ---

    @Test fun `abrir y cerrar sin editar, con todas las operaciones en su valor actual, no cambia nada`() {
        val f = Fixture()
        val note = fromWire(f, "p1", "uno\r\ndos").copy(tags = listOf("Casa", "Trabajo"), dockPosition = 1024.0).also { f.store.put(it) }
        val before = f.store.all()
        val writes = f.store.writeCount
        f.clock.advance(Duration.ofDays(2))

        f.service.edit("p1", "uno\r\ndos")
        f.service.edit("p1", "uno\ndos")
        f.service.setColor("p1", note.color)
        f.service.setTags("p1", listOf("Casa ", "Trabajo", "TRABAJO"))
        f.service.restore("p1")
        f.service.move("p1", null, null)
        f.service.purgeTrashed()

        assertEquals(before, f.store.all())
        assertEquals(writes, f.store.writeCount)
        assertTrue(f.store.tombstones().isEmpty())
    }

    // --- CRLF de ida y vuelta ---

    @Test fun `un texto de Windows sin tocar vuelve igual byte a byte`() {
        val f = Fixture()
        for ((i, wire) in listOf("a\r\nb\r\n", "\r\n\r\nx", "una sola", "", "\r\n", "a\r\n\r\nb\r\n\r\n").withIndex()) {
            val note = fromWire(f, "w$i", wire)
            assertEquals(wire, note.wireText, "ida y vuelta de «$wire»")
            // El editor devuelve lo que se le dio (con \n) o con CRLF: en los dos casos es la misma versión.
            f.service.edit(note.id, note.text)
            f.service.edit(note.id, wire)
            assertEquals(note, f.stored(note.id))
        }
    }

    @Test fun `editar una nota de Windows mantiene CRLF en todas las lineas, tambien las nuevas`() {
        val f = Fixture()
        fromWire(f, "w", "uno\r\ndos")
        f.clock.advance(Duration.ofMinutes(1))
        val edited = f.service.edit("w", "uno\ndos\n\ntres\n").getOrThrow()
        assertEquals("uno\r\ndos\r\n\r\ntres\r\n", edited.wireText)
    }

    @Test fun `un CR suelto no es un salto de linea y se conserva`() {
        val f = Fixture()
        val note = fromWire(f, "w", "a\rb")
        assertEquals(LineEnding.LF, note.lineEnding)
        assertEquals("a\rb", note.wireText)
    }

    @Test fun `una nota con CRLF y el editor devolviendo LF no cambia de salto de linea`() {
        val f = Fixture()
        fromWire(f, "w", "uno\r\ndos")
        f.clock.advance(Duration.ofMinutes(1))
        assertEquals(LineEnding.CRLF, f.service.edit("w", "uno\ndos!").getOrThrow().lineEnding)
    }

    // --- escrituras desordenadas ---

    @Test fun `una escritura mas vieja que otra ya aceptada se descarta`() {
        val f = Fixture()
        f.create("v0")
        val t1 = t0.plusSeconds(1)
        val t2 = t0.plusSeconds(2)
        f.clock.current = t2
        f.service.edit("n1", "v2", editedAt = t2).getOrThrow()

        f.clock.current = t2.plusSeconds(5) // llega tarde, pero el reloj ya avanzó: no debe ganar por eso
        val late = f.service.edit("n1", "v1", editedAt = t1)
        assertEquals(NoteError.StaleWrite("n1"), late.errorOrNull())
        assertEquals("v2", f.stored("n1").text)
        assertEquals(t2, f.stored("n1").updatedAt)
    }

    @Test fun `con la misma hora de edicion gana la ultima en llegar`() {
        val f = Fixture()
        f.create("v0")
        val t = t0.plusSeconds(3)
        f.service.edit("n1", "a", editedAt = t).getOrThrow()
        f.service.edit("n1", "b", editedAt = t).getOrThrow()
        assertEquals("b", f.stored("n1").text)
    }

    @Test fun `sin hora de edicion siempre gana la ultima, como en Core`() {
        val f = Fixture()
        f.create("v0")
        f.service.edit("n1", "a", editedAt = t0.plusSeconds(9)).getOrThrow()
        f.service.edit("n1", "b").getOrThrow()
        assertEquals("b", f.stored("n1").text)
    }

    @Test fun `una edicion de texto antigua no se pierde por un cambio de color o de estado mas nuevo`() {
        val f = Fixture()
        f.create("v0")
        f.clock.current = t0.plusSeconds(10)
        f.service.setColor("n1", "#000000").getOrThrow()
        f.service.trash("n1").getOrThrow()
        // El texto se escribió a las t0+5, antes del cambio de color, pero ninguna otra escritura de texto lo supera.
        val edited = f.service.edit("n1", "v1", editedAt = t0.plusSeconds(5)).getOrThrow()
        assertEquals("v1", edited.text)
        assertEquals("#000000", edited.color)
        assertEquals(NoteState.TRASHED, edited.state)
    }

    @Test fun `la regla es por nota, no global`() {
        val f = Fixture()
        f.create("uno"); f.create("dos")
        f.service.edit("n1", "uno+", editedAt = t0.plusSeconds(10)).getOrThrow()
        assertEquals("dos+", f.service.edit("n2", "dos+", editedAt = t0.plusSeconds(1)).getOrThrow().text)
    }

    @Test fun `la version final es la de la hora de edicion mas reciente, llegue en el orden que llegue`() {
        val f = Fixture()
        f.create("v0")
        val pool = Executors.newFixedThreadPool(8)
        val start = CountDownLatch(1)
        val order = (1..200).shuffled(java.util.Random(7))
        val tasks = order.map { n ->
            pool.submit {
                start.await()
                f.service.edit("n1", "v$n", editedAt = t0.plusSeconds(n.toLong()))
            }
        }
        start.countDown()
        tasks.forEach { it.get(10, TimeUnit.SECONDS) }
        pool.shutdown()
        assertEquals("v200", f.stored("n1").text)
    }

    @Test fun `cambios de campos distintos desde hilos distintos se suman, ninguno pisa a otro`() {
        val f = Fixture()
        f.create("v0")
        val pool = Executors.newFixedThreadPool(4)
        val start = CountDownLatch(1)
        val work = listOf<() -> Unit>(
            { f.service.edit("n1", "texto nuevo") },
            { f.service.setColor("n1", "#123456") },
            { f.service.setTags("n1", listOf("a", "b")) },
            { f.service.archive("n1") },
        )
        val tasks = work.map { w -> pool.submit { start.await(); w() } }
        start.countDown()
        tasks.forEach { it.get(10, TimeUnit.SECONDS) }
        pool.shutdown()
        val note = f.stored("n1")
        assertEquals("texto nuevo", note.text)
        assertEquals("#123456", note.color)
        assertEquals(listOf("a", "b"), note.tags)
        assertEquals(NoteState.ARCHIVED, note.state)
        assertNull(note.syncAuthorDeviceId)
    }

    // --- ids y estados desconocidos ---

    @Test fun `editar o mover un id desconocido devuelve NotFound sin escribir`() {
        val f = Fixture()
        f.create("a")
        val writes = f.store.writeCount
        assertEquals(NoteError.NotFound("zzz"), f.service.edit("zzz", "x", editedAt = t0).errorOrNull())
        assertEquals(NoteError.NotFound("zzz"), f.service.move("zzz", "n1", null).errorOrNull())
        assertEquals(NoteError.NotFound("zzz"), f.service.changeState("zzz", 1).errorOrNull())
        assertEquals(writes, f.store.writeCount)
    }

    @Test fun `un id vacio o con otra capitalizacion no es la misma nota`() {
        val f = Fixture()
        f.service.create("a", "#FFDDD4", screenOrigin = "x")
        f.store.put(f.stored("n1").copy(id = "ABC"))
        assertIs<NoteError.NotFound>(f.service.edit("abc", "x").errorOrNull())
        assertIs<NoteError.NotFound>(f.service.edit("", "x").errorOrNull())
    }

    @Test fun `un codigo de estado desconocido es un argumento no valido y no cambia la nota`() {
        val f = Fixture()
        val note = f.create("a")
        for (code in listOf(3, -1, 99, Int.MAX_VALUE)) {
            val error = f.service.changeState("n1", code).errorOrNull()
            assertIs<NoteError.InvalidArgument>(error)
            assertTrue(error.reason.contains(code.toString()))
        }
        assertEquals(note, f.stored("n1"))
    }

    @Test fun `un codigo de estado valido hace lo mismo que el metodo con nombre`() {
        val f = Fixture()
        f.create("a")
        assertEquals(NoteState.ARCHIVED, f.service.changeState("n1", 1).getOrThrow().state)
        assertEquals(NoteState.TRASHED, f.service.changeState("n1", 2).getOrThrow().state)
        assertEquals(NoteState.ACTIVE, f.service.changeState("n1", 0).getOrThrow().state)
    }

    @Test fun `ninguna operacion lanza con entradas raras`() {
        val f = Fixture()
        f.create("a")
        val weird = listOf("", " ", "\u0000", "\uD83D", "a".repeat(100_000), "\r", "\r\n\r\n")
        for (s in weird) {
            f.service.edit("n1", s)
            f.service.setTags("n1", listOf(s, s, "ok"))
            f.service.setColor("n1", s)
            f.service.list(NoteState.ACTIVE, s, s)
            f.service.edit(s, s)
        }
        assertEquals(1, f.store.all().size)
    }
}
