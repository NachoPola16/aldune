package app.aldune.reminders

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import androidx.core.app.NotificationCompat
import app.aldune.R
import app.aldune.capture.CaptureIntents
import app.aldune.capture.CaptureNotification
import app.aldune.data.Languages
import app.aldune.data.SettingsStore
import app.aldune.domain.Note
import app.aldune.domain.ReminderNotificationContent

/**
 * La notificación de un recordatorio: título, hasta tres tareas pendientes y los botones Hecho, +1 h y Mañana 9:00; tocarla abre la nota (Android
 * enseña tres botones como mucho). El contenido sale de [ReminderNotificationContent]: una protegida es `Locked` y no tiene texto que enseñar.
 * Cada nota tiene la suya (etiqueta = id de la nota): varios recordatorios vencidos se avisan por separado.
 */
class ReminderNotifier(context: Context) {
    private val context = context.applicationContext
    private val manager = this.context.getSystemService(NotificationManager::class.java)

    fun show(note: Note) {
        // Sin permiso, `notify` no hace nada: el recordatorio se da por avisado igualmente (como Windows con las notificaciones apagadas).
        if (!CaptureNotification.canPost(context)) return
        // Los textos en el idioma elegido en Ajustes, que puede no ser el del sistema (aquí no hay actividad que lo aplique).
        val res = Languages.wrap(context, SettingsStore(context).load().language)
        manager.createNotificationChannel(
            NotificationChannel(CHANNEL_ID, res.getString(R.string.reminders_channel), NotificationManager.IMPORTANCE_HIGH).apply {
                description = res.getString(R.string.reminders_channel_description)
            },
        )
        val builder = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_reminder)
            .setCategory(NotificationCompat.CATEGORY_REMINDER)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setContentIntent(openIntent(note.id))
            .setAutoCancel(true)
            .addAction(0, res.getString(R.string.reminder_done), actionIntent(note.id, ReminderReceiver.ACTION_DONE))
            .addAction(0, res.getString(R.string.reminder_snooze_hour), actionIntent(note.id, ReminderReceiver.ACTION_SNOOZE_HOUR))
            .addAction(0, res.getString(R.string.reminder_snooze_morning), actionIntent(note.id, ReminderReceiver.ACTION_SNOOZE_MORNING))
        when (val content = ReminderNotificationContent.of(note, res.getString(R.string.new_note_title))) {
            is ReminderNotificationContent.Locked ->
                builder.setContentTitle(res.getString(R.string.protected_note_title)).setVisibility(NotificationCompat.VISIBILITY_PUBLIC)
            is ReminderNotificationContent.Open -> {
                builder.setContentTitle(content.title).setVisibility(NotificationCompat.VISIBILITY_PRIVATE)
                if (content.pendingTasks.isNotEmpty() && SettingsStore(context).load().reminderIncludesTasks) {
                    builder.setContentText(content.pendingTasks.first())
                    builder.setStyle(NotificationCompat.InboxStyle().also { style -> content.pendingTasks.forEach { style.addLine("☐ $it") } })
                }
            }
        }
        manager.notify(note.id, NOTIFICATION_ID, builder.build())
    }

    fun cancel(noteId: String) = manager.cancel(noteId, NOTIFICATION_ID)

    // Abrir es una actividad y no un receptor: desde Android 12 un receptor lanzado por una notificación ya no puede abrir pantallas. Va por el mismo
    // camino que una tira del widget (una protegida pide su contraseña). La URI hace distinto el PendingIntent de cada nota: los extras no cuentan.
    private fun openIntent(noteId: String): PendingIntent = PendingIntent.getActivity(
        context, 0, CaptureIntents.openNote(context, noteId).setData(ReminderScheduler.uriOf(noteId)),
        PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
    )

    private fun actionIntent(noteId: String, action: String): PendingIntent = PendingIntent.getBroadcast(
        context, 0, Intent(context, ReminderReceiver::class.java).setAction(action).setData(ReminderScheduler.uriOf(noteId)),
        PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
    )

    private companion object {
        const val CHANNEL_ID = "reminders"
        const val NOTIFICATION_ID = 2
    }
}
