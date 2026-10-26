package app.aldune.capture

import android.app.PendingIntent
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import androidx.core.app.RemoteInput
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.R
import app.aldune.data.SettingsStore
import app.aldune.domain.NoteResult
import app.aldune.syncclient.SyncEvent
import app.aldune.widget.WidgetUpdater
import kotlin.concurrent.thread

/** La nota elegida como lista rápida (Ajustes → Captura rápida): su id y el título con el que se enseña en la notificación. */
data class QuickList(val id: String, val title: String)

/**
 * Añadir un artículo a la lista rápida desde la notificación de captura, sin abrir la app: la acción lleva un campo de respuesta
 * (`RemoteInput`) y este receptor añade `☐ artículo` a la nota con el servicio de notas de siempre.
 */
object QuickAdd {
    const val ACTION = "app.aldune.action.QUICK_ADD"
    const val EXTRA_NOTE_ID = "note_id"
    const val KEY_TEXT = "quick_add_text"

    /** La acción de la notificación. El `PendingIntent` es mutable: el sistema le añade lo que se escribe (obligatorio con `RemoteInput`). */
    fun action(context: Context, list: QuickList): NotificationCompat.Action {
        val intent = Intent(context, QuickAddReceiver::class.java).setAction(ACTION).putExtra(EXTRA_NOTE_ID, list.id)
        val pending = PendingIntent.getBroadcast(context, 1, intent, PendingIntent.FLAG_MUTABLE or PendingIntent.FLAG_UPDATE_CURRENT)
        val input = RemoteInput.Builder(KEY_TEXT).setLabel(context.getString(R.string.quick_list_input, list.title)).build()
        return NotificationCompat.Action.Builder(R.drawable.ic_new_note, context.getString(R.string.quick_list_action, list.title), pending)
            .addRemoteInput(input)
            .setAllowGeneratedReplies(false)
            .build()
    }
}

class QuickAddReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action != QuickAdd.ACTION) return
        val noteId = intent.getStringExtra(QuickAdd.EXTRA_NOTE_ID) ?: return
        val item = RemoteInput.getResultsFromIntent(intent)?.getCharSequence(QuickAdd.KEY_TEXT)?.toString().orEmpty()
        // Abrir la base hace E/S: fuera del hilo principal, y el receptor se mantiene vivo hasta que acaba.
        val pending = goAsync()
        thread(name = "quick-add") {
            try {
                handle(context.applicationContext, noteId, item)
            } finally {
                pending.finish()
            }
        }
    }

    private fun handle(context: Context, noteId: String, item: String) {
        val store = SettingsStore(context)
        val settings = store.load()
        val ready = (context as AldunaApp).container.open() as? AppContainer.Opened.Ready
        val result = ready?.notes?.appendTask(noteId, item)
        if (ready == null || result is NoteResult.Err) {
            // La nota ya no sirve (borrada, archivada, protegida): se quita la lista rápida y la notificación lo dice.
            store.save(settings.copy(quickListId = null, quickListTitle = ""))
            CaptureNotification.sync(context, settings.captureNotification, quickList = null, feedback = context.getString(R.string.quick_list_gone))
            return
        }
        if (item.isNotBlank()) {
            ready.sync.request(SyncEvent.LocalChange)
            // Igual que tras una sync que cambió notas: el editor abierto recarga su texto y la lista y el widget se repintan.
            ready.sync.onNotesChanged?.invoke()
            WidgetUpdater.request(context)
        }
        CaptureNotification.sync(
            context, settings.captureNotification, settings.quickList,
            feedback = if (item.isBlank()) null else context.getString(R.string.quick_list_added, item.trim().let { if (it.length > 60) it.take(60) + "…" else it }),
        )
    }
}
