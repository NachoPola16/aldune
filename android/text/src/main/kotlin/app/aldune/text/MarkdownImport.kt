package app.aldune.text

/**
 * Markdown text to the text of a note (the way back of [MarkdownExport]; port of `MarkdownImport`/`MarkdownLink.ToNoteText` in Aldune.Core).
 * Tasks and bullets become the glyphs of Aldune, the `# ` of the first line goes away (it is the title), and line breaks become `\n`.
 */
object MarkdownImport {
    // Marker followed by a space (so "**bold**" and "---" do not count) and an optional "[ ]" box or a glyph box already in the text.
    private val listItem = Regex("""^(?<indent>[ \t]*)(?<marker>[-*+]) (?:\[(?<mark>[ xX])\](?: |$)|(?<glyph>[☐☑☒])(?: |$))?""")
    private val title = Regex("^# +")

    fun toNoteText(markdown: String): String {
        val lines = markdown.replace("\r\n", "\n").split('\n')
        val literal = literalLines(lines)
        val translated = lines.mapIndexed { i, line -> if (literal[i]) line else toNoteLine(line) }.joinToString("\n")
        return title.replaceFirst(translated, "")
    }

    // Fenced code (``` or ~~~) is text, not tasks; the fence closes with the same kind that opened it.
    private fun literalLines(lines: List<String>): BooleanArray {
        val literal = BooleanArray(lines.size)
        var fence: String? = null
        for ((i, line) in lines.withIndex()) {
            val trimmed = line.trimStart(' ', '\t')
            val opener = when {
                trimmed.startsWith("```") -> "```"
                trimmed.startsWith("~~~") -> "~~~"
                else -> null
            }
            if (fence == null) {
                if (opener != null) { fence = opener; literal[i] = true }
            } else {
                literal[i] = true
                if (opener == fence) fence = null
            }
        }
        return literal
    }

    private fun toNoteLine(line: String): String {
        val m = listItem.find(line) ?: return line
        val indent = m.groups["indent"]!!.value
        val rest = line.substring(m.value.length)
        m.groups["glyph"]?.let { return indent + BulletLines.PREFIX + it.value + " " + rest }
        val mark = m.groups["mark"] ?: return indent + BulletLines.PREFIX + rest
        return indent + (if (mark.value == " ") TaskLines.UNCHECKED else TaskLines.CHECKED) + " " + rest
    }
}
