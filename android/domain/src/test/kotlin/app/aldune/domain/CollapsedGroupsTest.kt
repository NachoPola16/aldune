package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertTrue

class CollapsedGroupsTest {
    @Test
    fun `what is folded comes back after a restart`() {
        val folded = setOf("casa", "trabajo")
        assertEquals(folded, CollapsedGroups.decode(CollapsedGroups.encode(folded)))
    }

    @Test
    fun `the no tag group is stored without the control character of its key`() {
        val encoded = CollapsedGroups.encode(setOf("casa", TagGrouping.UNTAGGED))
        assertTrue(encoded.none { it.code < 32 && it != '\n' }, "control character in $encoded")
        assertEquals(setOf("casa", TagGrouping.UNTAGGED), CollapsedGroups.decode(encoded))
    }

    @Test
    fun `nothing folded is an empty string and an empty string is nothing folded`() {
        assertEquals("", CollapsedGroups.encode(emptySet()))
        assertEquals(emptySet(), CollapsedGroups.decode(""))
    }

    @Test
    fun `toggle folds an open group and opens a folded one`() {
        assertEquals(setOf("a"), CollapsedGroups.toggle(emptySet(), "a"))
        assertEquals(emptySet(), CollapsedGroups.toggle(setOf("a"), "a"))
    }

    @Test
    fun `groups of tags that no longer exist are dropped`() {
        val kept = CollapsedGroups.prune(setOf("casa", "gone", TagGrouping.UNTAGGED), existing = listOf("Casa", "work"))
        assertEquals(setOf("casa", TagGrouping.UNTAGGED), kept)
        assertFalse("gone" in kept)
    }
}
