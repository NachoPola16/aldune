package app.aldune.text

import java.util.Locale

/** El color libre de una nota (port de `CustomColorWindow`): solo `#RRGGBB`, con la almohadilla opcional al escribirlo. */
object CustomColor {
    private val hex = Regex("#[0-9A-F]{6}")

    /** El color en `#RRGGBB` mayúsculas, o `null` si no lo es. */
    fun normalize(value: String?): String? {
        val text = value?.trim().orEmpty().uppercase(Locale.ROOT)
        if (text.isEmpty()) return null
        val withHash = if (text.startsWith('#')) text else "#$text"
        return withHash.takeIf { hex.matches(it) }
    }

    fun toHex(red: Int, green: Int, blue: Int): String =
        "#%02X%02X%02X".format(Locale.ROOT, red.coerceIn(0, 255), green.coerceIn(0, 255), blue.coerceIn(0, 255))

    /** Rojo, verde y azul de un color, o `null` si no es `#RRGGBB`. */
    fun channels(color: String): Triple<Int, Int, Int>? {
        val c = normalize(color) ?: return null
        return Triple(c.substring(1, 3).toInt(16), c.substring(3, 5).toInt(16), c.substring(5, 7).toInt(16))
    }
}
