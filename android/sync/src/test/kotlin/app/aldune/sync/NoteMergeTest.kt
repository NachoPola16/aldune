package app.aldune.sync

import app.aldune.core.NoteState
import app.aldune.core.SyncNote
import java.time.OffsetDateTime
import java.util.UUID
import org.junit.jupiter.api.Assertions.assertEquals
import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertNotNull
import org.junit.jupiter.api.Assertions.assertNull
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

/** Los casos de `NoteMergeTests` de Core, para que los dos lados fusionen igual. */
class NoteMergeTest {
    private val id = UUID.fromString("00000001-0000-4000-8000-000000000000")
    private val t0 = OffsetDateTime.parse("2026-11-22T10:00:00+00:00")

    private fun make(
        text: String, minutes: Long = 0, color: String = "#EBD38B", state: Int = NoteState.ACTIVE,
        tags: List<String> = emptyList(), pinned: Boolean = false, policy: Int = 0, dock: Double? = null, isProtected: Boolean = false,
    ) = SyncNote(
        id, text, color, t0, t0.plusMinutes(minutes), state, "primary", tags, isProtected, null, dock,
        pinnedToTop = pinned, completedTasks = policy,
    )

    @Test fun `el texto se fusiona y el color viene del unico lado que lo cambio`() {
        val result = NoteMerge.merge(make("compra\npan"), make("compra\npan\nleche", 1), "a", make("compra\npan", 2, color = "#C2D4FF"), "b")
        assertFalse(result.conflict)
        assertEquals("compra\npan\nleche", result.merged!!.text)
        assertEquals("#C2D4FF", result.merged!!.color)
    }

    @Test fun `si los dos cambian el color gana el mas reciente sin conflicto`() {
        val result = NoteMerge.merge(make("x"), make("x", 1, color = "#111111"), "a", make("x", 2, color = "#222222"), "b")
        assertFalse(result.conflict)
        assertEquals("#222222", result.merged!!.color)
    }

    @Test fun `las etiquetas se fusionan como conjunto contra la base`() {
        val result = NoteMerge.merge(make("x", tags = listOf("a", "b")), make("x", 1, tags = listOf("a", "b", "c")), "a", make("x", 2, tags = listOf("a")), "b")
        assertFalse(result.conflict)
        assertEquals(listOf("a", "c"), result.merged!!.tags.sorted())
    }

    @Test fun `papelera contra edicion de texto es conflicto`() {
        val result = NoteMerge.merge(make("a"), make("a\nb", 1), "a", make("a", 2, state = NoteState.TRASH), "b")
        assertTrue(result.conflict)
        assertNull(result.merged)
    }

    @Test fun `archivar y editar el texto se fusiona`() {
        val result = NoteMerge.merge(make("a"), make("a\nb", 1), "a", make("a", 2, state = NoteState.ARCHIVED), "b")
        assertFalse(result.conflict)
        assertEquals(NoteState.ARCHIVED, result.merged!!.state)
        assertEquals("a\nb", result.merged!!.text)
    }

    @Test fun `una nota protegida nunca se fusiona`() {
        val base = make("a")
        assertTrue(NoteMerge.merge(base, make("a\nb", 1, isProtected = true), "a", make("a\nc", 2), "b").conflict)
        assertTrue(NoteMerge.merge(base, make("a\nb", 1), "a", make("a\nc", 2, isProtected = true), "b").conflict)
    }

    @Test fun `la misma linea cambiada distinto es conflicto`() {
        assertTrue(NoteMerge.merge(make("a\nb"), make("a\nB1", 1), "a", make("a\nB2", 2), "b").conflict)
    }

    @Test fun `es simetrico al intercambiar los lados`() {
        val base = make("a\nb", tags = listOf("t"))
        val local = make("a\nb\nx", 1, color = "#111111", tags = listOf("t", "u"))
        val remote = make("y\na\nb", 2, color = "#222222", pinned = true)
        val ab = NoteMerge.merge(base, local, "dev-a", remote, "dev-b")
        val ba = NoteMerge.merge(base, remote, "dev-b", local, "dev-a")
        assertFalse(ab.conflict)
        assertNotNull(ab.merged)
        assertEquals(ab.merged, ba.merged)
    }

    @Test fun `fijada, ajuste de tareas y posicion siguen al unico lado que cambia`() {
        val result = NoteMerge.merge(make("x", dock = 1.0), make("x", 1, pinned = true, policy = 1), "a", make("x", 2, dock = 2.5), "b")
        assertFalse(result.conflict)
        assertTrue(result.merged!!.pinnedToTop)
        assertEquals(1, result.merged!!.completedTasks)
        assertEquals(2.5, result.merged!!.dockPosition)
    }
}
