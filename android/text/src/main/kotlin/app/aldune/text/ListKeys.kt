package app.aldune.text

/** Texto con una selección `[start, end)`; colapsada (`start == end`) es un cursor. */
data class TextSelection(val text: String, val start: Int, val end: Int)

/**
 * Enter y Retroceso del teclado virtual. En Android no llegan como eventos de tecla fiables (Gboard manda texto), así que se deducen
 * comparando el valor anterior con el nuevo de `onValueChange`. Función pura para probarla en la JVM.
 */
object ListKeys {
    private const val NEW_LINE = "\n"

    fun resolve(old: TextAndCaret, new: TextAndCaret): TextAndCaret? = resolve(TextSelection(old.text, old.caret, old.caret), new)

    /**
     * Si el cambio `old → new` es un Enter o un Retroceso que las listas tratan de forma especial, devuelve el valor que debe quedar
     * (texto y cursor); `null` si hay que aceptar `new` tal cual (teclear, pegar, Enter en mitad de línea, Retroceso normal…).
     */
    fun resolve(old: TextSelection, new: TextAndCaret): TextAndCaret? {
        if (old.start != old.end) return null // con selección lo resuelve el campo
        val c = old.start
        if (c !in 0..old.text.length) return null

        // Enter: se insertó exactamente "\n" en el cursor.
        if (new.caret == c + 1 && new.text == old.text.substring(0, c) + NEW_LINE + old.text.substring(c)) {
            return TaskLines.enterContinuation(old.text, c, NEW_LINE) ?: BulletLines.enterContinuation(old.text, c, NEW_LINE)
        }

        // Retroceso: se borró exactamente el carácter anterior al cursor.
        if (c > 0 && new.caret == c - 1 && new.text == old.text.removeRange(c - 1, c)) {
            return ListPrefix.removeOnBackspace(old.text, c)
        }

        return null
    }
}
