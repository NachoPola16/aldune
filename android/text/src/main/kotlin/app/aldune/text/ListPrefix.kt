package app.aldune.text

/** Lo que comparten las tareas (☐) y las viñetas (→): la sangría y el retroceso sobre el prefijo. */
object ListPrefix {
    /**
     * Retroceso con el cursor justo después del glifo (o de su espacio): quita el prefijo entero, sin dejar la casilla ni la
     * flecha a medias. `null` si no toca hacer nada y el editor debe borrar un carácter como siempre.
     */
    fun removeOnBackspace(text: String, caret: Int): TextAndCaret? {
        if (caret <= 0 || caret > text.length) return null

        val start = LineText.start(text, caret)
        val line = text.substring(start, LineText.end(text, caret))

        val caretInLine = caret - start

        // Con "→ ☐ x" el retroceso quita lo que tiene justo delante: la casilla si el cursor está tras ella, la
        // flecha si está entre la flecha y la casilla.
        var glyph = TaskLines.glyphIndex(line)
        if (glyph >= 0 && (caretInLine == glyph + 1 || caretInLine == glyph + 2))
            return TextAndCaret(text.removeRange(start + glyph, start + glyph + 2), start + glyph)

        glyph = BulletLines.glyphIndex(line)
        if (glyph >= 0 && (caretInLine == glyph + 1 || caretInLine == glyph + 2))
            return TextAndCaret(text.removeRange(start + glyph, start + glyph + 2), start + glyph)

        return null
    }

    internal fun indentLength(line: String): Int {
        var i = 0
        while (i < line.length && (line[i] == ' ' || line[i] == '\t')) i++
        return i
    }

    internal fun insertSpaceAfterGlyph(text: String, caret: Int, glyphAt: Int): TextAndCaret =
        TextAndCaret(text.insertAt(glyphAt + 1, " "), if (caret > glyphAt) caret + 1 else caret)
}
