package app.aldune.text

/** Qué cambia entre dos versiones de una nota en conflicto: la primera línea distinta y cuántas hay en total. Una línea que falta en una de las dos cuenta como vacía. */
data class ConflictDiffSummary(val line: Int, val winnerLine: String, val losingLine: String, val differingLines: Int)

/** Una línea de la otra versión tal como se pinta: [number] desde 1, sin salto de línea, y si [differs] de la que ocupa su sitio en la ganadora. */
data class DiffLine(val number: Int, val text: String, val differs: Boolean)

/** Port de `ConflictDiff` de Core. */
object ConflictDiff {
    private const val ELLIPSIS = "…"

    /**
     * Compara línea a línea (por posición, sin alinear: basta para decir «dónde»). `null` si el texto es el mismo; los finales de línea
     * de Windows y de Unix cuentan igual.
     */
    fun summarize(winnerText: String, losingText: String): ConflictDiffSummary? {
        val winner = lines(winnerText)
        val losing = lines(losingText)

        var first = 0
        var count = 0
        var firstWinner = ""
        var firstLosing = ""
        for (i in 0 until maxOf(winner.size, losing.size)) {
            val w = winner.getOrElse(i) { "" }
            val l = losing.getOrElse(i) { "" }
            if (w == l) continue
            if (count++ == 0) {
                first = i + 1
                firstWinner = w
                firstLosing = l
            }
        }
        return if (count == 0) null else ConflictDiffSummary(first, firstWinner, firstLosing, count)
    }

    /**
     * Las líneas de la versión perdedora —su texto entero, que es lo que pinta la franja— marcando las que difieren de la ganadora en
     * la misma posición. Una línea que la ganadora no tiene cuenta como vacía. Lo que le sobra a la ganadora no se lista: no es texto
     * de la otra versión (el [summarize] sí lo cuenta como diferencia).
     */
    fun lineDiff(winnerText: String, losingText: String): List<DiffLine> {
        val winner = lines(winnerText)
        return lines(losingText).mapIndexed { i, line -> DiffLine(i + 1, line, line != winner.getOrElse(i) { "" }) }
    }

    /** Recorta una línea para mostrarla en una fila: sin espacios en los extremos y con «…» si se pasa de [max]. */
    fun clip(line: String, max: Int): String {
        val trimmed = line.trimDotNet()
        return if (trimmed.length <= max) trimmed else trimmed.substring(0, max) + ELLIPSIS
    }

    private fun lines(text: String): List<String> = text.replace("\r\n", "\n").split('\n')
}
