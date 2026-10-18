package app.aldune.capture

import android.annotation.SuppressLint
import android.app.PendingIntent
import android.os.Build
import android.service.quicksettings.Tile
import android.service.quicksettings.TileService

/** El mosaico «Nota nueva» de Ajustes rápidos. Es un botón, no un interruptor: el estado no cambia nunca. */
class NewNoteTileService : TileService() {
    override fun onStartListening() {
        qsTile?.let {
            it.state = Tile.STATE_INACTIVE
            it.updateTile()
        }
    }

    override fun onClick() {
        // Con el móvil bloqueado, primero se pide desbloquear; la nota se abre al terminar.
        if (isLocked) unlockAndRun { openEditor() } else openEditor()
    }

    private fun openEditor() {
        val intent = CaptureIntents.newNote(this)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
            // Desde la 14, la variante con Intent lanza una excepción cuando targetSdk >= 34.
            startActivityAndCollapse(PendingIntent.getActivity(this, 0, intent, PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT))
        } else {
            // Antes de la 14 la variante con PendingIntent no existe: lint la da por obsoleta, pero aquí es la única.
            @SuppressLint("StartActivityAndCollapseDeprecated")
            @Suppress("DEPRECATION")
            startActivityAndCollapse(intent)
        }
    }
}
