package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertNull
import kotlin.test.assertTrue

class ModelAndStoreTest {
    private val t0 = Instant.parse("2026-01-01T00:00:00Z")

    private fun note(id: String = "n1", text: String = "hola", ending: LineEnding = LineEnding.LF) = Note(
        id = id, text = text, lineEnding = ending, color = "#FFDDD4", createdAt = t0, updatedAt = t0, state = NoteState.ACTIVE,
    )

    @Test fun `los estados viajan como 0, 1 y 2`() {
        assertEquals(listOf(0, 1, 2), NoteState.entries.map { it.code })
        assertEquals(NoteState.TRASHED, NoteState.fromCode(2))
    }

    @Test fun `un estado desconocido no se inventa`() {
        assertNull(NoteState.fromCode(3))
        assertNull(NoteState.fromCode(-1))
    }

    @Test fun `una nota con CRLF dentro del texto es un error de programa`() {
        assertFailsWith<IllegalArgumentException> { note(text = "a\r\nb") }
    }

    @Test fun `el texto de sync recupera el salto de línea original`() {
        assertEquals("a\r\nb", note(text = "a\nb", ending = LineEnding.CRLF).wireText)
        assertEquals("a\nb", note(text = "a\nb", ending = LineEnding.LF).wireText)
    }

    @Test fun `se detecta el salto de línea por el primero que aparece`() {
        assertEquals(LineEnding.CRLF, LineEnding.detect("a\r\nb\nc"))
        assertEquals(LineEnding.LF, LineEnding.detect("a\nb\r\nc"))
        assertEquals(LineEnding.LF, LineEnding.detect("sin saltos"))
        assertEquals(LineEnding.LF, LineEnding.detect(""))
    }

    @Test fun `normalizar convierte CRLF en LF y no toca los demas caracteres`() {
        assertEquals("a\nb\nc\r", LineEnding.normalize("a\r\nb\nc\r"))
    }

    @Test fun `put y get devuelven la misma nota y put reemplaza`() {
        val store = InMemoryNoteStore()
        assertNull(store.get("n1"))
        store.put(note())
        store.put(note(text = "otra"))
        assertEquals("otra", store.get("n1")!!.text)
        assertEquals(1, store.all().size)
    }

    @Test fun `delete quita la nota y deja una lapida local`() {
        val store = InMemoryNoteStore()
        store.put(note())
        assertTrue(store.delete("n1", t0))
        assertNull(store.get("n1"))
        assertEquals(listOf(Tombstone("n1", t0, Tombstone.LOCAL_DEVICE)), store.tombstones())
    }

    @Test fun `delete de una nota que no existe no deja lapida`() {
        val store = InMemoryNoteStore()
        assertFalse(store.delete("nada", t0))
        assertTrue(store.tombstones().isEmpty())
    }

    @Test fun `el almacen no comparte instancias mutables con quien lo usa`() {
        val store = InMemoryNoteStore()
        val tags = mutableListOf("a")
        store.put(note().copy(tags = tags))
        tags.add("b")
        assertEquals(listOf("a"), store.get("n1")!!.tags)
    }
}
