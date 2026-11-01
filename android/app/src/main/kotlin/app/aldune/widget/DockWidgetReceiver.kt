package app.aldune.widget

import android.content.Context
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver

/** What the three dock widget sizes share: the widget itself and forgetting its tag filter when the user removes it. */
abstract class BaseDockWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = DockWidget()

    override fun onDeleted(context: Context, appWidgetIds: IntArray) {
        WidgetTagStore.forget(context, appWidgetIds)
        super.onDeleted(context, appWidgetIds)
    }
}

/** El punto de entrada del widget del mazo para el sistema (declarado en el manifiesto). */
class DockWidgetReceiver : BaseDockWidgetReceiver()

/**
 * El mismo widget del mazo con otro tamaño por defecto, para que el selector de widgets ofrezca más de un 4x2. Los tres pintan lo mismo
 * (`DockWidget` se adapta al tamaño que tenga) y se repintan juntos: `DockWidget().updateAll` alcanza a los receptores de esa clase.
 */
class DockWidgetSmallReceiver : BaseDockWidgetReceiver()

class DockWidgetLargeReceiver : BaseDockWidgetReceiver()

/** El botón de nota nueva de 1x1. */
class NewNoteWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = NewNoteWidget()
}
