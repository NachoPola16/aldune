package app.aldune.domain

/**
 * The tag groups of the grouped list that are folded, remembered between runs (a local setting: it is how this device likes to see the list,
 * not data of the notes, and it never syncs). Keys are the lower-case tag names, and [TagGrouping.UNTAGGED] for «No tag».
 */
object CollapsedGroups {
    // The key of the «no tag» group holds a control character that a settings file cannot store, so it is written as a word no tag key looks like.
    private const val UNTAGGED_TOKEN = "::untagged::"

    fun encode(folded: Set<String>): String =
        folded.map { if (it == TagGrouping.UNTAGGED) UNTAGGED_TOKEN else it }.sorted().joinToString("\n")

    fun decode(raw: String): Set<String> =
        raw.split('\n').filter { it.isNotBlank() }.map { if (it == UNTAGGED_TOKEN) TagGrouping.UNTAGGED else it }.toSet()

    fun toggle(folded: Set<String>, key: String): Set<String> = if (key in folded) folded - key else folded + key

    /** Forgets the groups of tags that are gone (deleted or renamed), so the list does not carry them for ever. */
    fun prune(folded: Set<String>, existing: List<String>): Set<String> {
        val keys = existing.map { it.lowercase() }.toHashSet()
        return folded.filter { it == TagGrouping.UNTAGGED || it in keys }.toSet()
    }
}
