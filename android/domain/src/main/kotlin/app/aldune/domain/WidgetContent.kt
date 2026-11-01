package app.aldune.domain

import app.aldune.text.NoteTitleHelper
import app.aldune.text.ProtectedTitleRules

/**
 * Una tira del widget. Sellada a propósito: [Locked] no tiene ningún campo de texto, así que "una protegida nunca enseña nada
 * suyo" lo garantiza el tipo y no un `if` que alguien pueda olvidar.
 */
sealed interface WidgetStrip {
    val id: String

    /** El color de la cara tal cual; el borde y el contraste los deriva la app a partir de él. */
    val color: String

    /** [preview] es `null` si el ajuste está apagado o la nota no tiene más líneas que enseñar. */
    data class Open(
        override val id: String, override val color: String, val title: String, val preview: String?,
        /** Tareas hechas y total (0/0 si la nota no tiene casillas): el widget enseña «2/5» como la lista. */
        val tasksDone: Int = 0, val tasksTotal: Int = 0,
        /** Fijada arriba: el widget lo marca igual que la lista. */
        val pinned: Boolean = false,
        /** Las primeras tareas pendientes, para marcarlas desde el widget (vacío si el ajuste está apagado o no hay). */
        val tasks: List<WidgetTask> = emptyList(),
    ) : WidgetStrip

    /** Nota protegida: sitio y color, y su nombre visible solo si el usuario se lo puso (es público por diseño). Nunca nada del contenido. */
    data class Locked(override val id: String, override val color: String, val title: String? = null) : WidgetStrip
}

/**
 * Una tarea pendiente que el widget deja marcar. [index] es la posición de su casilla en el texto de la nota y [line] la línea entera tal
 * como se vio: al marcarla se comprueba que sigue siendo esa (la nota pudo cambiar desde que se pintó el widget; ver `NoteService.toggleTask`).
 */
data class WidgetTask(val index: Int, val text: String, val line: String)

/** Lo que pinta el widget: las tiras que caben y cuántas notas activas se quedaron fuera (la fila «Más notas: N»). */
data class WidgetContent(val strips: List<WidgetStrip>, val hiddenCount: Int)

/** [placeholder] es el título de una nota sin texto, en el idioma de la interfaz (este módulo no escribe textos de interfaz). */
data class WidgetOptions(
    val placeholder: String, val showPreview: Boolean = false, val limit: Int = DEFAULT_LIMIT,
    /** Cuántas tareas pendientes lleva cada tira para marcarlas (0 = ninguna). */
    val tasksPerNote: Int = 0,
    /** Solo las notas con esta etiqueta (sin distinguir mayúsculas); `null` = todas. */
    val tag: String? = null,
) {
    companion object {
        /** Tope de seguridad: un `RemoteViews` enorme lo rechaza el lanzador. */
        const val DEFAULT_LIMIT = 50
    }
}

object WidgetContentBuilder {
    const val MAX_TITLE_LENGTH = 120
    const val MAX_PREVIEW_LENGTH = 200

    private const val ELLIPSIS = "…"

    fun build(notes: Collection<Note>, options: WidgetOptions): WidgetContent {
        val active = DockOrder.sort(notes.filter { it.state == NoteState.ACTIVE && (options.tag == null || it.tags.any { tag -> tag.equals(options.tag, ignoreCase = true) }) })
        val shown = active.take(options.limit.coerceAtLeast(0))
        return WidgetContent(shown.map { strip(it, options) }, active.size - shown.size)
    }

    private fun strip(note: Note, options: WidgetOptions): WidgetStrip {
        // Antes de mirar nada más: de una protegida solo se usan el id, el color y el nombre visible, aunque tenga texto en memoria (nota desbloqueada).
        if (note.isProtected) {
            return WidgetStrip.Locked(note.id, note.color, ProtectedTitleRules.visible(true, note.protectedTitle)?.let { truncate(it, MAX_TITLE_LENGTH) })
        }

        val title = truncate(NoteTitleHelper.getTitle(note.text, options.placeholder), MAX_TITLE_LENGTH)
        val preview = if (options.showPreview) {
            NoteTitleHelper.getPreview(previewSource(note.text, options)).takeIf { it.isNotEmpty() }?.let { truncate(it, MAX_PREVIEW_LENGTH) }
        } else {
            null
        }
        val tasks = app.aldune.text.TaskLines.count(note.text)
        return WidgetStrip.Open(note.id, note.color, title, preview, tasks.done, tasks.total, note.pinnedToTop, pendingTasks(note.text, options.tasksPerNote))
    }

    /**
     * El texto del que sale la vista previa. Si la tira ya lleva casillas para marcar (`tasksPerNote` > 0), las líneas de tarea se quitan: la
     * previa las repetía justo encima de las mismas casillas (y el contador «0/4» ya dice cuántas hay). Sin casillas en la tira, el texto entero.
     */
    private fun previewSource(text: String, options: WidgetOptions): String {
        if (options.tasksPerNote <= 0) return text
        val lines = text.split('\n')
        return lines.filterIndexed { i, line -> i == 0 || app.aldune.text.TaskLines.glyphIndex(line) < 0 }.joinToString("\n")
    }

    /** Las primeras [max] tareas sin marcar, con la posición de su casilla en [text] (las líneas se separan con `\n`: el texto guardado va normalizado). */
    private fun pendingTasks(text: String, max: Int): List<WidgetTask> {
        if (max <= 0) return emptyList()
        val found = ArrayList<WidgetTask>()
        var start = 0
        for (line in text.split('\n')) {
            val glyph = app.aldune.text.TaskLines.glyphIndex(line)
            if (glyph >= 0 && !app.aldune.text.TaskLines.isChecked(line)) {
                val label = truncate(line.substring(app.aldune.text.TaskLines.prefixLength(line, glyph)).trim(), MAX_TITLE_LENGTH)
                found.add(WidgetTask(start + glyph, label, line))
                if (found.size == max) break
            }
            start += line.length + 1
        }
        return found
    }

    /** Como mucho [max] unidades UTF-16 contando los puntos suspensivos, sin dejar a medias un par sustituto. */
    internal fun truncate(text: String, max: Int): String {
        if (text.length <= max) return text
        var end = max - ELLIPSIS.length
        if (end > 0 && text[end - 1].isHighSurrogate()) end--
        return text.substring(0, end) + ELLIPSIS
    }
}
