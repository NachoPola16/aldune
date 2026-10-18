package app.aldune.widget

import android.content.Context
import android.util.Log
import androidx.glance.appwidget.updateAll
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/**
 * Pide repintar el widget tras un cambio en las notas. Se agrupa: la primera petición espera [WINDOW_MS] y repinta una vez, y las que
 * llegan mientras tanto quedan incluidas (el widget lee las notas al repintar, no al pedirlo). Sin esto, escribir en el editor
 * repintaría el widget con cada guardado.
 *
 * Una petición que llega **mientras se pinta** no puede darse por incluida: el repintado ya leyó las notas al empezar. Por eso hay una marca
 * ([dirty]) y el bucle vuelve a esperar y pintar mientras siga puesta; sin ella, el widget se quedaba una nota por detrás.
 */
object WidgetUpdater {
    private const val WINDOW_MS = 1_000L

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private val lock = Any()
    private var dirty = false
    private var running = false

    private val versionFlow = MutableStateFlow(0L)

    /** Sube con cada repintado pedido. Un widget con la sesión viva lo observa y recarga las notas (ver `DockWidget.provideGlance`). */
    val version: StateFlow<Long> = versionFlow.asStateFlow()

    fun request(context: Context) {
        val app = context.applicationContext
        synchronized(lock) {
            dirty = true
            if (running) return
            running = true
        }
        scope.launch {
            while (true) {
                delay(WINDOW_MS)
                // Quitar la marca y salir se decide en el mismo bloque: una petición que llegue justo ahora o ve `running` y se queda
                // anotada para otra vuelta, o ve `running == false` y arranca su propio bucle.
                synchronized(lock) {
                    if (!dirty) {
                        running = false
                        return@launch
                    }
                    dirty = false
                }
                versionFlow.update { it + 1 }
                // Con la sesión del widget viva basta la versión; sin ella (proceso nuevo, sesión caducada), updateAll arranca una.
                runCatching { DockWidget().updateAll(app) }.onFailure { Log.w("WidgetUpdater", "no se pudo repintar el widget", it) }
            }
        }
    }
}
