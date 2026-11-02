package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class TagDropTest {
    private val inTarget = listOf("a", "b", "c")

    @Test
    fun `dropped on the header the note goes first`() {
        val place = TagDrop.place(inTarget, onId = null, below = false)
        assertNull(place.before)
        assertEquals("a", place.after)
    }

    @Test
    fun `dropped on the upper half of a note it goes before it`() {
        val place = TagDrop.place(inTarget, onId = "b", below = false)
        assertEquals("a", place.before)
        assertEquals("b", place.after)
    }

    @Test
    fun `dropped on the lower half of a note it goes after it`() {
        val place = TagDrop.place(inTarget, onId = "b", below = true)
        assertEquals("b", place.before)
        assertEquals("c", place.after)
    }

    @Test
    fun `dropped on the lower half of the last note it goes to the end`() {
        val place = TagDrop.place(inTarget, onId = "c", below = true)
        assertEquals("c", place.before)
        assertNull(place.after)
    }

    @Test
    fun `an empty group has no neighbours`() {
        val place = TagDrop.place(emptyList(), onId = null, below = false)
        assertNull(place.before)
        assertNull(place.after)
        assertEquals(false, place.hasNeighbours)
    }

    @Test
    fun `a note that is no longer in the group counts as the end of it`() {
        val place = TagDrop.place(inTarget, onId = "gone", below = false)
        assertEquals("c", place.before)
        assertNull(place.after)
    }

    @Test
    fun `retag swaps the tag and leaves the others`() {
        assertEquals(listOf("x", "new"), TagDrop.retag(listOf("old", "x"), from = "old", to = "new"))
    }

    @Test
    fun `retag matches names ignoring case and does not repeat a tag`() {
        assertEquals(listOf("Home"), TagDrop.retag(listOf("work", "Home"), from = "WORK", to = "home"))
    }

    @Test
    fun `retag from no tag only adds and to no tag only removes`() {
        assertEquals(listOf("a"), TagDrop.retag(emptyList(), from = null, to = "a"))
        assertEquals(emptyList(), TagDrop.retag(listOf("a"), from = "a", to = null))
    }

    @Test
    fun `undoing a retag gives the original tags back`() {
        val start = listOf("a", "z")
        assertEquals(start, TagDrop.retag(TagDrop.retag(start, "a", "b"), "b", "a").sorted())
    }

    @Test
    fun `insertion index follows the neighbours in the full list`() {
        val others = listOf("p", "a", "q", "b")
        assertEquals(1, TagDrop.insertIndex(others, TagDrop.Place(before = null, after = "a")))
        assertEquals(4, TagDrop.insertIndex(others, TagDrop.Place(before = "b", after = null)))
        assertEquals(3, TagDrop.insertIndex(others, TagDrop.Place(before = "a", after = "b")))
        assertEquals(4, TagDrop.insertIndex(others, TagDrop.Place(before = null, after = null)))
    }
}
