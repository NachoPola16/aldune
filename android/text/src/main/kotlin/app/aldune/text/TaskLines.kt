package app.aldune.text

/**
 * Tareas dentro del texto de una nota: líneas que empiezan por una casilla (☐ pendiente, ☒ o ☑ hecha) y un espacio.
 * Port de `TaskLines` de Aldune.Core, con las mismas comprobaciones en el mismo orden.
 */
object TaskLines {
    const val UNCHECKED = '☐'
    const val CHECKED = '☒'
    const val CHECKED_ALTERNATE = '☑'
    const val PREFIX = "☐ "

    private const val DEFAULT_NEW_LINE = "\r\n"

    fun glyphIndex(line: String): Int {
        var i = ListPrefix.indentLength(line)

        // Una flecha delante no impide que sea tarea: "→ ☐ x" es una viñeta con casilla (flecha y casilla son dos
        // interruptores independientes). El índice que se devuelve es el de la casilla, no el de la flecha.
        if (i + 1 < line.length && line[i] == BulletLines.GLYPH && line[i + 1] == ' ') i += 2
        else i += markerLength(line, i)

        if (i + 1 >= line.length) return -1
        if (!isBoxGlyph(line[i]) || !endsGlyph(line[i + 1])) return -1

        return i
    }

    fun isTaskLine(line: String): Boolean = glyphIndex(line) >= 0

    /**
     * How much room (with its space) a plain list marker in front of the box takes: "- ", "• ", "* "... or a short number ("1. ", "12) ").
     * Like the arrow, it does not stop the line from being a task. 0 when there is none. Same as `MarkerLength` in Aldune.Core.
     */
    internal fun markerLength(line: String, i: Int): Int {
        if (i + 1 >= line.length) return 0
        if (line[i] in "-–—•*+·" && line[i + 1] == ' ') return 2
        var j = i
        while (j < line.length && line[j] in '0'..'9') j++
        if (j > i && j - i <= 3 && j + 1 < line.length && (line[j] == '.' || line[j] == ')') && line[j + 1] == ' ') return j + 2 - i
        return 0
    }

    /**
     * The space right behind the box belongs to it: the caret cannot stay between the glyph and that space (nor can anything be typed there), so it
     * moves to the other side: behind the space, or in front of the glyph when the caret came from the right ([movingLeft]), so the left arrow does
     * not get stuck. Returns [caret] as is when it does not need to move. Same as `ClampCaret` in Aldune.Core.
     */
    fun clampCaret(text: String, caret: Int, movingLeft: Boolean = false): Int {
        if (caret < 0 || caret > text.length) return caret
        val start = lineStart(text, caret)
        val line = text.substring(start, lineEnd(text, caret))
        val glyph = glyphIndex(line)
        return if (glyph >= 0 && caret - start == glyph + 1 && glyph + 1 < line.length && line[glyph + 1] == ' ') {
            if (movingLeft) caret - 1 else caret + 1
        } else caret
    }

    /**
     * Always leaves a space between the box and what follows on the caret's line: a task with a sign glued to it ("☐-", from pasted or synced
     * text) gets it here. The caret moves with the text if it was behind the glyph. Same as `EnsureGlyphSpace` in Aldune.Core.
     */
    fun ensureGlyphSpace(text: String, caret: Int): TextAndCaret {
        if (caret < 0 || caret > text.length) return TextAndCaret(text, caret)
        val start = lineStart(text, caret)
        val line = text.substring(start, lineEnd(text, caret))
        val glyph = glyphIndex(line)
        if (glyph < 0 || glyph + 1 >= line.length || line[glyph + 1] == ' ') return TextAndCaret(text, caret)
        val at = start + glyph + 1
        return TextAndCaret(text.substring(0, at) + " " + text.substring(at), if (caret >= at) caret + 1 else caret)
    }

    fun prefixLength(line: String, glyphIndex: Int): Int =
        if (glyphIndex + 1 < line.length && line[glyphIndex + 1] == ' ') 2 else 1

    fun isEmptyTaskLine(line: String): Boolean {
        val glyph = glyphIndex(line)
        return glyph >= 0 && line.substring(glyph + prefixLength(line, glyph)).trimDotNet().isEmpty()
    }

    fun lineContaining(text: String, index: Int): String = text.substring(lineStart(text, index), lineEnd(text, index))

    internal fun startsWithBoxGlyph(line: String): Boolean {
        val i = ListPrefix.indentLength(line)
        return i < line.length && isBoxGlyph(line[i])
    }

    fun isChecked(line: String): Boolean {
        val glyph = glyphIndex(line)
        return glyph >= 0 && isCheckedGlyph(line[glyph])
    }

    /** What may follow the box for the line to stay a task: a space, or a sign (a dash, a dot...) but not a word or another box. Same as `EndsGlyph` in Aldune.Core. */
    private fun endsGlyph(c: Char) = c == ' ' || !(c.isLetterOrDigit() || isBoxGlyph(c))

    private fun isBoxGlyph(c: Char) = c == UNCHECKED || isCheckedGlyph(c)

    private fun isCheckedGlyph(c: Char) = c == CHECKED || c == CHECKED_ALTERNATE

    /** Cambia la casilla que está justo en `index`, o `null` si ahí no hay una casilla que sea el prefijo de su línea. */
    fun toggleCheckboxAt(text: String, index: Int): String? {
        if (index < 0 || index >= text.length) return null
        if (!isBoxGlyph(text[index])) return null

        val start = lineStart(text, index)
        val end = lineEnd(text, index)
        val line = text.substring(start, end)

        // Solo cuenta si ese glifo es el prefijo de SU línea, no uno escrito más adelante.
        if (glyphIndex(line) != index - start) return null

        val flipped = if (isCheckedGlyph(text[index])) UNCHECKED else CHECKED
        return text.substring(0, index) + flipped + text.substring(index + 1)
    }

    fun toggleTaskLineAt(text: String, caret: Int): TextAndCaret {
        val c = caret.coerceIn(0, text.length)
        val start = lineStart(text, c)
        val end = lineEnd(text, c)
        val line = text.substring(start, end)

        val glyph = glyphIndex(line)
        if (glyph >= 0) {
            // Quitar: fuera el glifo y, si lo tiene, el espacio que lo sigue (ver prefixLength).
            val prefixLength = prefixLength(line, glyph)
            val stripped = line.removeRange(glyph, glyph + prefixLength)
            // Un cursor delante de la casilla (en la sangría o antes de la flecha) se queda donde estaba, como al
            // ponerla; solo lo que va detrás se desplaza con el texto.
            val caretInLine = if (c - start <= glyph) c - start else maxOf(c - start - prefixLength, glyph)
            return TextAndCaret(text.substring(0, start) + stripped + text.substring(end), start + caretInLine)
        }

        val bulletGlyph = BulletLines.glyphIndex(line)
        if (bulletGlyph >= 0) {
            // Añadir la casilla detrás de la flecha, sin quitarla: "→ x" pasa a "→ ☐ x".
            val afterArrow = bulletGlyph + BulletLines.prefixLength(line, bulletGlyph)
            val withBox = line.insertAt(afterArrow, PREFIX)
            val caretInLine = if (c - start >= afterArrow) c - start + PREFIX.length else c - start
            return TextAndCaret(text.substring(0, start) + withBox + text.substring(end), start + caretInLine)
        }

        // Reparar: una tarea antigua con el texto pegado al glifo ya no es tarea; se le mete el
        // espacio en vez de apilar otra casilla delante.
        val indent = ListPrefix.indentLength(line)
        if (startsWithBoxGlyph(line)) return ListPrefix.insertSpaceAfterGlyph(text, c, start + indent)

        // Poner: detrás de la sangría que ya tuviera la línea.
        val prefixed = line.insertAt(indent, PREFIX)
        val newCaret = if (c >= start + indent) c + PREFIX.length else c
        return TextAndCaret(text.substring(0, start) + prefixed + text.substring(end), newCaret)
    }

    /**
     * Enter al final de una tarea: continúa la lista con otra casilla (misma sangría) o, si la tarea estaba vacía, la termina.
     * `newLine` es parámetro: Android trabaja con LF, Windows con CRLF.
     */
    fun enterContinuation(text: String, caret: Int, newLine: String = DEFAULT_NEW_LINE): TextAndCaret? {
        val c = caret.coerceIn(0, text.length)
        val start = lineStart(text, c)
        val end = lineEnd(text, c)
        val line = text.substring(start, end)

        val glyph = glyphIndex(line)
        if (glyph < 0) return null

        // Solo continúa desde el final de la línea: pulsar Enter en mitad de una tarea la parte en
        // dos, que es lo que hace un editor normal y lo que el usuario espera.
        if (c != end) return null

        if (isEmptyTaskLine(line)) {
            // Solo la sangría: con "→ ☐ " vacío la lista termina quitando también la flecha.
            val cleared = line.substring(0, ListPrefix.indentLength(line)).trimEndDotNet()
            return TextAndCaret(text.substring(0, start) + cleared + text.substring(end), start + cleared.length)
        }

        val indent = line.substring(0, glyph)
        val inserted = newLine + indent + PREFIX
        return TextAndCaret(text.substring(0, c) + inserted + text.substring(c), c + inserted.length)
    }

    /** Tareas hechas y totales; las tareas se cuentan por línea, con o sin `\r` al final. */
    fun count(text: String): TaskCount {
        var done = 0
        var total = 0
        for (line in text.split('\n')) {
            val clean = line.trimEndCr()
            if (!isTaskLine(clean)) continue
            total++
            if (isChecked(clean)) done++
        }
        return TaskCount(done, total)
    }

    fun lineStart(text: String, index: Int): Int = LineText.start(text, index)

    private fun lineEnd(text: String, index: Int): Int = LineText.end(text, index)
}

/** `(int Done, int Total)` de `TaskLines.Count`. */
data class TaskCount(val done: Int, val total: Int)
