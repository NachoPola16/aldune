package app.aldune.domain

import app.aldune.text.NoteTitleHelper
import app.aldune.text.ProtectedTitleRules
import app.aldune.text.TaskLines

/**
 * Lo que enseña una fila de la lista, ya calculado: título, vista previa de una línea y contador de tareas. Vive aquí (y no en la interfaz)
 * para probarlo en la JVM con las mismas reglas que Windows (`NoteTitleHelper`, `TaskLines.count`).
 */
data class NoteListItem(
    val id: String,
    val title: String,
    val preview: String,
    val color: String,
    val tasksDone: Int,
    val tasksTotal: Int,
    val tags: List<String>,
    val state: NoteState,
    /** Una protegida no enseña su texto (no se conoce sin contraseña): su nombre visible o el título de relleno, sin vista previa ni tareas. */
    val isProtected: Boolean = false,
    /** Para la señal de sincronización de la fila (se compara con la última versión acordada con el almacén). */
    val updatedAt: java.time.Instant = java.time.Instant.EPOCH,
) {
    val hasTasks: Boolean get() = tasksTotal > 0

    companion object {
        /** [placeholder] es el título de una nota sin texto y [protectedTitle] el de una protegida, en el idioma de la interfaz. */
        fun from(note: Note, placeholder: String, protectedTitle: String = placeholder): NoteListItem {
            if (note.isProtected) {
                return NoteListItem(
                    id = note.id, title = ProtectedTitleRules.visible(true, note.protectedTitle) ?: protectedTitle, preview = "", color = note.color, tasksDone = 0, tasksTotal = 0,
                    tags = note.tags, state = note.state, isProtected = true, updatedAt = note.updatedAt,
                )
            }
            val count = TaskLines.count(note.text)
            return NoteListItem(
                id = note.id, title = NoteTitleHelper.getTitle(note.text, placeholder), preview = NoteTitleHelper.getPreview(note.text),
                color = note.color, tasksDone = count.done, tasksTotal = count.total, tags = note.tags, state = note.state, updatedAt = note.updatedAt,
            )
        }
    }
}
