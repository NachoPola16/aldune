package app.aldune.widget

import android.content.Context
import androidx.glance.GlanceId
import androidx.glance.action.ActionParameters
import androidx.glance.appwidget.action.ActionCallback
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.domain.NoteResult
import app.aldune.syncclient.SyncEvent
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Ticks a task from the dock widget. The widget only knows the note id, the position of the box and the line it saw; `toggleTask`
 * refuses a line that no longer matches (the note changed meanwhile), so a tap can never tick another task. Either way the widget is
 * redrawn so it shows what is really in the note.
 */
class ToggleTaskAction : ActionCallback {
    override suspend fun onAction(context: Context, glanceId: GlanceId, parameters: ActionParameters) {
        val noteId = parameters[NOTE_ID] ?: return
        val index = parameters[BOX_INDEX] ?: return
        val line = parameters[LINE] ?: return
        val app = context.applicationContext
        withContext(Dispatchers.IO) {
            val ready = (app as AldunaApp).container.open() as? AppContainer.Opened.Ready ?: return@withContext
            if (ready.notes.toggleTask(noteId, index, line) is NoteResult.Ok) {
                // As after a sync that changed notes: an open editor reloads its text, the list refreshes and the change is pushed.
                ready.sync.request(SyncEvent.LocalChange)
                ready.sync.onNotesChanged?.invoke()
            }
        }
        WidgetUpdater.request(app)
    }

    companion object {
        val NOTE_ID = ActionParameters.Key<String>("noteId")
        val BOX_INDEX = ActionParameters.Key<Int>("boxIndex")
        val LINE = ActionParameters.Key<String>("line")
    }
}
