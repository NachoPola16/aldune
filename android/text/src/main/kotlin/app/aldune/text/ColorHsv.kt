package app.aldune.text

import kotlin.math.abs
import kotlin.math.floor
import kotlin.math.max
import kotlin.math.min
import kotlin.math.roundToInt

/** Tono (0–360), saturación y valor (0–1) de un color: la base del selector visual de color. */
data class Hsv(val hue: Float, val saturation: Float, val value: Float)

object ColorHsv {
    /** `#RRGGBB` → HSV, o `null` si no es un color válido. En un gris el tono es 0. */
    fun fromHex(hex: String): Hsv? {
        val (r, g, b) = CustomColor.channels(hex) ?: return null
        val rf = r / 255f
        val gf = g / 255f
        val bf = b / 255f
        val top = max(rf, max(gf, bf))
        val delta = top - min(rf, min(gf, bf))
        val hue = when {
            delta == 0f -> 0f
            top == rf -> 60f * (((gf - bf) / delta + 6f) % 6f)
            top == gf -> 60f * ((bf - rf) / delta + 2f)
            else -> 60f * ((rf - gf) / delta + 4f)
        }
        return Hsv(hue, if (top == 0f) 0f else delta / top, top)
    }

    /** HSV → `#RRGGBB`; los valores fuera de rango se recortan. */
    fun toHex(hue: Float, saturation: Float, value: Float): String {
        val h = ((hue % 360f) + 360f) % 360f
        val s = saturation.coerceIn(0f, 1f)
        val v = value.coerceIn(0f, 1f)
        val c = v * s
        val x = c * (1f - abs((h / 60f) % 2f - 1f))
        val m = v - c
        val (r, g, b) = when (floor(h / 60f).toInt()) {
            0 -> Triple(c, x, 0f)
            1 -> Triple(x, c, 0f)
            2 -> Triple(0f, c, x)
            3 -> Triple(0f, x, c)
            4 -> Triple(x, 0f, c)
            else -> Triple(c, 0f, x)
        }
        return CustomColor.toHex(((r + m) * 255f).roundToInt(), ((g + m) * 255f).roundToInt(), ((b + m) * 255f).roundToInt())
    }

    /**
     * La rejilla del selector: cada tono en versión suave y viva, y una fila de grises. Son atajos para encontrar el color a ojo; el ajuste fino
     * y el HEX siguen disponibles. Los grises van al final.
     */
    val swatches: List<String> = buildList {
        val hues = (0 until 12).map { it * 30f }
        for ((s, v) in listOf(0.35f to 1f, 0.8f to 0.9f)) for (h in hues) add(toHex(h, s, v))
        for (v in listOf(1f, 0.85f, 0.7f, 0.5f, 0.3f, 0.12f)) add(toHex(0f, 0f, v))
    }
}
