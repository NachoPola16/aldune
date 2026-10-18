package app.aldune.reminders

import android.app.AlarmManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.os.Build
import app.aldune.data.SettingsStore
import app.aldune.domain.Reminder

/**
 * Las alarmas de los recordatorios, una por nota. Por defecto no exacta (`setAndAllowWhileIdle`: con el móvil en reposo puede llegar minutos
 * tarde); exacta solo si el ajuste está activo y el sistema concede `SCHEDULE_EXACT_ALARM` (spec madre §2.9). `RTC_WAKEUP` porque la hora es de
 * pared: un cambio de zona no la mueve (el instante es el mismo).
 */
class ReminderScheduler(context: Context) {
    private val context = context.applicationContext
    private val alarms = this.context.getSystemService(AlarmManager::class.java)

    fun schedule(reminder: Reminder) {
        val at = reminder.dueAt.toEpochMilli()
        val operation = PendingIntent.getBroadcast(
            context, 0, fireIntent(reminder.noteId).putExtra(EXTRA_DUE_AT, at),
            PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        if (SettingsStore(context).load().exactReminders && canScheduleExact(context)) {
            try {
                alarms.setExactAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, operation)
                return
            } catch (e: SecurityException) {
                // El permiso se retiró entre la comprobación y la llamada: vale la alarma no exacta.
            }
        }
        alarms.setAndAllowWhileIdle(AlarmManager.RTC_WAKEUP, at, operation)
    }

    fun cancel(noteId: String) {
        PendingIntent.getBroadcast(context, 0, fireIntent(noteId), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_NO_CREATE)?.let {
            alarms.cancel(it)
            it.cancel()
        }
    }

    // Un PendingIntent por nota: se distinguen por la URI (los extras no cuentan para el sistema), y la misma nota reemplaza su alarma.
    private fun fireIntent(noteId: String) =
        Intent(context, ReminderReceiver::class.java).setAction(ReminderReceiver.ACTION_FIRE).setData(uriOf(noteId))

    companion object {
        const val EXTRA_DUE_AT = "due_at"

        fun uriOf(noteId: String): Uri = Uri.parse("aldune://reminder/$noteId")

        /** Antes de Android 12 las alarmas exactas no piden permiso. */
        fun canScheduleExact(context: Context): Boolean =
            Build.VERSION.SDK_INT < Build.VERSION_CODES.S || context.getSystemService(AlarmManager::class.java).canScheduleExactAlarms()
    }
}
