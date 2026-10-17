package app.aldune.text

/**
 * Dónde empieza y acaba la línea que contiene un índice. Un solo criterio para todos los módulos de lista: el principio es
 * lo que sigue al último `\n` (el `\r` de un CRLF queda al final de su línea) y el final es el primer `\r` o `\n`.
 */
internal object LineText {
    fun start(text: String, index: Int): Int {
        for (i in minOf(index, text.length) - 1 downTo 0) {
            if (text[i] == '\n') return i + 1
        }
        return 0
    }

    fun end(text: String, index: Int): Int {
        for (i in maxOf(index, 0) until text.length) {
            if (text[i] == '\r' || text[i] == '\n') return i
        }
        return text.length
    }
}

/** `(string Text, int Caret)` de Core. */
data class TextAndCaret(val text: String, val caret: Int)
