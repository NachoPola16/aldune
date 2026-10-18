package app.aldune.domain

import app.aldune.text.TaskLists

/** Resultado de componer una nota a partir de lo que otra app comparte. */
sealed interface SharedNote {
    /** El texto de la nota nueva: con LF, no vacío y listo para `NoteService.create`. */
    data class Text(val value: String) : SharedNote

    /** No hay nada que guardar (todo en blanco o ausente). */
    data object Empty : SharedNote

    /** Más de [SharedNoteText.MAX_LENGTH] caracteres: no se crea la nota ni se corta el texto. */
    data object TooLong : SharedNote
}

/**
 * El texto de una nota nueva a partir de `EXTRA_TEXT` y `EXTRA_SUBJECT` de un `ACTION_SEND`. Con asunto, el asunto es el título
 * (primera línea) salvo que el texto ya empiece por él, que es lo que mandan Chrome y otras apps con «Título\nURL».
 */
object SharedNoteText {
    /**
     * El servidor rechaza objetos de más de 5 MB; un millón de caracteres cifrados y en JSON queda por debajo con holgura incluso en el
     * peor caso de UTF-8.
     */
    const val MAX_LENGTH = 1_000_000

    fun compose(text: String?, subject: String?): SharedNote {
        val body = cleanBody(text.orEmpty())
        val title = cleanSubject(subject.orEmpty())

        val composed = when {
            title.isEmpty() && body.isEmpty() -> return SharedNote.Empty
            title.isEmpty() -> body
            body.isEmpty() -> title
            // Comparación ordinal sin distinguir mayúsculas; el cuerpo ya viene con LF, así que la primera línea es hasta el primer '\n'.
            body.substringBefore('\n').trimDotNet().startsWith(title, ignoreCase = true) -> body
            else -> title + "\n" + body
        }

        // Las casillas de Markdown nunca alargan el texto, pero el tope se mide sobre lo que de verdad llega a la nota.
        val converted = TaskLists.convertMarkdownTasks(composed)
        return if (converted.length > MAX_LENGTH) SharedNote.TooLong else SharedNote.Text(converted)
    }

    /** LF; sin líneas en blanco al principio ni al final y sin espacios al final. La sangría de la primera línea se conserva. */
    private fun cleanBody(raw: String): String {
        val text = raw.replace("\r\n", "\n").replace('\r', '\n')
        val firstContent = text.indexOfFirst { !it.isDotNetWhiteSpace() }
        if (firstContent < 0) return ""
        val lineStart = text.lastIndexOf('\n', firstContent) + 1
        return text.substring(lineStart).trimEndDotNet()
    }

    /** Un asunto no puede tener dos líneas: todo espacio en blanco, saltos incluidos, es un solo espacio. */
    private fun cleanSubject(raw: String): String {
        val builder = StringBuilder(raw.length)
        var pendingSpace = false
        for (character in raw) {
            if (character.isDotNetWhiteSpace()) {
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
}
