package app.aldune.text

/**
 * Un color en OKLCH (claridad, croma, matiz en grados). Port de `OklchColor` de Aldune.Core: mismas constantes y mismo
 * orden de operaciones, porque los `#RRGGBB` de los vectores se comparan exactos.
 */
data class OklchColor(val l: Double, val c: Double, val h: Double) {
    /** `#RRGGBB`; si queda fuera de la gama sRGB se baja el croma (búsqueda binaria de 30 pasos) hasta que cabe. */
    fun toHex(): String {
        var rgb = toLinearRgb(l, c, h)
        if (!inGamut(rgb)) {
            var low = 0.0
            var high = c
            for (i in 0 until 30) {
                val mid = (low + high) / 2
                if (inGamut(toLinearRgb(l, mid, h))) low = mid else high = mid
            }
            rgb = toLinearRgb(l, low, h)
        }

        return "#%02X%02X%02X".format(toByte(rgb.r), toByte(rgb.g), toByte(rgb.b))
    }

    /** Distancia euclídea en OKLab: lo que el ojo nota como «distinto». */
    fun distanceTo(other: OklchColor): Double {
        val (a1, b1) = toAb(this)
        val (a2, b2) = toAb(other)
        val dl = l - other.l
        val da = a1 - a2
        val db = b1 - b2
        return Math.sqrt(dl * dl + da * da + db * db)
    }

    private class Rgb(val r: Double, val g: Double, val b: Double)

    companion object {
        private const val GAMUT_TOLERANCE = 1e-4

        /** `null` si no es exactamente `#` y seis dígitos hexadecimales ASCII. */
        fun fromHex(hex: String?): OklchColor? {
            val rgb = parseHex(hex) ?: return null
            val r = toLinear((rgb shr 16) and 0xFF)
            val g = toLinear((rgb shr 8) and 0xFF)
            val b = toLinear(rgb and 0xFF)

            val l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b)
            val m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b)
            val s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b)

            val lightness = 0.2104542553 * l + 0.7936177850 * m - 0.0040720468 * s
            val a = 1.9779984951 * l - 2.4285922050 * m + 0.4505937099 * s
            val bb = 0.0259040371 * l + 0.7827717662 * m - 0.8086757660 * s

            val hue = Math.atan2(bb, a) * 180 / Math.PI
            return OklchColor(lightness, Math.sqrt(a * a + bb * bb), if (hue < 0) hue + 360 else hue)
        }

        /** El entero RGB de un `#RRGGBB`, o `null`. Compartido con el contraste (`Uri.IsHexDigit`: solo ASCII). */
        internal fun parseHex(hex: String?): Int? {
            if (hex == null || hex.length != 7 || hex[0] != '#') return null
            var value = 0
            for (i in 1 until 7) {
                val digit = when (hex[i]) {
                    in '0'..'9' -> hex[i] - '0'
                    in 'a'..'f' -> hex[i] - 'a' + 10
                    in 'A'..'F' -> hex[i] - 'A' + 10
                    else -> return null
                }
                value = (value shl 4) or digit
            }
            return value
        }

        /** Canal sRGB (0-255) → luz lineal. */
        internal fun toLinear(channel: Int): Double {
            val value = channel / 255.0
            return if (value <= 0.04045) value / 12.92 else Math.pow((value + 0.055) / 1.055, 2.4)
        }

        private fun toAb(color: OklchColor): Pair<Double, Double> {
            val radians = color.h * Math.PI / 180
            return color.c * Math.cos(radians) to color.c * Math.sin(radians)
        }

        private fun toLinearRgb(lightness: Double, chroma: Double, hue: Double): Rgb {
            val (a, b) = toAb(OklchColor(lightness, chroma, hue))
            val l = Math.pow(lightness + 0.3963377774 * a + 0.2158037573 * b, 3.0)
            val m = Math.pow(lightness - 0.1055613458 * a - 0.0638541728 * b, 3.0)
            val s = Math.pow(lightness - 0.0894841775 * a - 1.2914855480 * b, 3.0)
            return Rgb(
                4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
                -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
                -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s
            )
        }

        private fun inGamut(rgb: Rgb): Boolean =
            rgb.r >= -GAMUT_TOLERANCE && rgb.r <= 1 + GAMUT_TOLERANCE
                && rgb.g >= -GAMUT_TOLERANCE && rgb.g <= 1 + GAMUT_TOLERANCE
                && rgb.b >= -GAMUT_TOLERANCE && rgb.b <= 1 + GAMUT_TOLERANCE

        private fun toByte(linear: Double): Int {
            val clamped = linear.coerceIn(0.0, 1.0)
            val encoded = if (clamped <= 0.0031308) 12.92 * clamped else 1.055 * Math.pow(clamped, 1 / 2.4) - 0.055
            // Math.Round de .NET redondea la mitad al par; en Java eso es rint (Math.round y roundToInt redondean hacia arriba).
            return Math.rint(encoded.coerceIn(0.0, 1.0) * 255).toInt()
        }
    }
}
