package app.aldune

import android.content.Context
import android.content.Intent
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.capture.CaptureIntents
import app.aldune.capture.CaptureNotification
import app.aldune.data.Languages
import app.aldune.data.SettingsStore
import app.aldune.ui.AldunaTheme
import app.aldune.ui.NotesApp
import app.aldune.ui.NotesViewModel

class MainActivity : ComponentActivity() {
    private val viewModel: NotesViewModel by viewModels()

    // El idioma elegido en Ajustes se aplica aquí, antes de que exista ninguna pantalla; cambiarlo recrea la actividad.
    override fun attachBaseContext(newBase: Context) {
        super.attachBaseContext(Languages.wrap(newBase, SettingsStore(newBase).load().language))
    }

    // Volver a primer plano pide una sync (la política decide si toca: no antes de 30 s desde la última).
    override fun onStart() {
        super.onStart()
        viewModel.onForeground()
        // Abrir la app repone la notificación de captura si el ajuste está activo (o la quita si el permiso ya no está).
        CaptureNotification.sync(this, viewModel.settings.value.captureNotification)
    }

    // El acceso directo, el mosaico y la notificación llegan aquí cuando la app ya está abierta (singleTop).
    override fun onNewIntent(intent: Intent) {
        super.onNewIntent(intent)
        setIntent(intent)
        routeCapture(intent)
    }

    // Un intent se atiende una sola vez: al recrear la actividad (rotar, proceso matado) o volver desde Recientes el mismo intent vuelve a
    // entregarse, y crearía otra nota.
    private fun routeCapture(intent: Intent?) {
        if (intent == null || intent.flags and Intent.FLAG_ACTIVITY_LAUNCHED_FROM_HISTORY != 0) return
        CaptureIntents.requestOf(intent)?.let(viewModel::requestCapture)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        if (savedInstanceState == null) routeCapture(intent)
        setContent {
            val settings by viewModel.settings.collectAsStateWithLifecycle()
            AldunaTheme(settings.appearance) {
                NotesApp(viewModel)
            }
        }
    }
}
