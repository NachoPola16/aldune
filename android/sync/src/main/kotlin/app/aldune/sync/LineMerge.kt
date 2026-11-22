package app.aldune.sync

data class LineMergeResult(val text: String, val conflict: Boolean)

/**
 * Fusión de un texto por líneas con tres versiones (diff3): port de `LineMerge` de Aldune.Core; su contrato son los vectores de
 * `tests/vectors/merge-vectors.json`. Cada lado se compara con la base; los cambios que no se solapan se aplican juntos, el mismo
 * cambio en los dos lados cuenta una vez, y dos inserciones en el mismo sitio se conservan por orden ordinal (unidades UTF-16),
 * de modo que el resultado no depende de qué lado es el «local». Solo es conflicto un cambio solapado y distinto.
 * El final de línea del resultado es el de la base; si un lado no cambió se devuelve el otro tal cual.
 */
object LineMerge {
    // Por encima de este número de celdas no se hace el diff cuadrático: el trozo distinto se toma como un solo bloque reemplazado
    // (solo puede dar un conflicto, nunca una fusión equivocada).
    private const val MAX_DIFF_CELLS = 4_000_000L

    private class Hunk(val start: Int, val end: Int, val lines: List<String>) {
        val isInsert: Boolean get() = start == end
        fun sameAs(other: Hunk) = start == other.start && end == other.end && lines == other.lines
    }

    fun merge(baseText: String, localText: String, remoteText: String): LineMergeResult {
        if (localText == remoteText || localText == baseText) return LineMergeResult(remoteText, false)
        if (remoteText == baseText) return LineMergeResult(localText, false)

        val eol = when {
            baseText.contains("\r\n") -> "\r\n"
            baseText.contains('\n') -> "\n"
            localText.contains("\r\n") || remoteText.contains("\r\n") -> "\r\n"
            else -> "\n"
        }

        val baseLines = split(baseText)
        val local = diff(baseLines, split(localText))
        val remote = diff(baseLines, split(remoteText))

        val output = ArrayList<String>()
        var position = 0
        var i = 0
        var j = 0

        fun copyBaseUpTo(end: Int) {
            while (position < end) output += baseLines[position++]
        }

        while (i < local.size || j < remote.size) {
            val l = local.getOrNull(i)
            val r = remote.getOrNull(j)

            if (l != null && r != null) {
                if (l.isInsert && r.isInsert && l.start == r.start) {
                    copyBaseUpTo(l.start)
                    if (l.sameAs(r)) {
                        output += l.lines
                    } else {
                        val first = if (l.lines.joinToString("\n") <= r.lines.joinToString("\n")) l else r
                        val second = if (first === l) r else l
                        output += first.lines
                        output += second.lines
                    }
                    i++
                    j++
                    continue
                }
                if (overlap(l, r)) {
                    if (!l.sameAs(r)) return LineMergeResult(split(localText).joinToString("\n"), true)
                    copyBaseUpTo(l.start)
                    output += l.lines
                    position = l.end
                    i++
                    j++
                    continue
                }
            }

            // El que empieza antes va primero; con el mismo comienzo, una inserción va antes que un reemplazo.
            val takeLocal = r == null || (l != null && (l.start < r.start || (l.start == r.start && l.isInsert)))
            val next = checkNotNull(if (takeLocal) l else r)
            copyBaseUpTo(next.start)
            output += next.lines
            position = next.end
            if (takeLocal) i++ else j++
        }

        copyBaseUpTo(baseLines.size)
        return LineMergeResult(output.joinToString(eol), false)
    }

    private fun overlap(a: Hunk, b: Hunk): Boolean = when {
        a.isInsert && b.isInsert -> a.start == b.start
        a.isInsert -> b.start < a.start && a.start < b.end
        b.isInsert -> a.start < b.start && b.start < a.end
        else -> a.start < b.end && b.start < a.end
    }

    private fun split(text: String): List<String> = text.replace("\r\n", "\n").split('\n')

    /** Los tramos de líneas de la base (posiblemente vacíos) que pasaron a ser otras líneas. */
    private fun diff(baseLines: List<String>, other: List<String>): List<Hunk> {
        val hunks = ArrayList<Hunk>()
        var prefix = 0
        while (prefix < baseLines.size && prefix < other.size && baseLines[prefix] == other[prefix]) prefix++
        var suffix = 0
        while (suffix < baseLines.size - prefix && suffix < other.size - prefix &&
            baseLines[baseLines.size - 1 - suffix] == other[other.size - 1 - suffix]
        ) suffix++

        val n = baseLines.size - prefix - suffix
        val m = other.size - prefix - suffix
        if (n == 0 && m == 0) return hunks
        if (n == 0 || m == 0 || n.toLong() * m > MAX_DIFF_CELLS) {
            hunks += Hunk(prefix, prefix + n, other.subList(prefix, prefix + m).toList())
            return hunks
        }

        // Subsecuencia común más larga del trozo que difiere.
        val table = Array(n + 1) { IntArray(m + 1) }
        for (a in n - 1 downTo 0) {
            for (b in m - 1 downTo 0) {
                table[a][b] = if (baseLines[prefix + a] == other[prefix + b]) table[a + 1][b + 1] + 1
                else maxOf(table[a + 1][b], table[a][b + 1])
            }
        }

        var x = 0
        var y = 0
        var runBase = 0
        var runOther = 0
        var open = false
        fun close(baseEnd: Int, otherEnd: Int) {
            if (!open) return
            hunks += Hunk(prefix + runBase, prefix + baseEnd, other.subList(prefix + runOther, prefix + otherEnd).toList())
            open = false
        }
        while (x < n || y < m) {
            if (x < n && y < m && baseLines[prefix + x] == other[prefix + y]) {
                close(x, y)
                x++
                y++
                continue
            }
            if (!open) {
                open = true
                runBase = x
                runOther = y
            }
            if (y < m && (x == n || table[x][y + 1] >= table[x + 1][y])) y++ else x++
        }
        close(x, y)
        return hunks
    }
}
