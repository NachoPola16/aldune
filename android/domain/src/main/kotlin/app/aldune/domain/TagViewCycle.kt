package app.aldune.domain

/**
 * Moving between «All» and the tags one at a time (a swipe over the empty part of the list). The views form a ring: All, then the tags in the
 * order they are shown, then back to All. `null` is All.
 */
object TagViewCycle {
    enum class Swipe { NEXT, PREVIOUS, NONE }

    /** The view after (or before, with [forward] false) [current]; a tag that is gone counts as All. */
    fun next(tags: List<String>, current: String?, forward: Boolean): String? {
        if (tags.isEmpty()) return null
        val ring = listOf<String?>(null) + tags
        val at = if (current == null) 0 else ring.indexOfFirst { it != null && it.equals(current, ignoreCase = true) }.coerceAtLeast(0)
        return ring[(at + if (forward) 1 else -1).mod(ring.size)]
    }

    /**
     * What a finished drag means: a pull to the left goes to the next view, to the right to the previous one. It has to travel at least [minimum]
     * and be mostly sideways, so a scroll that drifts a little never changes the view.
     */
    fun swipe(dx: Float, dy: Float, minimum: Float): Swipe = when {
        kotlin.math.abs(dx) < minimum || kotlin.math.abs(dx) < 2 * kotlin.math.abs(dy) -> Swipe.NONE
        dx < 0 -> Swipe.NEXT
        else -> Swipe.PREVIOUS
    }
}
