package app.aldune.domain

import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNull

class TagViewCycleTest {
    private val tags = listOf("home", "work", "ideas")

    @Test
    fun `forward goes from all to the first tag and on in order`() {
        assertEquals("home", TagViewCycle.next(tags, current = null, forward = true))
        assertEquals("work", TagViewCycle.next(tags, "home", forward = true))
        assertEquals("ideas", TagViewCycle.next(tags, "work", forward = true))
    }

    @Test
    fun `forward from the last tag wraps to all`() {
        assertNull(TagViewCycle.next(tags, "ideas", forward = true))
    }

    @Test
    fun `backward goes the other way and wraps to the last tag`() {
        assertEquals("ideas", TagViewCycle.next(tags, current = null, forward = false))
        assertEquals("home", TagViewCycle.next(tags, "work", forward = false))
        assertNull(TagViewCycle.next(tags, "home", forward = false))
    }

    @Test
    fun `names match ignoring case`() {
        assertEquals("work", TagViewCycle.next(tags, "HOME", forward = true))
    }

    @Test
    fun `a tag that no longer exists counts as all`() {
        assertEquals("home", TagViewCycle.next(tags, "gone", forward = true))
        assertEquals("ideas", TagViewCycle.next(tags, "gone", forward = false))
    }

    @Test
    fun `with no tags there is nowhere to go`() {
        assertNull(TagViewCycle.next(emptyList(), null, forward = true))
        assertNull(TagViewCycle.next(emptyList(), "x", forward = false))
    }

    @Test
    fun `a swipe counts only when it is long enough and mostly horizontal`() {
        assertEquals(TagViewCycle.Swipe.NEXT, TagViewCycle.swipe(dx = -200f, dy = 20f, minimum = 120f))
        assertEquals(TagViewCycle.Swipe.PREVIOUS, TagViewCycle.swipe(dx = 200f, dy = -20f, minimum = 120f))
        assertEquals(TagViewCycle.Swipe.NONE, TagViewCycle.swipe(dx = 60f, dy = 0f, minimum = 120f))
        assertEquals(TagViewCycle.Swipe.NONE, TagViewCycle.swipe(dx = 150f, dy = 200f, minimum = 120f))
    }
}
