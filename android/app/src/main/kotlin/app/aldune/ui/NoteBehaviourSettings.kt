package app.aldune.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
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
@Composable
fun NoteBehaviourSettings(settings: AppSettings, onChange: ((AppSettings) -> AppSettings) -> Unit) {
    Text(stringResource(R.string.list_order), Modifier.padding(horizontal = 16.dp), style = MaterialTheme.typography.labelLarge)
    val orders = listOf(
        NoteListOrder.DOCK to R.string.order_dock, NoteListOrder.NEWEST to R.string.order_newest,
        NoteListOrder.OLDEST to R.string.order_oldest, NoteListOrder.TITLE to R.string.order_title,
    )
    for ((order, label) in orders) Choice(stringResource(label), settings.listOrder == order.code) { onChange { it.copy(listOrder = order.code) } }
    if (settings.listOrder != 0) {
        Text(stringResource(R.string.order_no_drag), Modifier.padding(horizontal = 16.dp), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
    }

    Text(stringResource(R.string.color_rule), Modifier.padding(start = 16.dp, end = 16.dp, top = 8.dp), style = MaterialTheme.typography.labelLarge)
    val rules = listOf(0 to R.string.rule_avoid_neighbors, 1 to R.string.rule_rotate, 2 to R.string.rule_most_distinct, 3 to R.string.rule_fixed)
    for ((rule, label) in rules) Choice(stringResource(label), settings.colorRule == rule) { onChange { it.copy(colorRule = rule) } }
    if (settings.colorRule == 3) {
        var picking by rememberSaveable { mutableStateOf(false) }
        val hex = settings.fixedNoteColor ?: DEFAULT_FIXED
        Row(Modifier.fillMaxWidth().padding(horizontal = 16.dp), verticalAlignment = Alignment.CenterVertically) {
            Box(Modifier.padding(end = 12.dp).size(28.dp).clip(CircleShape).background(noteFace(hex)).border(BorderStroke(1.dp, MaterialTheme.colorScheme.outline), CircleShape))
            Text(hex, Modifier.weight(1f), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            TextButton(onClick = { picking = true }) { Text(stringResource(R.string.aspect_color_change)) }
        }
        if (picking) {
            CustomColorDialog(initial = hex, onDismiss = { picking = false }, onApply = { picked -> onChange { it.copy(fixedNoteColor = picked) }; picking = false })
        }
    }
}
