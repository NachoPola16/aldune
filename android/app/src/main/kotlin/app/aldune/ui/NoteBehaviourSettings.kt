package app.aldune.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import android.widget.Toast
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.material3.AlertDialog
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import app.aldune.text.NoteThemes
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.ui.draw.clip
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
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
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.data.AppSettings
import app.aldune.domain.NoteListOrder

/** Primer color de la regla «fijo» cuando el usuario aún no ha elegido (el amarillo de Clásico). */
private const val DEFAULT_FIXED = "#EBD38B"

/**
 * Orden de la lista y regla de color de las notas nuevas (`NotesManagerOrder`, `ColorAssignment` y `FixedNoteColor` de Windows).
 * Con un orden que no es el del mazo no se puede arrastrar: el arrastre cambia el mazo, y una lista ordenada por fecha no lo enseña.
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun NoteBehaviourSettings(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit, onApplyToExisting: () -> Unit) {
    GroupLabel(stringResource(R.string.list_order))
    val orders = listOf(
        NoteListOrder.DOCK to R.string.order_dock, NoteListOrder.NEWEST to R.string.order_newest,
        NoteListOrder.OLDEST to R.string.order_oldest, NoteListOrder.TITLE to R.string.order_title,
    )
    for ((order, label) in orders) Choice(stringResource(label), settings.listOrder == order.code) { onChange { it.copy(listOrder = order.code) } }
    if (settings.listOrder != 0) {
        Text(stringResource(R.string.order_no_drag), Modifier.padding(horizontal = 16.dp), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }

    GroupLabel(stringResource(R.string.color_rule))
    val rules = listOf(0 to R.string.rule_avoid_neighbors, 1 to R.string.rule_rotate, 2 to R.string.rule_most_distinct, 3 to R.string.rule_fixed)
    for ((rule, label) in rules) Choice(stringResource(label), settings.colorRule == rule) { onChange { it.copy(colorRule = rule) } }
    val theme = NoteThemes.resolve(settings.noteThemeId, null)
    val themeColors = theme.lightColors + theme.darkColors
    if (settings.colorRule == 3) {
        // Solo colores del tema: `NoteColorAssigner` ignora un fijo que no lo sea y usa el primero del tema, así que un color libre aquí
        // parecería que no hace nada.
        val chosen = settings.fixedNoteColor ?: DEFAULT_FIXED
        FlowRow(Modifier.fillMaxWidth().padding(horizontal = 16.dp), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
            for (hex in themeColors) {
                val selected = hex.equals(chosen, ignoreCase = true)
                Box(
                    Modifier.size(40.dp).clip(CircleShape).background(noteFace(hex))
                        .border(BorderStroke(if (selected) 3.dp else 1.dp, if (selected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.outline), CircleShape)
                        .clickable { onChange { it.copy(fixedNoteColor = hex) } }.semantics { contentDescription = hex },
                )
            }
        }
        if (themeColors.none { it.equals(chosen, ignoreCase = true) }) {
            Text(stringResource(R.string.fixed_color_not_in_theme), Modifier.padding(horizontal = 16.dp, vertical = 4.dp), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
        }
    }

    var confirming by rememberSaveable { mutableStateOf(false) }
    TextButton(onClick = { confirming = true }, Modifier.padding(horizontal = 8.dp)) { Text(stringResource(R.string.apply_colors_button)) }
    if (confirming) {
        val context = LocalContext.current
        val done = stringResource(R.string.apply_colors_done)
        AlertDialog(
            onDismissRequest = { confirming = false },
            title = { Text(stringResource(R.string.apply_colors_title), Modifier.semantics { heading() }) },
            text = { Text(stringResource(R.string.apply_colors_body)) },
            confirmButton = {
                TextButton(onClick = { confirming = false; onApplyToExisting(); Toast.makeText(context, done, Toast.LENGTH_SHORT).show() }) { Text(stringResource(R.string.apply_colors_confirm)) }
            },
            dismissButton = { TextButton(onClick = { confirming = false }) { Text(stringResource(R.string.cancel)) } },
        )
    }
}
