package app.aldune.text

enum class LineDirection { Up, Down }

/** Mover la línea del cursor arriba o abajo (Alt+↑/↓). Port de `LineMovement`. */
object LineMovement {
    /** `null` si la línea ya está en el borde. Intercambia CONTENIDOS, nunca terminadores, y el cursor sigue a su línea. */
    fun move(text: String, caret: Int, direction: LineDirection): TextAndCaret? {
        val c = caret.coerceIn(0, text.length)
        val (contents, terminators) = splitLines(text)

        var lineIndex = contents.size - 1
        var lineStart = 0
        var consumed = 0
        for (i in contents.indices) {
            if (c <= consumed + contents[i].length) {
                lineIndex = i
                lineStart = consumed
                break
            }
            consumed += contents[i].length + terminators[i].length
        }

        val offsetInLine = c - lineStart
        val targetIndex = if (direction == LineDirection.Up) lineIndex - 1 else lineIndex + 1
        if (targetIndex < 0 || targetIndex >= contents.size) return null

        // Solo se intercambia el CONTENIDO, nunca los terminadores: cada posición lleva su propio
        // terminador (línea normal -> "\r\n"/"\n", última línea -> "") porque depende de dónde
        // queda esa posición en la lista, no de qué texto se haya movido a vivir ahí.
        val moved = contents[lineIndex]
        contents[lineIndex] = contents[targetIndex]
        contents[targetIndex] = moved

        val builder = StringBuilder()
        for (i in contents.indices) builder.append(contents[i]).append(terminators[i])

        var newLineStart = 0
        for (i in 0 until targetIndex) newLineStart += contents[i].length + terminators[i].length
        val newCaret = newLineStart + minOf(offsetInLine, contents[targetIndex].length)

        return TextAndCaret(builder.toString(), newCaret)
    }

    private fun splitLines(text: String): Pair<MutableList<String>, MutableList<String>> {
        val contents = mutableListOf<String>()
        val terminators = mutableListOf<String>()
        var start = 0

        for (i in text.indices) {
            if (text[i] != '\n') continue

            val hasCr = i > start && text[i - 1] == '\r'
            val contentEnd = if (hasCr) i - 1 else i
            contents.add(text.substring(start, contentEnd))
            terminators.add(if (hasCr) "\r\n" else "\n")
            start = i + 1
        }

        contents.add(text.substring(start))
        terminators.add("")
        return contents to terminators
    }
}
