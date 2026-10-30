package app.aldune.text

/** Nombre corriente de un color, para decirlo en voz alta (TalkBack) en vez de su código hexadecimal. */
enum class ColorName { RED, ORANGE, YELLOW, GREEN, TURQUOISE, BLUE, PURPLE, PINK, BROWN, GRAY, WHITE, BLACK }

object ColorNames {
    /**
     * El nombre más cercano de `#RRGGBB`, o `null` si no es un color. Se reparte por tono (HSV) en bandas anchas: no pretende ser un
     * diccionario de colores, sino que quien no ve la nota sepa distinguir el amarillo del azul. Un rojo pálido se dice rosa y un
     * naranja oscuro, marrón; los casi sin saturación son blanco, gris o negro según su luminosidad.
     */
    fun of(hex: String): ColorName? {
        val hsv = ColorHsv.fromHex(hex) ?: return null
        val (hue, saturation, value) = hsv
        if (saturation < 0.12f || value < 0.15f) {
            return when {
                value < 0.2f -> ColorName.BLACK
                value > 0.92f -> ColorName.WHITE
                else -> ColorName.GRAY
            }
        }
        return when {
            hue < 15f || hue >= 345f -> if (saturation < 0.45f && value > 0.8f) ColorName.PINK else ColorName.RED
            hue < 40f -> if (value < 0.6f) ColorName.BROWN else ColorName.ORANGE
            hue < 70f -> if (value < 0.55f) ColorName.BROWN else ColorName.YELLOW
            hue < 165f -> ColorName.GREEN
            hue < 200f -> ColorName.TURQUOISE
            hue < 255f -> ColorName.BLUE
            hue < 300f -> ColorName.PURPLE
            else -> ColorName.PINK
        }
    }
}
