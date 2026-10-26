package app.aldune.widget

import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.GlanceAppWidgetReceiver

/** El punto de entrada del widget del mazo para el sistema (declarado en el manifiesto). */
class DockWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = DockWidget()
}

/**
 * El mismo widget del mazo con otro tamaño por defecto, para que el selector de widgets ofrezca más de un 4x2. Los tres pintan lo mismo
 * (`DockWidget` se adapta al tamaño que tenga) y se repintan juntos: `DockWidget().updateAll` alcanza a los receptores de esa clase.
 */
class DockWidgetSmallReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = DockWidget()
}

class DockWidgetLargeReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = DockWidget()
}

/** El botón de nota nueva de 1x1. */
class NewNoteWidgetReceiver : GlanceAppWidgetReceiver() {
    override val glanceAppWidget: GlanceAppWidget = NewNoteWidget()
}
