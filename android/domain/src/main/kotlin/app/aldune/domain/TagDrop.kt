package app.aldune.domain

/**
 * Dropping a note on another tag in the grouped list: which tags it ends up with and where it lands among the notes of that tag.
 * Pure, so the rules the gesture relies on are tested without a screen.
 */
object TagDrop {
    /** The notes next to the spot where a note was let go (`null` = the start or the end of the group). */
    class Place(val before: String?, val after: String?) {
        val hasNeighbours: Boolean get() = before != null || after != null
    }

    /**
     * Where the dragged note lands in the target group. [inTarget] is the ids of its notes in list order (without the dragged one); [onId] is
     * the note it was let go over, or `null` for the group's header (the note goes first); [below] says it was over the lower half of that note.
     */
    fun place(inTarget: List<String>, onId: String?, below: Boolean): Place {
        // A note that is gone by now counts as the end of the group, so the drop never fails for a list that changed under the finger.
        val at = if (onId == null) 0 else (inTarget.indexOf(onId).takeIf { it >= 0 } ?: inTarget.size).let { if (below) it + 1 else it }
        return Place(inTarget.getOrNull(at - 1), inTarget.getOrNull(at))
    }

    /** Where in [others] (the whole list without the dragged note) the note is inserted to sit at [place]. */
    fun insertIndex(others: List<String>, place: Place): Int = when {
        place.after != null -> others.indexOf(place.after).takeIf { it >= 0 } ?: others.size
        place.before != null -> others.indexOf(place.before).let { if (it >= 0) it + 1 else others.size }
        else -> others.size
    }

    /** The tags of a note moved from [from] to [to] (`null` = no tag): drops the first, adds the second and leaves the rest alone. */
    fun retag(tags: List<String>, from: String?, to: String?): List<String> {
        val kept = tags.filterNot { from != null && it.equals(from, ignoreCase = true) }
        return if (to != null && kept.none { it.equals(to, ignoreCase = true) }) kept + to else kept
    }
}
