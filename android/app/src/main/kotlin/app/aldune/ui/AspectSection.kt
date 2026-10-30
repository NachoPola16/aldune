package app.aldune.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.data.AppSettings
import app.aldune.text.AspectCatalog

/** Un color de un aspecto retro, sin tocar el resto de lo elegido para él ni para los demás aspectos. */
internal fun AppSettings.withAspectColor(aspectId: String, slotId: String, hex: String): AppSettings =
    copy(aspectColors = aspectColors + (aspectId to ((aspectColors[aspectId] ?: emptyMap()) + (slotId to hex))))

private fun slotNameRes(slotId: String): Int? = when (slotId) {
    "titleBar" -> R.string.aspect_slot_title_bar
    "accent" -> R.string.aspect_slot_accent
    "ink" -> R.string.aspect_slot_ink
    "panel" -> R.string.aspect_slot_panel
    "trace" -> R.string.aspect_slot_trace
    "background" -> R.string.aspect_slot_terminal
    "user" -> R.string.aspect_slot_user
    "path" -> R.string.aspect_slot_path
    "phosphor" -> R.string.aspect_slot_phosphor
    "details" -> R.string.aspect_slot_details
    "paper" -> R.string.aspect_slot_paper
    else -> null
}

/**
 * Los colores del aspecto retro elegido (equivalente de «Colores del aspecto» de Windows): un renglón por hueco con su muestra, «Cambiar»
 * (color libre) y muestras de un toque; las paletas de Bash; y «Restablecer colores». Con un aspecto de siempre no pinta nada.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun AspectSection(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit) {
    val aspect = AspectCatalog.forMode(settings.appearance) ?: return
    val effective = AspectCatalog.resolve(aspect.mode, settings.aspectColors[aspect.id])
    var editing by remember(aspect.id) { mutableStateOf<String?>(null) }

    Section(R.string.aspect_colors)
    for (slot in aspect.slots) {
        val current = effective.getValue(slot.id)
        Column(Modifier.fillMaxWidth().padding(horizontal = 16.dp, vertical = 4.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Swatch(current, Modifier.size(28.dp))
                Column(Modifier.weight(1f).padding(start = 12.dp)) {
                    Text(slotNameRes(slot.id)?.let { stringResource(it) } ?: slot.id)
                    Text(current, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
                TextButton(onClick = { editing = slot.id }) { Text(stringResource(R.string.aspect_color_change)) }
            }
            Row(Modifier.padding(top = 2.dp), horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                for (hex in slot.suggestions) {
                    Swatch(hex, Modifier.size(32.dp).clickable { onChange { it.withAspectColor(aspect.id, slot.id, hex) } }, description = hex)
                }
            }
        }
    }
    editing?.let { slotId ->
        CustomColorDialog(
            initial = effective.getValue(slotId),
            onDismiss = { editing = null },
            onApply = { hex -> onChange { it.withAspectColor(aspect.id, slotId, hex) }; editing = null },
        )
    }

    if (aspect.presets.isNotEmpty()) {
        Text(
            stringResource(R.string.aspect_presets), Modifier.padding(start = 16.dp, end = 16.dp, top = 12.dp),
            style = MaterialTheme.typography.labelLarge,
        )
        // FlowRow: con fuente grande o pantalla estrecha los botones pasan a otra línea en vez de salirse.
        FlowRow(Modifier.padding(horizontal = 8.dp), horizontalArrangement = Arrangement.spacedBy(4.dp)) {
            for (preset in aspect.presets) {
                // Una paleta sustituye todos los huecos del aspecto de una vez.
                TextButton(onClick = { onChange { it.copy(aspectColors = it.aspectColors + (aspect.id to preset.colors)) } }) { Text(preset.name) }
            }
        }
    }
    TextButton(
        onClick = { onChange { it.copy(aspectColors = it.aspectColors - aspect.id) } },
        Modifier.padding(horizontal = 8.dp),
    ) { Text(stringResource(R.string.aspect_colors_reset)) }
}

/** Un círculo de color con borde, para que se vea también cuando se parece al fondo. */
@Composable
private fun Swatch(hex: String, modifier: Modifier, description: String? = null) {
    val base = modifier.clip(CircleShape).background(noteFace(hex)).border(BorderStroke(1.dp, MaterialTheme.colorScheme.outline), CircleShape)
    Box(if (description != null) base.semantics { contentDescription = description } else base)
}
