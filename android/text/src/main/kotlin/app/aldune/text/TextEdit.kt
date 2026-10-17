package app.aldune.text

/**
 * Resultado de editar un texto por líneas (quitar, reordenar) junto con el mapa de posiciones viejas → nuevas, para que la
 * interfaz no mueva el cursor al reordenar. Port de `TextEdit` de Aldune.Core.
 */
class TextEdit private constructor(
    val text: String,
    val changed: Boolean,
    // Por cada línea vieja: dónde empezaba, cuánto medía su contenido y en qué línea nueva acabó (-1 si
    // se borró). Por cada línea nueva: dónde empieza y cuánto mide su contenido.
    private val oldStarts: IntArray,
    private val oldLengths: IntArray,
    private val newLineOfOld: IntArray,
    private val newStarts: IntArray,
    private val newLengths: IntArray,
) {
    /** Dónde queda en el texto nuevo una posición del texto original: dentro de su misma línea, o donde empieza la siguiente que sobrevive. */
    fun mapIndex(oldIndex: Int): Int {
        if (oldStarts.isEmpty()) return oldIndex.coerceIn(0, text.length)

        var line = oldStarts.size - 1
        for (i in 0 until oldStarts.size - 1) {
            if (oldIndex < oldStarts[i + 1]) { line = i; break }
        }

        val target = newLineOfOld[line]
        if (target >= 0) {
            val offset = (oldIndex - oldStarts[line]).coerceIn(0, newLengths[target])
            return minOf(newStarts[target] + offset, text.length)
        }

        for (next in line + 1 until newLineOfOld.size) {
            if (newLineOfOld[next] >= 0) return newStarts[newLineOfOld[next]]
        }
        return text.length
    }

    internal class Lines private constructor(
        val contents: List<String>,
        val terminators: List<String>,
        val starts: IntArray,
    ) {
        val count: Int get() = contents.size

        companion object {
            fun parse(text: String): Lines {
                val contents = mutableListOf<String>()
                val terminators = mutableListOf<String>()
                val starts = mutableListOf<Int>()
                var start = 0
                for (i in text.indices) {
                    if (text[i] != '\n') continue
                    val hasCr = i > start && text[i - 1] == '\r'
                    starts.add(start)
                    contents.add(text.substring(start, if (hasCr) i - 1 else i))
                    terminators.add(if (hasCr) "\r\n" else "\n")
                    start = i + 1
                }
                starts.add(start)
                contents.add(text.substring(start))
                terminators.add("")
                return Lines(contents, terminators, starts.toIntArray())
            }
        }
    }

    companion object {
        /** Sin cambios: todo se queda donde estaba (el mapa solo recorta al largo del texto). */
        fun unchanged(text: String) = TextEdit(text, false, IntArray(0), IntArray(0), IntArray(0), IntArray(0), IntArray(0))

        /** Cambio de caracteres sueltos sin mover líneas: las posiciones no cambian. */
        internal fun inPlace(original: String, text: String) =
            TextEdit(text, original != text, IntArray(0), IntArray(0), IntArray(0), IntArray(0), IntArray(0))

        internal fun removeLines(text: String, removed: Set<Int>): TextEdit {
            val lines = Lines.parse(text)
            if (removed.isEmpty()) return unchanged(text)

            val kept = (0 until lines.count).filter { it !in removed }
            val terminators = kept.map { lines.terminators[it] }.toMutableList()
            if (terminators.isNotEmpty()) terminators[terminators.size - 1] = ""
            return build(text, lines, kept, terminators)
        }

        internal fun reorder(text: String, order: List<Int>): TextEdit {
            val lines = Lines.parse(text)
            return build(text, lines, order, lines.terminators)
        }

        private fun build(original: String, lines: Lines, order: List<Int>, terminators: List<String>): TextEdit {
            val builder = StringBuilder(original.length)
            val newStarts = IntArray(order.size)
            val newLengths = IntArray(order.size)
            val newLineOfOld = IntArray(lines.count) { -1 }

            for (position in order.indices) {
                val old = order[position]
                newStarts[position] = builder.length
                newLengths[position] = lines.contents[old].length
                newLineOfOld[old] = position
                builder.append(lines.contents[old]).append(terminators[position])
            }

            val text = builder.toString()
            return TextEdit(
                text, original != text,
                lines.starts, IntArray(lines.count) { lines.contents[it].length }, newLineOfOld, newStarts, newLengths
            )
        }
    }
}
