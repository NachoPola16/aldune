package app.aldune.widget

import android.content.Context
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.glance.GlanceId
import androidx.glance.GlanceModifier
import androidx.glance.Image
import androidx.glance.ImageProvider
import androidx.glance.action.clickable
import androidx.glance.appwidget.GlanceAppWidget
import androidx.glance.appwidget.action.actionStartActivity
import androidx.glance.appwidget.cornerRadius
import androidx.glance.appwidget.provideContent
import androidx.glance.background
import androidx.glance.layout.Alignment
import androidx.glance.layout.Box
import androidx.glance.layout.fillMaxSize
import androidx.glance.layout.padding
import app.aldune.R
import app.aldune.capture.CaptureIntents
import app.aldune.data.SettingsStore

/**
 * El botón de 1x1: un toque abre el editor con una nota nueva (el mismo gesto que el «+» del widget del mazo). Es el logo, así que su
 * aspecto es fijo (fondo del logo y la «A» de tres notas) y no sigue el aspecto de la app. No lee notas: no hay nada que repintar.
 */
class NewNoteWidget : GlanceAppWidget() {
    override suspend fun provideGlance(context: Context, id: GlanceId) {
        // Lee el ajuste al pintar: WidgetUpdater repinta este widget cuando cambia.
        val square = SettingsStore(context).load().newButtonSquare
        provideContent {
            Box(
                GlanceModifier.fillMaxSize().background(Color(0xFF2A261F)).cornerRadius(if (square) 0.dp else 20.dp).padding(6.dp)
                    .clickable(actionStartActivity(CaptureIntents.newNote(context))),
                contentAlignment = Alignment.Center,
            ) {
                Image(ImageProvider(R.drawable.ic_aldune_symbol), contentDescription = context.getString(R.string.new_note), modifier = GlanceModifier.fillMaxSize())
            }
        }
    }
}
