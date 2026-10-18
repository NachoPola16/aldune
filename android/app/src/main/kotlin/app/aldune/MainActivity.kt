package app.aldune

import android.content.Context
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
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
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val settings by viewModel.settings.collectAsStateWithLifecycle()
            AldunaTheme(settings.appearance) {
                NotesApp(viewModel)
            }
        }
    }
}
