package app.aldune.text

/**
 * Convierte el texto de una nota a Markdown (port de `MarkdownExport` de Core): título como encabezado, casillas como `- [ ]`/`- [x]`,
 * viñetas como `- `, el resto del cuerpo tal cual. Los saltos de línea son `\r\n`, como los del fichero que exporta Windows.
 * `placeholder` es el título de una nota vacía, en el idioma de la app.
 */
object MarkdownExport {
    private const val MAX_FILE_NAME_LENGTH = 80
    private const val NEW_LINE = "\r\n"

    // Los caracteres que Windows no admite en un nombre de fichero; así el nombre sirve también si el fichero acaba en el PC.
    private const val INVALID_FILE_NAME_CHARS = "<>:\"/\\|?*"

    fun toMarkdown(noteText: String, placeholder: String): String {
        val title = NoteTitleHelper.getTitle(noteText, placeholder)
        val body = NoteText.split(noteText).body

        val heading = "# $title"
        if (body.isEmpty()) return heading

        return heading + NEW_LINE + NEW_LINE + body.split('\n').joinToString(NEW_LINE, transform = ::convertLine)
    }

    private fun convertLine(rawLine: String): String {
        val line = rawLine.trimEnd('\r')

        val taskGlyph = TaskLines.glyphIndex(line)
        if (taskGlyph >= 0) {
            val prefixLength = TaskLines.prefixLength(line, taskGlyph)
            val indent = line.substring(0, ListPrefix.indentLength(line)) // sin la flecha, si la hay: la lista ya es «- »
            val content = line.substring(taskGlyph + prefixLength)
            val marker = if (TaskLines.isChecked(line)) "[x]" else "[ ]"
            return "$indent- $marker $content"
        }

        val bulletGlyph = BulletLines.glyphIndex(line)
        if (bulletGlyph >= 0) {
            val prefixLength = BulletLines.prefixLength(line, bulletGlyph)
            return line.substring(0, bulletGlyph) + "- " + line.substring(bulletGlyph + prefixLength)
        }

        return line
    }

    /** El título como nombre de fichero: sin caracteres inválidos, recortado y con `.md`. */
    fun suggestedFileName(noteText: String, placeholder: String): String =
        sanitize(NoteTitleHelper.getTitle(noteText, placeholder)) + ".md"

    private fun sanitize(title: String): String {
        val cleaned = title.map { if (it in INVALID_FILE_NAME_CHARS || it.code < 32) '_' else it }.joinToString("").trim()
        val result = if (cleaned.length > MAX_FILE_NAME_LENGTH) cleaned.substring(0, MAX_FILE_NAME_LENGTH).trimEnd() else cleaned
        return result.ifEmpty { "Nota" }
    }
}
