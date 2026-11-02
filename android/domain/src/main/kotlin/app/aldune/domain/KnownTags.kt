package app.aldune.domain

/**
 * The tags the device remembers (a local list, never synced): a tag whose last note moved elsewhere stays, empty, until it is deleted on
 * purpose, as on Windows where tags are a table. Names compare ignoring case.
 */
object KnownTags {
    /** The tags in [inUse] that [known] does not have yet, once each. */
    fun fresh(known: List<String>, inUse: List<String>): List<String> {
        val keys = known.map { it.lowercase() }.toHashSet()
        return inUse.filter { it.lowercase() !in keys }.distinctBy { it.lowercase() }
    }

    /** [known] plus the tags now in use that it lacks, at the end. A tag nobody carries any more stays. */
    fun remember(known: List<String>, inUse: List<String>): List<String> = known + fresh(known, inUse)

    /** Forgets [tag], called once no note carries it (a refresh in between would have remembered it again). */
    fun forget(known: List<String>, tag: String): List<String> = known.filterNot { it.equals(tag, ignoreCase = true) }

    /** [from] becomes [to] in the same place; if [to] was already there, the list does not end up with two. */
    fun rename(known: List<String>, from: String, to: String): List<String> =
        known.map { if (it.equals(from, ignoreCase = true)) to else it }.distinctBy { it.lowercase() }
}
