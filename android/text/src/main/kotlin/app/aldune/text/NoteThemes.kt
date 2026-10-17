package app.aldune.text

import java.util.Locale

/** Los temas de serie y la resolución/limpieza de los propios. Port de `NoteThemes`; los colores están copiados literales. */
object NoteThemes {
    const val CLASSIC_ID = "classic"
    const val SERENE_ID = "serene"
    const val GRAPHITE_ID = "graphite"
    const val GRAY_ID = "gray"
    const val XP_ID = "xp"
    const val PASTEL_ID = "pastel"
    const val AUTUMN_ID = "autumn"
    const val OCEAN_ID = "ocean"

    /** Los temas de serie, en el orden de Core; el primero (Clásico) es el de reserva. */
    val builtIn: List<NoteTheme> = listOf(
        NoteTheme(id = CLASSIC_ID, name = "Clásico", isBuiltIn = true, lightColors = NoteColorDerivation.classicColors),
        NoteTheme(
            id = SERENE_ID, name = "Sereno", isBuiltIn = true,
            // Oscuros L 0.31 C 0.052 cada 45°, claros L 0.925 C 0.042 cada 60°.
            darkColors = listOf("#472525", "#422A11", "#36310E", "#1F371F", "#033936", "#113447", "#2B2D4A", "#3E273F"),
            lightColors = listOf("#FFDDD4", "#EFE7C7", "#D3EFD8", "#C7EFF4", "#D9E7FF", "#F5DDF6"),
        ),
        NoteTheme(
            id = GRAY_ID, name = "Grafito", isBuiltIn = true,
            // Grises neutros: entre grises sin matiz solo se distinguen por la claridad, así que son una escalera de cuatro pasos.
            darkColors = listOf("#191B1D", "#252729", "#313336", "#3E4043"),
            lightColors = listOf("#F1F4F8", "#E4E6EA", "#D7D9DD", "#CACCD0"),
        ),
        NoteTheme(
            id = PASTEL_ID, name = "Pastel", isBuiltIn = true,
            // L 0.91, C 0.055. Solo claros: un pastel oscuro deja de serlo.
            lightColors = listOf("#FFD3E6", "#FFD8C6", "#ECE2B9", "#C3EDD5", "#BDE9FF", "#E3DCFF"),
        ),
        NoteTheme(
            id = AUTUMN_ID, name = "Otoño", isBuiltIn = true,
            darkColors = listOf("#512631", "#52281E", "#4A2F08", "#3D3605", "#2A3C15"), // L 0.33
            lightColors = listOf("#FEC6D2", "#FFC9BB", "#F4D1A9", "#E1D9A8", "#C9E1B4"), // L 0.88
        ),
        NoteTheme(
            id = OCEAN_ID, name = "Océano", isBuiltIn = true,
            darkColors = listOf("#1B3914", "#003A2F", "#00373F", "#093351", "#2A2B53"), // L 0.31
            lightColors = listOf("#C6EABE", "#ADEEDB", "#A7EBF8", "#C2E2FF", "#D7DBFF"), // L 0.90
        ),
        NoteTheme(
            id = XP_ID, name = "XP", isBuiltIn = true,
            // Solo claros, L 0.95: amarillo, azul, verde, melocotón, lila, turquesa.
            lightColors = listOf("#F7F1D0", "#E0F1FF", "#DCF9E1", "#FFE6DD", "#FFE7FF", "#CFFAF8"),
        ),
    )

    /** Los colores de Sereno de antes de la 1.5, que se migran al paleta nueva. */
    val legacySereneDarkColors: List<String> =
        listOf("#2E3034", "#26323E", "#262F47", "#1D3538", "#283426", "#3C2D21", "#462527", "#392A3C")

    val legacySereneLightColors: List<String> =
        listOf("#EBE6D9", "#EBE5E0", "#DEE8F0", "#DEEADE", "#F0E4D7", "#F2E2E1")

    /** Color antiguo (en mayúsculas) → color nuevo de Sereno, por posición. Buscar con [recolorSerene], que no distingue mayúsculas. */
    val sereneRecolorMap: Map<String, String> = buildSereneRecolorMap()

    private fun buildSereneRecolorMap(): Map<String, String> {
        val serene = builtIn.first { it.id == SERENE_ID }
        val map = LinkedHashMap<String, String>()
        legacySereneDarkColors.forEachIndexed { i, old -> map[old.uppercase(Locale.ROOT)] = serene.darkColors[i] }
        legacySereneLightColors.forEachIndexed { i, old -> map[old.uppercase(Locale.ROOT)] = serene.lightColors[i] }
        return map
    }

    fun recolorSerene(color: String): String? = sereneRecolorMap[color.uppercase(Locale.ROOT)]

    fun all(custom: Iterable<NoteTheme>?): List<NoteTheme> = builtIn + (custom ?: emptyList())

    /** El tema con ese id (sensible a mayúsculas); si no existe, Clásico. */
    fun resolve(id: String?, custom: Iterable<NoteTheme>?): NoteTheme = all(custom).firstOrNull { it.id == id } ?: builtIn[0]

    /**
     * Limpia los temas propios que vienen de un fichero: sin id, con id de un tema de serie o repetido, sin ningún color válido
     * → fuera; colores inválidos fuera y el resto en mayúsculas; sin nombre, el nombre es el id.
     */
    fun sanitize(custom: Iterable<NoteTheme>?): List<NoteTheme> {
        val builtInIds = builtIn.map { it.id }.toSet()
        val seen = HashSet<String>()
        val clean = mutableListOf<NoteTheme>()

        for (theme in custom ?: emptyList()) {
            if (theme.id.isNullOrWhiteSpaceDotNet()) continue
            if (theme.id in builtInIds || !seen.add(theme.id)) continue

            val dark = cleanColors(theme.darkColors)
            val light = cleanColors(theme.lightColors)
            if (dark.isEmpty() && light.isEmpty()) continue

            clean.add(
                NoteTheme(
                    id = theme.id,
                    name = if (theme.name.isNullOrWhiteSpaceDotNet()) theme.id else theme.name.trimDotNet(),
                    darkColors = dark,
                    lightColors = light,
                )
            )
        }

        return clean
    }

    private fun cleanColors(colors: List<String>?): List<String> =
        (colors ?: emptyList()).filter { OklchColor.fromHex(it) != null }.map { it.uppercase(Locale.ROOT) }
}
