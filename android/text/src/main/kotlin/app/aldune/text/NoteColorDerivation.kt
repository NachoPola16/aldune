package app.aldune.text

/**
 * Borde, etiqueta y franja de una nota derivados del color de su cara, nunca hex sueltos. Port de `NoteColorDerivation`.
 */
object NoteColorDerivation {
    /** Por debajo de esta claridad OKLCH la cara se trata como oscura. */
    const val DARK_THRESHOLD = 0.6

    private class Classic(val face: String, val rim: String, val label: String)

    private val classic = listOf(
        Classic("#EBD38B", "#B7A059", "#614E00"), // citron  oklch(0.87 0.095 92)
        Classic("#AAE6B1", "#78B280", "#006123"), // sage    oklch(0.87 0.095 148)
        Classic("#83E7F2", "#4CB3BE", "#005B63"), // sky     oklch(0.87 0.095 205)
        Classic("#C2D4FF", "#90A1CA", "#3A4C83"), // iris    oklch(0.87 0.063 268)
        Classic("#F9BEF2", "#C38CBE", "#743170"), // rose    oklch(0.87 0.095 330)
        Classic("#FFC5AF", "#C9937E", "#7D391D"), // amber   oklch(0.87 0.073 42)
    )

    /** Las seis caras del tema Clásico, en orden. */
    val classicColors: List<String> = classic.map { it.face }

    fun isDark(color: String?): Boolean = OklchColor.fromHex(color)?.let { it.l < DARK_THRESHOLD } ?: false

    fun rimFor(color: String): String {
        findClassic(color)?.let { return it.rim }
        val face = OklchColor.fromHex(color) ?: return color

        return if (face.l < DARK_THRESHOLD) face.copy(l = minOf(face.l + 0.07, 1.0)).toHex()
        else face.copy(l = maxOf(face.l - 0.16, 0.0)).toHex()
    }

    fun labelFor(color: String): String {
        findClassic(color)?.let { return it.label }
        val face = OklchColor.fromHex(color) ?: return NoteColorContrast.foregroundFor(color)

        val label = if (face.l < DARK_THRESHOLD) OklchColor(0.86, minOf(face.c * 1.6, 0.08), face.h).toHex()
        else face.copy(l = maxOf(face.l - 0.44, 0.0)).toHex()

        return if (NoteColorContrast.isReadable(color, label)) label else NoteColorContrast.foregroundFor(color)
    }

    /** Contorno de una nota oscura en reposo; `null` en una clara (no lo necesita). */
    fun restOutlineFor(color: String?): String? {
        val face = OklchColor.fromHex(color)
        if (face == null || face.l >= DARK_THRESHOLD) return null

        return OklchColor(maxOf(face.l + 0.2, 0.47), minOf(face.c * 1.3, 0.08), face.h).toHex()
    }

    fun stripeColor(color: String, ground: String): String {
        val face = OklchColor.fromHex(color) ?: return color
        val lightness = if (isDark(ground)) 0.72 else 0.5
        val chroma = if (face.c < 0.02) face.c else maxOf(face.c, 0.09)
        return OklchColor(lightness, chroma, face.h).toHex()
    }

    private fun findClassic(color: String?): Classic? = classic.firstOrNull { it.face.equals(color, ignoreCase = true) }
}
