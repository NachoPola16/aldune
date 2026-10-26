package app.aldune.text

/**
 * Lo que hace falta para arrastrar una tarea con el dedo y que no es dibujo: contar líneas en el texto y pasar de la línea que se ve a la
 * del texto real cuando hay tareas hechas ocultas (el campo dibuja el texto sin ellas; ver [HiddenTasks]). El movimiento en sí es
 * [LineMovement.moveTo].
 */
object TaskDrag {
    /** Número de líneas del texto (un texto vacío tiene una). */
    fun lineCount(text: String): Int = text.count { it == '\n' } + 1

    /** Índice (desde 0) de la línea que contiene [offset]. */
    fun lineOf(text: String, offset: Int): Int {
        val end = offset.coerceIn(0, text.length)
        var count = 0
        for (i in 0 until end) if (text[i] == '\n') count++
        return count
    }

    /** Posición del primer carácter de la línea [line]; si no existe, la de la última. */
    fun lineStart(text: String, line: Int): Int {
        var current = 0
        var start = 0
        while (current < line) {
            val lf = text.indexOf('\n', start)
            if (lf < 0) break
            start = lf + 1
            current++
        }
        return start
    }

    /**
     * La línea del texto REAL ([raw]) que corresponde a la línea [shownLine] del texto que se ve ([shown], con [spans] ya quitados).
     * Una línea visible fuera de rango cuenta como la última.
     */
    fun rawLine(shown: String, spans: List<HiddenTasks.Span>, raw: String, shownLine: Int): Int {
        val displayedStart = lineStart(shown, shownLine.coerceIn(0, lineCount(shown) - 1))
        return lineOf(raw, HiddenTasks.toRaw(displayedStart, spans))
    }
}
