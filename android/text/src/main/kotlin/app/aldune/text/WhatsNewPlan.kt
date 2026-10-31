package app.aldune.text

/**
 * Which versions' news to show after an update (port of `Aldune.Core.WhatsNewPlan`, here by `versionCode`).
 *
 * A fresh install shows nothing; an install that never stored a version is taken to be the last release without this feature
 * ([BASELINE_FOR_UNKNOWN]), so its first update lists everything since.
 */
object WhatsNewPlan {
    /** The `versionCode` of the last release before «What's new» existed (Android 1.2.0 is 18; 17 is 1.1.0). */
    const val BASELINE_FOR_UNKNOWN = 17

    /** The version codes to list, newest first: those after the last one seen, up to and including the running one. */
    fun toShow(lastSeen: Int, freshInstall: Boolean, current: Int, available: Collection<Int>): List<Int> {
        if (freshInstall) return emptyList()
        val from = if (lastSeen > 0) lastSeen else BASELINE_FOR_UNKNOWN
        return available.filter { it > from && it <= current }.sortedDescending()
    }
}
