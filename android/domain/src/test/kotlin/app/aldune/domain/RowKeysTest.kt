package app.aldune.domain

import org.junit.jupiter.api.Assertions.assertFalse
import org.junit.jupiter.api.Assertions.assertTrue
import org.junit.jupiter.api.Test

class RowKeysTest {
    private val id = "3f2b8c1e-aaaa-bbbb-cccc-0123456789ab"

    @Test
    fun `a plain row key is the note id`() {
        assertTrue(RowKeys.shows(id, id))
    }

    @Test
    fun `a grouped row key carries the tag before the id`() {
        assertTrue(RowKeys.shows("work/$id", id))
        assertTrue(RowKeys.shows("__untagged/$id", id))
    }

    @Test
    fun `other notes and headers do not match`() {
        assertFalse(RowKeys.shows("work/other-id", id))
        assertFalse(RowKeys.shows("work", id))
        assertFalse(RowKeys.shows(42, id))
    }
}
