package app.aldune.widget

import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver

/** El punto de entrada del widget del mazo para el sistema (declarado en el manifiesto). */
class DockWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = DockWidget()
}
