package app.aldune.text

/** Contraste WCAG entre el color de una nota y su texto. Port de `NoteColorContrast`. */
object NoteColorContrast {
    const val INK = "#1E1A14"
    const val WHITE = "#FFFFFF"
    const val BLACK = "#000000"
    const val MINIMUM_CONTRAST = 4.5

    /** La tinta de siempre si se lee sobre ese fondo; si no, blanco o negro, lo que contraste. Un fondo ilegible usa la tinta. */
    fun foregroundFor(background: String?): String {
        val luminance = luminance(background) ?: return INK
        val inkLuminance = luminance(INK)!!
        if (contrast(luminance, inkLuminance) >= MINIMUM_CONTRAST) return INK
        return if (contrast(luminance, 1.0) >= MINIMUM_CONTRAST) WHITE else BLACK
    }

    fun isReadable(background: String?, foreground: String?): Boolean {
        val backgroundLuminance = luminance(background) ?: return false
        val foregroundLuminance = luminance(foreground) ?: return false
        return contrast(backgroundLuminance, foregroundLuminance) >= MINIMUM_CONTRAST
    }

    /** Luminancia relativa de un `#RRGGBB`, o `null` si no lo es. */
    fun luminance(color: String?): Double? {
        val rgb = OklchColor.parseHex(color) ?: return null
        return 0.2126 * OklchColor.toLinear((rgb shr 16) and 0xFF) +
            0.7152 * OklchColor.toLinear((rgb shr 8) and 0xFF) +
            0.0722 * OklchColor.toLinear(rgb and 0xFF)
    }

    private fun contrast(first: Double, second: Double): Double =
        (maxOf(first, second) + 0.05) / (minOf(first, second) + 0.05)
}
