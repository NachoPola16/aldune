package app.aldune.text

/** Sangría de listas: Tab y Mayús+Tab, con el cursor y con una selección de varias líneas. Port de `ListIndent`. */
object ListIndent {
    private const val UNIT = "    " // 4 espacios por nivel

    const val INDENT_UNIT = UNIT

    /** Tab: en una línea de lista sangra la línea entera; en cualquier otra inserta la sangría en el cursor. */
    fun indent(text: String, caret: Int): TextAndCaret {
        val c = caret.coerceIn(0, text.length)
        val start = LineText.start(text, c)
        val end = LineText.end(text, c)
        val line = text.substring(start, end)

        if (!isListLine(line)) return TextAndCaret(text.insertAt(c, UNIT), c + UNIT.length)

        val indented = UNIT + line
        return TextAndCaret(text.substring(0, start) + indented + text.substring(end), c + UNIT.length)
    }

    /** Mayús+Tab: quita hasta un nivel del principio de la línea (una tabulación cuenta como un nivel). */
    fun outdent(text: String, caret: Int): TextAndCaret {
        val c = caret.coerceIn(0, text.length)
        val start = LineText.start(text, c)
        val end = LineText.end(text, c)
        val line = text.substring(start, end)

        var removable = 0
        if (line.startsWith('\t')) {
            removable = 1
        } else {
            while (removable < UNIT.length && removable < line.length && line[removable] == ' ') removable++
        }

        if (removable == 0) return TextAndCaret(text, c)

        val outdented = line.substring(removable)
        val caretInLine = maxOf(c - start - removable, 0)
        return TextAndCaret(text.substring(0, start) + outdented + text.substring(end), start + caretInLine)
    }

    fun indentLines(text: String, selectionStart: Int, selectionLength: Int): SelectionEdit =
        mapLines(text, selectionStart, selectionLength) { line -> if (line.trimEndCr().isEmpty()) line else UNIT + line }

    fun outdentLines(text: String, selectionStart: Int, selectionLength: Int): SelectionEdit =
        mapLines(text, selectionStart, selectionLength) { line -> outdent(line, 0).text }

    private fun mapLines(text: String, selectionStart: Int, selectionLength: Int, map: (String) -> String): SelectionEdit {
        val from = selectionStart.coerceIn(0, text.length)
        var selectionEnd = (from + selectionLength).coerceIn(from, text.length)
        if (selectionEnd > from && selectionEnd == LineText.start(text, selectionEnd)) selectionEnd--

        val start = LineText.start(text, from)
        val end = LineText.end(text, selectionEnd)

        // Se parte por '\n' y el '\r' de un salto de Windows se queda al final de su línea, fuera
        // de la sangría, que siempre va al principio.
        val replaced = text.substring(start, end).split('\n').joinToString("\n") { map(it) }
        val result = text.substring(0, start) + replaced + text.substring(end)
        return SelectionEdit(result, start, replaced.length)
    }

    fun isListLineAt(text: String, caret: Int): Boolean {
        val c = caret.coerceIn(0, text.length)
        return isListLine(text.substring(LineText.start(text, c), LineText.end(text, c)))
    }

    private fun isListLine(line: String) = TaskLines.isTaskLine(line) || BulletLines.isBulletLine(line)
}

/** `(string Text, int SelectionStart, int SelectionLength)` de `IndentLines`/`OutdentLines`. */
data class SelectionEdit(val text: String, val selectionStart: Int, val selectionLength: Int)
