package app.aldune.domain

import java.time.Instant
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class NoteServiceImportTest {
    @Test fun `an imported note keeps its dates, state, tags and pin in a single write`() {
        val f = Fixture()
        val created = Instant.parse("2020-05-06T07:08:09Z")
        val updated = Instant.parse("2021-05-06T07:08:09Z")

        val note = f.service.importNote("Title\nbody", "#ABCDEF", created, updated, NoteState.ARCHIVED, listOf("a", "A", " b "), true).getOrThrow()

        assertEquals(1, f.store.writeCount)
        assertEquals(note, f.stored(note.id))
        assertEquals(created, note.createdAt)
        assertEquals(updated, note.updatedAt)
        assertEquals(NoteState.ARCHIVED, note.state)
        assertEquals(listOf("a", "b"), note.tags)
        assertTrue(note.pinnedToTop)
        assertEquals(null, note.syncAuthorDeviceId)
        assertEquals(LineEnding.LF, note.lineEnding)
    }

    @Test fun `crlf text is normalised and empty text is refused`() {
        val f = Fixture()
        val at = f.clock.current
        assertEquals("a\nb", f.service.importNote("a\r\nb", "#ABCDEF", at, at, NoteState.ACTIVE, emptyList(), false).getOrThrow().text)
        assertTrue(f.service.importNote("", "#ABCDEF", at, at, NoteState.ACTIVE, emptyList(), false) is NoteResult.Err)
        assertEquals(1, f.store.writeCount)
    }
}
