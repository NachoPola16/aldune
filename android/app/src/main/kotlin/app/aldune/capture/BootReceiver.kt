package app.aldune.capture

import android.app.AlarmManager
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import app.aldune.data.SettingsStore
import app.aldune.reminders.ReminderReceiver.Companion.withReminders

/**
 * Avisos del sistema. Tras arrancar el móvil o actualizar la app las notificaciones y las alarmas se pierden: se repone la de captura si el ajuste
 * está activo y se reconcilian los recordatorios (avisa de los que vencieron con el móvil apagado y programa los demás). Un cambio de hora, de
 * zona o del permiso de alarma exacta también reconcilia: un recordatorio puede haber quedado en el pasado, o pasar de no exacto a exacto.
 */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        when (intent.action) {
            Intent.ACTION_BOOT_COMPLETED, Intent.ACTION_MY_PACKAGE_REPLACED -> {
                CaptureNotification.sync(context, SettingsStore(context).load().captureNotification)
                withReminders(context) { it.reconcile() }
            }
            Intent.ACTION_TIME_CHANGED, Intent.ACTION_TIMEZONE_CHANGED, AlarmManager.ACTION_SCHEDULE_EXACT_ALARM_PERMISSION_STATE_CHANGED ->
                withReminders(context) { it.reconcile() }
        }
    }
}
