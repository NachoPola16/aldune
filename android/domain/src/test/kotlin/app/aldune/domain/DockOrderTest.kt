package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals

class DockOrderTest {
    private fun note(id: String, position: Double? = null, created: Long = 0) = Note(
        id = id, text = id, lineEnding = LineEnding.LF, color = "#EBD38B", createdAt = Instant.ofEpochSecond(created),
        updatedAt = Instant.EPOCH, state = NoteState.ACTIVE, dockPosition = position,
    )

    private fun ids(notes: Collection<Note>) = DockOrder.sort(notes).map { it.id }

    @Test
    fun `las numeradas van por posicion, no por fecha`() {
        assertEquals(listOf("b", "a", "c"), ids(listOf(note("a", 2.0, 1), note("b", 1.0, 9), note("c", 3.0, 0))))
    }

    @Test
    fun `las sin posicion van detras de las numeradas, por fecha de creacion`() {
        val notes = listOf(note("viejaSinNumero", null, 1), note("nuevaSinNumero", null, 5), note("numerada", 100.0, 9))
        assertEquals(listOf("numerada", "viejaSinNumero", "nuevaSinNumero"), ids(notes))
    }

    @Test
    fun `una posicion mayor que 1e18 queda detras de las sin posicion`() {
        assertEquals(listOf("sin", "enorme"), ids(listOf(note("enorme", 2e18), note("sin", null))))
    }

    @Test
    fun `con la misma posicion decide la fecha y luego el id`() {
        val notes = listOf(note("z", 1.0, 1), note("b", 1.0, 2), note("a", 1.0, 2))
        assertEquals(listOf("z", "a", "b"), ids(notes))
    }

    @Test
    fun `no importa el orden de entrada y no se modifica la entrada`() {
        val input = listOf(note("b", 2.0), note("a", 1.0))
        assertEquals(ids(input.reversed()), ids(input))
        assertEquals(listOf("b", "a"), input.map { it.id })
    }

    @Test
    fun `sin notas, lista vacia`() {
        assertEquals(emptyList(), DockOrder.sort(emptyList()))
    }
}
