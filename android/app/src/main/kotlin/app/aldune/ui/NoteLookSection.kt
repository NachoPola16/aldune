package app.aldune.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.data.AppSettings
import app.aldune.text.AspectSkin

/** Color con el que arranca «mismo color en todas las notas»: el amarillo del tema Clásico, como en Windows. */
private const val DEFAULT_UNIFORM = "#EBD38B"

/**
 * Cómo se ven las notas, sin tocar lo que guardan (equivalente de la parte de «Aspecto» de Windows): un color único, esquinas rectas
 * y la señal de sincronización. Las dos últimas muestran lo que decide el aspecto elegido hasta que el usuario las cambia.
 */
@Composable
fun NoteLookSection(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit) {
    var picking by rememberSaveable { mutableStateOf(false) }
    SwitchRow(stringResource(R.string.uniform_color), stringResource(R.string.uniform_color_hint), settings.uniformNoteColor != null) { on ->
        onChange { it.copy(uniformNoteColor = if (on) DEFAULT_UNIFORM else null) }
    }
    settings.uniformNoteColor?.let { hex ->
        Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.size(28.dp).clip(CircleShape).background(noteFace(hex)).border(BorderStroke(1.dp, MaterialTheme.colorScheme.outline), CircleShape))
            Text(hex, Modifier.weight(1f).padding(start = 12.dp), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            TextButton(onClick = { picking = true }) { Text(stringResource(R.string.aspect_color_change)) }
        }
        if (picking) {
            CustomColorDialog(
                initial = hex,
                onDismiss = { picking = false },
                onApply = { picked -> onChange { it.copy(uniformNoteColor = picked) }; picking = false },
            )
        }
    }
    // Igual que Windows («Color de la nota solo como franja lateral»): mismo texto y mismo sentido del interruptor, y bloqueado en los aspectos que
    // ya deciden cómo se ve el color (Telecom, Bash, Fósforo).
    val locked = AspectSkin.colorLocked(settings.appearance)
    SwitchRow(
        stringResource(R.string.stripe_note_color), if (locked) stringResource(R.string.stripe_note_color_locked) else "",
        checked = !settings.colorFill, enabled = !locked,
    ) { on -> onChange { it.copy(fillNoteColor = !on) } }
    SwitchRow(stringResource(R.string.square_corners), stringResource(R.string.square_corners_hint), settings.cornersSquare) { on ->
        onChange { it.copy(squareCorners = on) }
    }
    GroupLabel(stringResource(R.string.new_button_shape))
    val shapes = listOf(0 to R.string.new_button_follow, 1 to R.string.new_button_rounded, 2 to R.string.new_button_square)
    for ((shape, label) in shapes) Choice(stringResource(label), settings.newButtonShape == shape) { onChange { it.copy(newButtonShape = shape) } }
    SwitchRow(stringResource(R.string.sync_signal), stringResource(R.string.sync_signal_hint), settings.syncSignalVisible) { on ->
        onChange { it.copy(showSyncSignal = on) }
    }
}
