package app.aldune.domain

/** Storage form of the colour each tag gives its notes (a setting of this device, not synced): one `tag<TAB>#RRGGBB` per line. */
object TagColors {
    fun encode(colors: Map<String, String>): String = colors.entries.joinToString("\n") { "${it.key}\t${it.value}" }

    fun decode(raw: String): Map<String, String> = raw.lineSequence()
        .map { it.split('\t', limit = 2) }
        .filter { it.size == 2 && it[0].isNotBlank() && it[1].isNotBlank() }
        .associate { it[0] to it[1] }

    /** Sets or, with null, removes the colour of [tag] (names compare ignoring case; the spelling given is kept). */
    fun with(colors: Map<String, String>, tag: String, color: String?): Map<String, String> {
        val rest = colors.filterKeys { !it.equals(tag, ignoreCase = true) }
        return if (color == null) rest else rest + (tag to color)
    }
}
