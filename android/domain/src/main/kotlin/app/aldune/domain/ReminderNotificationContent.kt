package app.aldune.domain

import app.aldune.text.NoteTitleHelper
import app.aldune.text.ProtectedTitleRules
import app.aldune.text.TaskLists

/**
 * Lo que enseña la notificación de un recordatorio. Sellada a propósito, como [WidgetStrip]: [Locked] no tiene ningún campo de texto,
 * así que «una protegida nunca lleva su texto» lo garantiza el tipo y no un `if` que alguien pueda olvidar.
 */
sealed interface ReminderNotificationContent {
    val noteId: String

    /** [title] y [pendingTasks] (las `☐` sin marcar, sin su casilla, como mucho [MAX_TASKS]). */
    data class Open(override val noteId: String, val title: String, val pendingTasks: List<String>) : ReminderNotificationContent

    /** Nota protegida: la interfaz pone su nombre visible (si lo tiene; si no, «Nota protegida») y ninguna tarea. */
    data class Locked(override val noteId: String, val title: String? = null) : ReminderNotificationContent

    companion object {
        const val MAX_TASKS = 3
        const val MAX_TASK_LENGTH = 120
        const val MAX_TITLE_LENGTH = 120

        /** [placeholder] es el título de una nota sin texto, en el idioma de la interfaz. */
        fun of(note: Note, placeholder: String): ReminderNotificationContent {
            // Antes de mirar el texto: de una protegida sólo se usan el id y el nombre visible, aunque tenga texto en memoria (nota desbloqueada).
            if (note.isProtected) return Locked(note.id, ProtectedTitleRules.visible(true, note.protectedTitle)?.let { WidgetContentBuilder.truncate(it, MAX_TITLE_LENGTH) })

            val title = WidgetContentBuilder.truncate(NoteTitleHelper.getTitle(note.text, placeholder), MAX_TITLE_LENGTH)
            val tasks = TaskLists.pendingTasks(note.text, MAX_TASKS).map { WidgetContentBuilder.truncate(it, MAX_TASK_LENGTH) }
            return Open(note.id, title, tasks)
        }
    }
}
