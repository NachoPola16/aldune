package app.aldune.domain.importing

import app.aldune.text.OklchColor

/** Name or hex colour of another app to [ImportHue]; null for neutrals. */
object ImportHues {
    // OKLCH hues of the six colour families (the same ones the Classic note colours are built on).
    private val targets = listOf(
        ImportHue.Orange to 42.0, ImportHue.Yellow to 92.0, ImportHue.Green to 148.0,
        ImportHue.Cyan to 205.0, ImportHue.Blue to 268.0, ImportHue.Pink to 330.0,
    )

    /** Below this chroma a colour reads as grey and has no family worth matching. */
    internal const val NEUTRAL_CHROMA = 0.03

    // Names used by Google Keep. Purple has no family of its own: Blue is the nearest one in hue.
    private val names = mapOf(
        "yellow" to ImportHue.Yellow, "green" to ImportHue.Green, "teal" to ImportHue.Cyan, "cerulean" to ImportHue.Cyan,
        "cyan" to ImportHue.Cyan, "blue" to ImportHue.Blue, "purple" to ImportHue.Blue, "pink" to ImportHue.Pink,
        "red" to ImportHue.Orange, "orange" to ImportHue.Orange, "brown" to ImportHue.Orange,
    )

    fun fromName(name: String?): ImportHue? = name?.trim()?.lowercase()?.let { names[it] }

    fun fromHex(hex: String?): ImportHue? {
        val color = OklchColor.fromHex(hex) ?: return null
        if (color.c < NEUTRAL_CHROMA) return null
        return targets.minBy { hueDistance(it.second, color.h) }.first
    }

    internal fun degrees(hue: ImportHue): Double = targets.first { it.first == hue }.second

    internal fun hueDistance(a: Double, b: Double): Double {
        val d = Math.abs(a - b) % 360
        return if (d > 180) 360 - d else d
    }
}
