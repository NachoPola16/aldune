package app.aldune.text

/** Viñetas (→): las mismas reglas que las tareas, con la flecha como glifo. Port de `BulletLines` de Aldune.Core. */
object BulletLines {
    const val GLYPH = '→'
    const val PREFIX = "→ "

    fun glyphIndex(line: String): Int {
        val i = ListPrefix.indentLength(line)

        if (i + 1 >= line.length) return -1
        if (line[i] != GLYPH || line[i + 1] != ' ') return -1

        return i
    }

    fun isBulletLine(line: String): Boolean = glyphIndex(line) >= 0

    fun prefixLength(line: String, glyphIndex: Int): Int =
        if (glyphIndex + 1 < line.length && line[glyphIndex + 1] == ' ') 2 else 1

    fun isEmptyBulletLine(line: String): Boolean {
        val glyph = glyphIndex(line)
        return glyph >= 0 && line.substring(glyph + prefixLength(line, glyph)).trimDotNet().isEmpty()
    }

    fun toggleBulletLineAt(text: String, caret: Int): TextAndCaret {
        val c = caret.coerceIn(0, text.length)
        val start = LineText.start(text, c)
        val end = LineText.end(text, c)
        val line = text.substring(start, end)

        val glyph = glyphIndex(line)
        if (glyph >= 0) {
            val prefixLength = prefixLength(line, glyph)
            val stripped = line.removeRange(glyph, glyph + prefixLength)
            // Un cursor en la sangría se queda donde estaba, como al poner la flecha (mismo arreglo que en TaskLines).
            val caretInLine = if (c - start <= glyph) c - start else maxOf(c - start - prefixLength, glyph)
            return TextAndCaret(text.substring(0, start) + stripped + text.substring(end), start + caretInLine)
        }

        val taskGlyph = TaskLines.glyphIndex(line)
        if (taskGlyph >= 0) {
            // Añadir la flecha delante de la casilla, sin quitarla: "☐ x" pasa a "→ ☐ x".
            val withArrow = line.insertAt(taskGlyph, PREFIX)
            val caretInLine = if (c - start >= taskGlyph) c - start + PREFIX.length else c - start
            return TextAndCaret(text.substring(0, start) + withArrow + text.substring(end), start + caretInLine)
        }

        // Reparar una viñeta antigua sin espacio, como TaskLines.toggleTaskLineAt.
        val indent = ListPrefix.indentLength(line)
        if (indent < line.length && line[indent] == GLYPH) return ListPrefix.insertSpaceAfterGlyph(text, c, start + indent)

        val prefixed = line.insertAt(indent, PREFIX)
        val newCaret = if (c >= start + indent) c + PREFIX.length else c
        return TextAndCaret(text.substring(0, start) + prefixed + text.substring(end), newCaret)
    }

    fun enterContinuation(text: String, caret: Int, newLine: String = "\r\n"): TextAndCaret? {
        val c = caret.coerceIn(0, text.length)
        val start = LineText.start(text, c)
        val end = LineText.end(text, c)
        val line = text.substring(start, end)

        val glyph = glyphIndex(line)
        if (glyph < 0) return null

        if (c != end) return null

        if (isEmptyBulletLine(line)) {
            val cleared = line.substring(0, glyph).trimEndDotNet()
            return TextAndCaret(text.substring(0, start) + cleared + text.substring(end), start + cleared.length)
        }

        val indent = line.substring(0, glyph)
        val inserted = newLine + indent + PREFIX
        return TextAndCaret(text.substring(0, c) + inserted + text.substring(c), c + inserted.length)
    }
}
