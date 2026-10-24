package app.aldune.text

/** Mezcla lineal en sRGB entre dos `#RRGGBB` (port de `ColorMix` de Core): la de las maquetas; en OKLCH no saldría igual. */
object ColorMix {
    /** `amount` 0 = [from], 1 = [to]. Con un color inválido devuelve [from] tal cual. */
    fun toward(from: String, to: String, amount: Double): String {
        val a = OklchColor.parseHex(from) ?: return from
        val b = OklchColor.parseHex(to) ?: return from
        val t = amount.coerceIn(0.0, 1.0)
        // Core redondea la mitad hacia arriba (MidpointRounding.AwayFromZero); los canales nunca son negativos.
        fun channel(x: Int, y: Int) = Math.floor(x + (y - x) * t + 0.5).toInt()
        return "#%02X%02X%02X".format(
            channel((a shr 16) and 0xFF, (b shr 16) and 0xFF),
            channel((a shr 8) and 0xFF, (b shr 8) and 0xFF),
            channel(a and 0xFF, b and 0xFF),
        )
    }
}
