package app.aldune.text

/** Elige el color de una nota nueva según el tema, el tono y la regla de reparto. Port de `NoteColorAssigner`. */
object NoteColorAssigner {
    private const val NEIGHBOR_COUNT = 2

    /** `dockColors` son los colores de las notas del mazo, en orden. */
    fun assign(
        theme: NoteTheme, tone: NoteTone, rule: NoteColorAssignment,
        fixedColor: String?, dockColors: List<String>,
    ): String {
        val candidates = candidates(theme, wantsDark(tone, dockColors))

        return when (rule) {
            NoteColorAssignment.Fixed -> fixedColor(theme, fixedColor) ?: candidates[0]
            NoteColorAssignment.Rotate -> candidates[rotateIndex(candidates, dockColors)]
            NoteColorAssignment.MostDistinct -> mostDistinct(candidates, dockColors)
            else -> avoidNeighbors(candidates, dockColors)
        }
    }

    private fun fixedColor(theme: NoteTheme, fixedColor: String?): String? {
        if (fixedColor == null) return null
        val dark = indexOf(theme.darkColors, fixedColor)
        if (dark >= 0) return theme.darkColors[dark]
        val light = indexOf(theme.lightColors, fixedColor)
        return if (light >= 0) theme.lightColors[light] else null
    }

    private fun wantsDark(tone: NoteTone, dockColors: List<String>): Boolean = when (tone) {
        NoteTone.Dark -> true
        NoteTone.Light -> false
        // Alternando: oscura si no hay nada o si la última es clara.
        else -> dockColors.isEmpty() || !NoteColorDerivation.isDark(dockColors.last())
    }

    private fun candidates(theme: NoteTheme, dark: Boolean): List<String> {
        val preferred = if (dark) theme.darkColors else theme.lightColors
        val other = if (dark) theme.lightColors else theme.darkColors
        if (preferred.isNotEmpty()) return preferred
        if (other.isNotEmpty()) return other
        return NoteColorDerivation.classicColors
    }

    private fun rotateIndex(candidates: List<String>, dockColors: List<String>): Int {
        for (i in dockColors.indices.reversed()) {
            val index = indexOf(candidates, dockColors[i])
            if (index >= 0) return (index + 1) % candidates.size
        }
        return 0
    }

    private fun avoidNeighbors(candidates: List<String>, dockColors: List<String>): String {
        val start = rotateIndex(candidates, dockColors)
        val neighbors = dockColors.drop(maxOf(0, dockColors.size - NEIGHBOR_COUNT))

        for (step in candidates.indices) {
            val candidate = candidates[(start + step) % candidates.size]
            if (indexOf(neighbors, candidate) < 0) return candidate
        }

        // Tema con tan pocos colores que todos están al lado: rotar sin más.
        return candidates[start]
    }

    private fun mostDistinct(candidates: List<String>, dockColors: List<String>): String {
        val existing = dockColors.mapNotNull { OklchColor.fromHex(it) }
        if (existing.isEmpty()) return candidates[0]

        var best = candidates[0]
        var bestDistance = -Double.MAX_VALUE
        for (candidate in candidates) {
            val oklch = OklchColor.fromHex(candidate) ?: continue
            val nearest = existing.minOf { oklch.distanceTo(it) }
            // Estrictamente mayor: en empate gana el primero en el orden del tema.
            if (nearest > bestDistance) {
                bestDistance = nearest
                best = candidate
            }
        }
        return best
    }

    private fun indexOf(colors: List<String>, color: String): Int = colors.indexOfFirst { it.equals(color, ignoreCase = true) }
}
