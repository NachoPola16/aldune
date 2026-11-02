package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals

class KnownTagsTest {
    @Test
    fun `a tag on a note is remembered`() {
        assertEquals(listOf("a", "b"), KnownTags.remember(listOf("a"), inUse = listOf("a", "b")))
    }

    @Test
    fun `remembering keeps the order it already had and adds the new ones last`() {
        assertEquals(listOf("z", "a", "m"), KnownTags.remember(listOf("z", "a"), inUse = listOf("m", "A")))
    }

    @Test
    fun `a tag that no note carries any more is still remembered`() {
        assertEquals(listOf("empty"), KnownTags.remember(listOf("empty"), inUse = emptyList()))
    }

    @Test
    fun `the same tag in another case is not added twice`() {
        assertEquals(listOf("Home"), KnownTags.remember(listOf("Home"), inUse = listOf("home", "HOME")))
    }

    @Test
    fun `nothing new to remember is reported as such`() {
        assertEquals(emptyList(), KnownTags.fresh(listOf("a", "b"), inUse = listOf("B", "a")))
        assertEquals(listOf("c"), KnownTags.fresh(listOf("a"), inUse = listOf("c", "C", "a")))
    }

    @Test
    fun `forgetting removes the tag whatever its case and leaves the rest`() {
        assertEquals(listOf("b"), KnownTags.forget(listOf("A", "b"), "a"))
    }

    @Test
    fun `forgetting a tag that is not there changes nothing`() {
        assertEquals(listOf("a"), KnownTags.forget(listOf("a"), "zzz"))
    }

    @Test
    fun `renaming keeps the place of the tag`() {
        assertEquals(listOf("a", "new", "c"), KnownTags.rename(listOf("a", "old", "c"), "OLD", "new"))
    }

    @Test
    fun `renaming onto an existing tag does not leave two`() {
        assertEquals(listOf("x", "y"), KnownTags.rename(listOf("x", "y", "old"), "old", "Y"))
    }

    @Test
    fun `a tag deleted on purpose and then seen again on no note stays forgotten`() {
        val afterDelete = KnownTags.forget(listOf("a", "b"), "a")
        assertEquals(listOf("b"), KnownTags.remember(afterDelete, inUse = listOf("b")))
    }
}
