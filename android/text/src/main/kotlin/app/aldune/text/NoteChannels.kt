package app.aldune.text

/**
 * Canales del osciloscopio (aspectos Telecom): cada nota es un canal según su puesto en la lista, y cada canal tiene su color y su onda.
 * Port de `NoteChannels` de Core.
 */
object NoteChannels {
    const val COUNT = 4

    // Senoidal, cuadrada, triangular y diente de sierra, en una caja de 54×18 (las de las maquetas de Windows).
    private val waves = listOf(
        "M0 9 C4.5 0 9 0 13.5 9 S22.5 18 27 9 S36 0 40.5 9 S49.5 18 54 9",
        "M0 15 H7 V3 H20 V15 H33 V3 H46 V15 H54",
        "M0 15 L9 3 L18 15 L27 3 L36 15 L45 3 L54 15",
        "M0 15 L13 3 V15 L26 3 V15 L39 3 V15 L52 3 V15",
    )

    const val WAVE_WIDTH = 54f
    const val WAVE_HEIGHT = 18f

    fun of(position: Int): Int = ((position % COUNT) + COUNT) % COUNT

    fun number(channel: Int): String = "CH${of(channel) + 1}"

    fun paletteKey(channel: Int): String = "Channel${of(channel) + 1}"

    fun wavePath(channel: Int): String = waves[of(channel)]

    /**
     * Cómo se pinta una nota en el canal [channel] (la piel «tintada» de Windows): cara = el color del canal muy aclarado hacia el fondo, y
     * el título en el color del canal si se lee sobre esa cara. Con «mismo color en todas las notas» ese color sustituye a los canales,
     * pasado por [NoteColorDerivation.stripeColor] (tal cual casi se confundiría con la cara).
     */
    fun tint(channel: Int, palette: Map<String, String>, uniformColor: String?): Tint {
        val ground = palette.getValue("Ground")
        val text = palette.getValue("Text")
        val accent = if (NoteDisplayColor.isActive(uniformColor)) NoteColorDerivation.stripeColor(NoteDisplayColor.resolve("", uniformColor), ground)
        else palette.getValue(paletteKey(channel))
        val face = ColorMix.toward(accent, ground, 0.88)
        return Tint(face = face, accent = accent, title = if (NoteColorContrast.isReadable(face, accent)) accent else text)
    }

    data class Tint(val face: String, val accent: String, val title: String)
}
