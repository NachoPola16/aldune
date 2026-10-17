package app.aldune.text

/** Título y vista previa de una nota a partir de su texto. Port de `NoteTitleHelper`. */
object NoteTitleHelper {
    /** Texto de relleno de Core (en la app lo fija el idioma; aquí es un parámetro y no estado global). */
    const val DEFAULT_PLACEHOLDER = "New note"

    // "# Tema 3" en la pestaña es "Tema 3": los # son marcas de Markdown, no parte del nombre. Solo de 1 a 6 almohadillas
    // seguidas de espacio, como en Markdown: "#hashtag" no. UNIX_LINES para que `$` signifique lo mismo que en .NET.
    private val headingMarks = Regex("""^#{1,6}(?:[ \t]+|$)""", RegexOption.UNIX_LINES)

    fun getTitle(text: String, placeholder: String = DEFAULT_PLACEHOLDER): String {
        var firstLine = text.substringBefore('\n').trimEndCr().trimDotNet()
        firstLine = headingMarks.replaceFirst(firstLine, "").trimDotNet()
        return if (firstLine.isEmpty()) placeholder else firstLine
    }

    /** Las líneas siguientes a la primera, en una sola línea: espacios colapsados y sin casillas. */
    fun getPreview(text: String): String {
        val firstBreak = text.indexOf('\n')
        if (firstBreak < 0) return ""

        val rest = text.substring(firstBreak + 1)

        val builder = StringBuilder(rest.length)
        var pendingSpace = false
        for (character in rest) {
            // Las casillas se caen de la vista previa: repetir "☐" delante de cada tarea gasta el hueco en decir algo que
            // el contador de getTabPreview ya dice mejor. (La ☑ alternativa sí se queda: Core no la quita.)
            if (character == TaskLines.UNCHECKED || character == TaskLines.CHECKED) continue

            if (character.isDotNetWhiteSpace()) {
                // Solo se materializa si después viene texto de verdad, así no quedan espacios sueltos al principio ni al final.
                pendingSpace = builder.isNotEmpty()
                continue
            }

            if (pendingSpace) {
                builder.append(' ')
                pendingSpace = false
            }
            builder.append(character)
        }

        return builder.toString()
    }

    /** Vista previa de la pestaña: «hechas/total · vista previa» si la nota tiene tareas. */
    fun getTabPreview(text: String): String {
        val preview = getPreview(text)
        val (done, total) = TaskLines.count(text)

        if (total == 0) return preview
        return if (preview.isEmpty()) "$done/$total" else "$done/$total · $preview"
    }
}
