package app.aldune.capture

import android.Manifest
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import app.aldune.R

/**
 * La notificación fija «Nota nueva» (spec de F6 §2.2). No es un servicio en primer plano: no hay trabajo detrás, solo un botón, y no
 * lleva nada de ninguna nota (salvo con la lista rápida, que en la pantalla de bloqueo se oculta), así que puede verse en la pantalla de bloqueo.
 */
object CaptureNotification {
    private const val CHANNEL_ID = "capture"
    private const val NOTIFICATION_ID = 1

    /** ¿Se puede publicar? En Android 13+ hace falta el permiso, y además el usuario puede haber bloqueado las notificaciones de la app. */
    fun canPost(context: Context): Boolean = hasPermission(context) && NotificationManagerCompat.from(context).areNotificationsEnabled()

    fun hasPermission(context: Context): Boolean =
        Build.VERSION.SDK_INT < Build.VERSION_CODES.TIRAMISU ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED

    /**
     * Deja la notificación como pide el ajuste: puesta si está activo y se puede, quitada en cualquier otro caso. Se llama al abrir la app, al
     * arrancar el móvil y al actualizar la app; si el usuario la descartó, no se repone hasta una de esas ocasiones.
     */
    fun sync(context: Context, enabled: Boolean, quickList: QuickList? = null, feedback: String? = null) {
        if (enabled && canPost(context)) show(context, quickList, feedback) else cancel(context)
    }

    /** [quickList] añade la acción de responder para añadir un artículo; [feedback] cambia el texto un momento (lo que se acaba de añadir). */
    private fun show(context: Context, quickList: QuickList?, feedback: String?) {
        val manager = context.getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(
            NotificationChannel(CHANNEL_ID, context.getString(R.string.capture_section), NotificationManager.IMPORTANCE_LOW).apply {
                description = context.getString(R.string.capture_channel_description)
                setShowBadge(false)
            },
        )
        val open = PendingIntent.getActivity(
            context, 0, CaptureIntents.newNote(context), PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        fun base() = NotificationCompat.Builder(context, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_aldune)
            .setContentTitle(context.getString(R.string.new_note))
            .setContentIntent(open)
            .addAction(R.drawable.ic_new_note, context.getString(R.string.new_note), open)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setShowWhen(false)
        // Con la lista rápida (su título) o lo recién añadido, el texto sale de una nota: en la pantalla de bloqueo solo se ve la versión
        // genérica, y la acción de responder pide desbloquear. Sin eso no lleva nada de ninguna nota y puede verse entera.
        val showsNoteText = quickList != null || feedback != null
        val notification = base()
            .setContentText(feedback ?: context.getString(R.string.capture_notification_text))
            .apply { if (quickList != null) addAction(QuickAdd.action(context, quickList)) }
            .setVisibility(if (showsNoteText) NotificationCompat.VISIBILITY_PRIVATE else NotificationCompat.VISIBILITY_PUBLIC)
            .apply {
                if (showsNoteText) {
                    setPublicVersion(base().setContentText(context.getString(R.string.capture_notification_text)).setVisibility(NotificationCompat.VISIBILITY_PUBLIC).build())
                }
            }
            .build()
        manager.notify(NOTIFICATION_ID, notification)
    }

    private fun cancel(context: Context) {
        context.getSystemService(NotificationManager::class.java).cancel(NOTIFICATION_ID)
    }
}
