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
    data class Open(override val id: String, override val color: String, val title: String, val preview: String?) : WidgetStrip

    /** Nota protegida: sitio y color, y su nombre visible solo si el usuario se lo puso (es público por diseño). Nunca nada del contenido. */
    data class Locked(override val id: String, override val color: String, val title: String? = null) : WidgetStrip
}

/** Lo que pinta el widget: las tiras que caben y cuántas notas activas se quedaron fuera (la fila «Más notas: N»). */
data class WidgetContent(val strips: List<WidgetStrip>, val hiddenCount: Int)

/** [placeholder] es el título de una nota sin texto, en el idioma de la interfaz (este módulo no escribe textos de interfaz). */
data class WidgetOptions(val placeholder: String, val showPreview: Boolean = false, val limit: Int = DEFAULT_LIMIT) {
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
        val active = DockOrder.sort(notes.filter { it.state == NoteState.ACTIVE })
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
            NoteTitleHelper.getPreview(note.text).takeIf { it.isNotEmpty() }?.let { truncate(it, MAX_PREVIEW_LENGTH) }
        } else {
            null
        }
        return WidgetStrip.Open(note.id, note.color, title, preview)
    }

    /** Como mucho [max] unidades UTF-16 contando los puntos suspensivos, sin dejar a medias un par sustituto. */
    internal fun truncate(text: String, max: Int): String {
        if (text.length <= max) return text
        var end = max - ELLIPSIS.length
        if (end > 0 && text[end - 1].isHighSurrogate()) end--
        return text.substring(0, end) + ELLIPSIS
    }
}
