package app.aldune.text

/** Operaciones sobre la lista de tareas de una nota entera (completarlas, limpiarlas, recolocarlas). Port de `TaskLists`. */
object TaskLists {
    /** Posición de la casilla de la línea del cursor, o `null` si esa línea no es una tarea. */
    fun checkboxOnLine(text: String, caret: Int): Int? {
        val c = caret.coerceIn(0, text.length)
        val start = TaskLines.lineStart(text, c)
        val glyph = TaskLines.glyphIndex(TaskLines.lineContaining(text, c))
        return if (glyph >= 0) start + glyph else null
    }

    fun hasChecked(text: String): Boolean = TaskLines.count(text).done > 0

    fun isAllDone(text: String): Boolean {
        val (done, total) = TaskLines.count(text)
        return total > 0 && done == total
    }

    /**
     * Añade `item` como tarea nueva (`☐ item`) justo detrás de la última tarea de la nota, o al final si no hay ninguna. Es lo que hace
     * la lista rápida de la notificación: el artículo se une a la lista y no cae debajo de las notas sueltas que haya después.
     * Un artículo vacío no cambia nada; los espacios y saltos del artículo se reducen a un espacio. Usa el salto de línea de la nota
     * (CRLF si lo lleva, LF si no).
     */
    fun appendTask(text: String, item: String): String {
        val clean = item.trim().replace(Regex("\\s+"), " ")
        if (clean.isEmpty()) return text
        val task = TaskLines.PREFIX + clean
        val newLine = if (text.contains("\r\n")) "\r\n" else "\n"

        val lines = TextEdit.Lines.parse(text)
        val lastTask = (0 until lines.count).lastOrNull { TaskLines.isTaskLine(lines.contents[it]) }
        if (lastTask != null) {
            val end = lines.starts[lastTask] + lines.contents[lastTask].length
            return text.substring(0, end) + newLine + task + text.substring(end)
        }
        return when {
            text.isEmpty() -> task
            text.endsWith("\n") -> text + task
            else -> text + newLine + task
        }
    }

    /** Desmarca todas las tareas hechas, sin mover nada (el texto mide lo mismo). */
    fun uncheckAll(text: String): TextEdit {
        val lines = TextEdit.Lines.parse(text)
        val chars = text.toCharArray()
        for (i in 0 until lines.count) {
            if (!TaskLines.isChecked(lines.contents[i])) continue
            chars[lines.starts[i] + TaskLines.glyphIndex(lines.contents[i])] = TaskLines.UNCHECKED
        }
        return TextEdit.inPlace(text, String(chars))
    }

    /** Borra las líneas de tareas hechas. */
    fun removeChecked(text: String): TextEdit {
        val lines = TextEdit.Lines.parse(text)
        val removed = (0 until lines.count).filter { TaskLines.isChecked(lines.contents[it]) }.toSet()
        return TextEdit.removeLines(text, removed)
    }

    /**
     * Tras marcar o desmarcar la tarea de `glyphIndex`, la recoloca dentro de su lista (mismo nivel de sangría): una hecha va al
     * final, una desmarcada va delante de la primera hecha. No mueve nada si alguna tarea de la lista tiene subtareas.
     */
    fun settleToggled(text: String, glyphIndex: Int): TextEdit {
        val lines = TextEdit.Lines.parse(text)
        val line = lineOf(lines, glyphIndex)
        val content = lines.contents[line]
        if (!TaskLines.isTaskLine(content)) return TextEdit.unchanged(text)

        val indent = indent(content)
        fun sameList(i: Int) = TaskLines.isTaskLine(lines.contents[i]) && indent(lines.contents[i]) == indent

        var first = line
        var last = line
        while (first > 0 && sameList(first - 1)) first--
        while (last < lines.count - 1 && sameList(last + 1)) last++

        for (i in first..last) {
            if (hasChildren(lines, i)) return TextEdit.unchanged(text)
        }

        val target: Int
        if (TaskLines.isChecked(content)) {
            target = last
        } else {
            val firstChecked = (first until line).firstOrNull { TaskLines.isChecked(lines.contents[it]) } ?: -1
            if (firstChecked < 0) return TextEdit.unchanged(text)
            target = firstChecked
        }
        if (target == line) return TextEdit.unchanged(text)

        val order = (0 until lines.count).filter { it != line }.toMutableList()
        order.add(target, line)
        return TextEdit.reorder(text, order)
    }

    // Líneas de Markdown ("- [ ] x", "[x] y") que se convierten a casillas de Aldune. Sin UNIX_LINES, `^` de Java también
    // reconocería \r y otros separadores; .NET solo mira \n.
    private val markdownTask = Regex("""^([ \t]*)(?:[-*+][ \t]+)?\[([ xX])\][ \t]?""", setOf(RegexOption.MULTILINE, RegexOption.UNIX_LINES))

    fun convertMarkdownTasks(text: String): String =
        markdownTask.replace(text) { match ->
            match.groupValues[1] + (if (match.groupValues[2] == " ") TaskLines.UNCHECKED else TaskLines.CHECKED) + " "
        }

    /** Hasta `max` tareas pendientes con texto, en orden, sin su casilla. */
    fun pendingTasks(text: String, max: Int): List<String> {
        val pending = mutableListOf<String>()
        for (raw in text.split('\n')) {
            if (pending.size >= max) break
            val line = raw.trimEndCr()
            val glyph = TaskLines.glyphIndex(line)
            if (glyph < 0 || TaskLines.isChecked(line)) continue
            val content = line.substring(glyph + TaskLines.prefixLength(line, glyph)).trimDotNet()
            if (content.isNotEmpty()) pending.add(content)
        }
        return pending
    }

    private fun lineOf(lines: TextEdit.Lines, index: Int): Int {
        for (i in lines.count - 1 downTo 0) {
            if (index >= lines.starts[i]) return i
        }
        return 0
    }

    private fun indent(line: String): String = line.substring(0, ListPrefix.indentLength(line))

    private fun hasChildren(lines: TextEdit.Lines, i: Int): Boolean =
        i + 1 < lines.count
            && lines.contents[i + 1].trimDotNet().isNotEmpty()
            && indent(lines.contents[i + 1]).length > indent(lines.contents[i]).length
}
