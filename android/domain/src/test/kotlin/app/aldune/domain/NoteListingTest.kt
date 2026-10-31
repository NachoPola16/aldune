package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals

class NoteListingTest {
    private fun note(id: String, text: String, at: String, protected: Boolean = false) =
        Note(id, text, LineEnding.LF, "#EBD38B", Instant.parse(at), Instant.parse(at), NoteState.ACTIVE, isProtected = protected)

    private val a = note("a", "Banana", "2026-01-02T00:00:00Z")
    private val b = note("b", "apple", "2026-01-03T00:00:00Z")
    private val c = note("c", "Cherry", "2026-01-01T00:00:00Z")
    private val locked = note("p", "secreto", "2026-01-04T00:00:00Z", protected = true)
    private val all = listOf(a, b, c, locked)

    private fun ids(order: NoteListOrder) = NoteListing.sort(all, order).map { it.id }

    @Test fun `el orden del mazo no toca nada`() = assertEquals(listOf("a", "b", "c", "p"), ids(NoteListOrder.DOCK))

    @Test fun `mas recientes primero por fecha de cambio`() = assertEquals(listOf("p", "b", "a", "c"), ids(NoteListOrder.NEWEST))

    @Test fun `mas antiguas primero`() = assertEquals(listOf("c", "a", "b", "p"), ids(NoteListOrder.OLDEST))

    @Test fun `por titulo sin distinguir mayusculas y con las protegidas al final`() = assertEquals(listOf("b", "a", "c", "p"), ids(NoteListOrder.TITLE))

    @Test fun `una protegida con nombre visible ordena por ese nombre y sin nombre sigue al final`() {
        val named = note("n", "", "2026-01-05T00:00:00Z", protected = true).copy(protectedTitle = "Banco")
        val sorted = NoteListing.sort(listOf(a, locked, c, named, b), NoteListOrder.TITLE).map { it.id }
        // apple, Banana, Banco, Cherry, y la protegida sin nombre al final.
        assertEquals(listOf("b", "a", "n", "c", "p"), sorted)
    }

    @Test fun `los codigos son los de Windows`() {
        assertEquals(listOf(0, 1, 2, 3), NoteListOrder.entries.map { it.code })
        assertEquals(NoteListOrder.DOCK, NoteListOrder.fromCode(99))
    }

    @Test fun `las fijadas van primero en todos los ordenes y cada grupo conserva el suyo`() {
        val pinnedC = c.copy(pinnedToTop = true)
        val pinnedA = a.copy(pinnedToTop = true)
        val notes = listOf(a, b, pinnedC, locked, pinnedA)
        for (order in NoteListOrder.entries) {
            val sorted = NoteListing.sort(notes, order)
            val pinnedFirst = NoteListing.sort(listOf(pinnedC, pinnedA), order) + NoteListing.sort(listOf(a, b, locked), order)
            assertEquals(pinnedFirst.map { it.id }, sorted.map { it.id }, order.name)
        }
    }
}
