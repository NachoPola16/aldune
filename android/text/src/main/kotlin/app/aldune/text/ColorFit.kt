package app.aldune.text

import java.util.Locale

/**
 * Un color elegido por el usuario tiene que seguir dejando leer el texto que va encima o al lado (port de `ColorFit` de Core): se mueve su
 * claridad OKLCH lo justo, conservando matiz y croma, que es lo que el usuario está eligiendo.
 */
object ColorFit {
    /** Contraste WCAG entre dos `#RRGGBB`; 1 si alguno no es válido. */
    fun ratio(a: String, b: String): Double {
        val la = NoteColorContrast.luminance(a) ?: return 1.0
        val lb = NoteColorContrast.luminance(b) ?: return 1.0
        return (maxOf(la, lb) + 0.05) / (minOf(la, lb) + 0.05)
    }

    /** Mueve el fondo hasta que [text] se lea con al menos [minimum]. */
    fun background(background: String, text: String, minimum: Double): String = adjust(background, text, minimum)

    /** Mueve el texto hasta que se lea sobre [background] con al menos [minimum]. */
    fun foreground(foreground: String, background: String, minimum: Double): String = adjust(foreground, background, minimum)

    fun lighter(color: String, amount: Double): String = shift(color, amount)

    fun darker(color: String, amount: Double): String = shift(color, -amount)

    private fun shift(color: String, amount: Double): String =
        OklchColor.fromHex(color)?.let { it.copy(l = (it.l + amount).coerceIn(0.0, 1.0)).toHex() } ?: color

    private fun adjust(movable: String, fixedColor: String, minimum: Double): String {
        val color = OklchColor.fromHex(movable) ?: return movable
        val fixedLuminance = NoteColorContrast.luminance(fixedColor) ?: return movable
        if (ratio(movable, fixedColor) >= minimum) return movable.uppercase(Locale.ROOT)

        // Contra un color claro se oscurece; contra uno oscuro se aclara. 0.18 es la luminancia en la que el negro y el blanco dan el mismo contraste.
        val darken = fixedLuminance > 0.18
        var low = if (darken) 0.0 else color.l
        var high = if (darken) color.l else 1.0
        var best = OklchColor(if (darken) 0.0 else 1.0, color.c, color.h).toHex()
        // Búsqueda binaria de la claridad más cercana a la elegida que todavía llega al mínimo.
        repeat(30) {
            val middle = (low + high) / 2
            val candidate = color.copy(l = middle).toHex()
            val readable = ratio(candidate, fixedColor) >= minimum
            if (darken) {
                if (readable) { best = candidate; low = middle } else high = middle
            } else {
                if (readable) { best = candidate; high = middle } else low = middle
            }
        }
        return best
    }
}
