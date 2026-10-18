package app.aldune.capture

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import app.aldune.data.SettingsStore

/**
 * Tras arrancar el móvil o actualizar la app las notificaciones se pierden: se repone la de captura si el ajuste está activo. F7 reutiliza
 * este receptor para reprogramar los recordatorios.
 */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (intent.action == Intent.ACTION_BOOT_COMPLETED || intent.action == Intent.ACTION_MY_PACKAGE_REPLACED) {
            CaptureNotification.sync(context, SettingsStore(context).load().captureNotification)
        }
    }
}
