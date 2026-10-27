package app.aldune

import android.content.Context
import android.content.Intent
import android.graphics.Color
import android.os.Build
import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.lifecycleScope
import app.aldune.capture.CaptureIntents
import app.aldune.capture.CaptureNotification
import app.aldune.data.Languages
import app.aldune.data.SettingsStore
import app.aldune.text.AppPalette
import app.aldune.ui.AldunaTheme
import app.aldune.ui.NoteLook
import app.aldune.ui.NotesApp
import app.aldune.ui.NotesViewModel
import kotlinx.coroutines.launch

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
        viewModel.settings.value.let { CaptureNotification.sync(this, it.captureNotification, it.quickList) }
    }

    // Las protegidas se vuelven a bloquear al pasar a segundo plano. Rotar también pasa por aquí, pero no es salir de la app.
    override fun onStop() {
        super.onStop()
        if (!isChangingConfigurations) viewModel.lockAllProtected()
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

    // `setRecentsScreenshotEnabled` existe desde Android 13; antes, FLAG_SECURE (que además impide las capturas de pantalla mientras dura).
    private fun hideFromRecents(hide: Boolean) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            setRecentsScreenshotEnabled(!hide)
        } else if (hide) {
            window.addFlags(WindowManager.LayoutParams.FLAG_SECURE)
        } else {
            window.clearFlags(WindowManager.LayoutParams.FLAG_SECURE)
        }
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        if (savedInstanceState == null) routeCapture(intent)
        // Con una protegida abierta, la captura de Recientes no puede llevar su texto (el sistema la guarda en disco).
        lifecycleScope.launch {
            viewModel.unlockedIds.collect { ids -> hideFromRecents(ids.isNotEmpty()) }
        }
        setContent {
            val settings by viewModel.settings.collectAsStateWithLifecycle()
            // Los iconos de la barra de estado y de navegación siguen la claridad del aspecto elegido, no la del sistema: XP claro con el
            // móvil en oscuro (o Bash con el móvil en claro) los dejaría blancos sobre beige, o negros sobre negro.
            val light = AppPalette.isLight(settings.appearance, !isSystemInDarkTheme())
            DisposableEffect(light) {
                val bars = if (light) SystemBarStyle.light(Color.TRANSPARENT, Color.TRANSPARENT) else SystemBarStyle.dark(Color.TRANSPARENT)
                enableEdgeToEdge(statusBarStyle = bars, navigationBarStyle = bars)
                onDispose {}
            }
            AldunaTheme(settings.appearance, settings.aspectColors, NoteLook(settings.uniformNoteColor, settings.cornersSquare, settings.syncSignalVisible, settings.colorFill)) {
                NotesApp(viewModel)
            }
        }
    }
}
