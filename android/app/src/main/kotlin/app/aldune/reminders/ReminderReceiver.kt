package app.aldune.reminders

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import app.aldune.AldunaApp
import app.aldune.AppContainer
import app.aldune.domain.SnoozeKind
import java.time.Instant
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch

/**
 * La alarma de un recordatorio y los botones de su notificación. Abrir la base (Keystore, Room) es E/S: se hace con `goAsync`, fuera del hilo
 * principal. Si la clave de las notas no se puede recuperar no se hace nada (la app lo explicará al abrirla).
 */
class ReminderReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val noteId = intent.data?.lastPathSegment ?: return
        val action = intent.action
        val dueAt = intent.getLongExtra(ReminderScheduler.EXTRA_DUE_AT, -1)
        withReminders(context) { reminders ->
            when (action) {
                ACTION_FIRE -> if (dueAt >= 0) reminders.fired(noteId, Instant.ofEpochMilli(dueAt)) else reminders.reconcile()
                ACTION_DONE -> reminders.dismiss(noteId)
                ACTION_SNOOZE_HOUR -> reminders.snooze(noteId, SnoozeKind.OneHour)
                ACTION_SNOOZE_MORNING -> reminders.snooze(noteId, SnoozeKind.TomorrowMorning)
            }
        }
    }

    companion object {
        const val ACTION_FIRE = "app.aldune.action.REMINDER_FIRE"
        const val ACTION_DONE = "app.aldune.action.REMINDER_DONE"
        const val ACTION_SNOOZE_HOUR = "app.aldune.action.REMINDER_SNOOZE_HOUR"
        const val ACTION_SNOOZE_MORNING = "app.aldune.action.REMINDER_SNOOZE_MORNING"

        /** Abre el contenedor del proceso y trabaja con sus recordatorios fuera del hilo principal; el receptor vive hasta que acaba. */
        fun BroadcastReceiver.withReminders(context: Context, block: (Reminders) -> Unit) {
            val pending = goAsync()
            val container = (context.applicationContext as AldunaApp).container
            CoroutineScope(Dispatchers.IO).launch {
                try {
                    (container.open() as? AppContainer.Opened.Ready)?.let { block(it.reminders) }
                } finally {
                    pending.finish()
                }
            }
        }
    }
}
