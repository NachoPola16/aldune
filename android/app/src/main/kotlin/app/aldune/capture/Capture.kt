package app.aldune.capture

import android.content.Context
import android.content.Intent
import app.aldune.MainActivity
import app.aldune.core.ProfileCodes

/** Lo que una puerta de captura (acceso directo, mosaico, notificación, widget) le pide a la interfaz. */
sealed interface CaptureRequest {
    /** Abrir el editor con una nota nueva, todavía sin texto: no existe hasta el primer carácter. */
    data object NewNote : CaptureRequest

    /** Lo que otra app compartió con Aldune (`ACTION_SEND` de texto): `EXTRA_TEXT` y, si trae, `EXTRA_SUBJECT`. */
    data class SharedText(val text: String?, val subject: String?) : CaptureRequest

    /** Un código de perfil de sync (`aldune-profile-v2:…`) que llegó compartido: no es una nota, abre «Añadir perfil» con el código puesto. */
    data class ProfileCode(val code: String) : CaptureRequest

    /** Tocar una tira del widget: abrir la nota [id]. */
    data class OpenNote(val id: String) : CaptureRequest
}

object CaptureIntents {
    const val ACTION_NEW_NOTE = "app.aldune.action.NEW_NOTE"
    const val ACTION_OPEN_NOTE = "app.aldune.action.OPEN_NOTE"
    const val EXTRA_NOTE_ID = "note_id"

    /**
     * El intent de «nota nueva» para quien no es una actividad (mosaico, notificación). `CLEAR_TOP` con `singleTop` entrega el intent a la
     * instancia que ya existe en vez de apilar otra copia de la app; `NEW_TASK` es obligatorio desde fuera de una actividad.
     */
    fun newNote(context: Context): Intent =
        Intent(context, MainActivity::class.java)
            .setAction(ACTION_NEW_NOTE)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)

    /** El intent de una tira del widget: abre esa nota. Mismas banderas que [newNote]. */
    fun openNote(context: Context, id: String): Intent =
        Intent(context, MainActivity::class.java)
            .setAction(ACTION_OPEN_NOTE)
            .putExtra(EXTRA_NOTE_ID, id)
            .addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TOP)

    /** Qué pide un intent que llegó a la actividad, o `null` si no es de captura. */
    fun requestOf(intent: Intent?): CaptureRequest? = when (intent?.action) {
        ACTION_NEW_NOTE -> CaptureRequest.NewNote
        ACTION_OPEN_NOTE -> intent.getStringExtra(EXTRA_NOTE_ID)?.let(CaptureRequest::OpenNote)
        Intent.ACTION_SEND ->
            if (intent.type?.startsWith("text/") == true) {
                val text = intent.getCharSequenceExtra(Intent.EXTRA_TEXT)?.toString()
                ProfileCodes.findShareCode(text)?.let(CaptureRequest::ProfileCode)
                    ?: CaptureRequest.SharedText(text, intent.getCharSequenceExtra(Intent.EXTRA_SUBJECT)?.toString())
            } else {
                null
            }
        else -> null
    }
}
