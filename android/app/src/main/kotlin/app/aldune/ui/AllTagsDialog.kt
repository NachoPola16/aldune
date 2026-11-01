package app.aldune.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.TextButton
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.domain.TagOrder

/**
 * Todas las etiquetas en una hoja: buscable, y con flechas para ordenarlas a mano (orden local de este dispositivo; alfabético mientras no se toque).
 * Las flechas (y no arrastrar) porque son un objetivo táctil grande y funcionan con TalkBack; con una búsqueda activa no se muestran, porque mover
 * dentro de una lista filtrada no tiene un orden claro.
 */
@Composable
fun AllTagsDialog(
    tags: List<String>, selected: String?, showHash: Boolean,
    onSelect: (String?) -> Unit, onMove: (String, Int) -> Unit, onDismiss: () -> Unit,
    tagColors: Map<String, String> = emptyMap(), paletteColors: List<String> = emptyList(), onSetColor: (String, String?) -> Unit = { _, _ -> },
) {
    var query by remember { mutableStateOf("") }
    // The tag whose colour palette is open under its row.
    var paletteFor by remember { mutableStateOf<String?>(null) }
    val shown = TagOrder.filter(tags, query)
    val searching = query.isNotBlank()
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(stringResource(R.string.all_tags)) },
        text = {
            Column {
                OutlinedTextField(query, { query = it }, Modifier.fillMaxWidth(), singleLine = true, placeholder = { Text(stringResource(R.string.search_tags)) })
                LazyColumn(Modifier.fillMaxWidth().heightIn(max = 360.dp).padding(top = 8.dp), verticalArrangement = Arrangement.spacedBy(0.dp)) {
                    if (!searching) item {
                        Text(
                            stringResource(R.string.all_notes_chip), style = MaterialTheme.typography.bodyLarge,
                            color = if (selected == null) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface,
                            modifier = Modifier.fillMaxWidth().clickable { onSelect(null) }.padding(vertical = 14.dp, horizontal = 4.dp),
                        )
                    }
                    items(shown, key = { it.lowercase() }) { tag ->
                        val isSelected = tag.equals(selected, ignoreCase = true)
                        Column {
                        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
                            Row(
                                Modifier.weight(1f).clickable { onSelect(if (isSelected) null else tag) }.padding(vertical = 14.dp, horizontal = 4.dp),
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                if (!showHash) Icon(TagIcon, contentDescription = null, Modifier.padding(end = 8.dp).size(18.dp))
                                Text(
                                    if (showHash) "#$tag" else tag, style = MaterialTheme.typography.bodyLarge,
                                    color = if (isSelected) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurface,
                                )
                            }
                            val own = tagColors.entries.firstOrNull { it.key.equals(tag, ignoreCase = true) }?.value
                            val colourCd = stringResource(R.string.tag_colour, tag)
                            IconButton({ paletteFor = if (paletteFor.equals(tag, ignoreCase = true)) null else tag }, Modifier.semantics { contentDescription = colourCd }) {
                                Box(
                                    Modifier.size(20.dp).clip(CircleShape).background(own?.let { noteFace(it) } ?: Color.Transparent)
                                        .border(1.dp, MaterialTheme.colorScheme.outline, CircleShape),
                                )
                            }
                            if (!searching) {
                                val up = stringResource(R.string.tag_move_up, tag)
                                val down = stringResource(R.string.tag_move_down, tag)
                                IconButton({ onMove(tag, -1) }, Modifier.semantics { contentDescription = up }) { Icon(Icons.Filled.KeyboardArrowUp, null) }
                                IconButton({ onMove(tag, 1) }, Modifier.semantics { contentDescription = down }) { Icon(Icons.Filled.KeyboardArrowDown, null) }
                            }
                        }
                        if (paletteFor.equals(tag, ignoreCase = true)) {
                            @OptIn(androidx.compose.foundation.layout.ExperimentalLayoutApi::class)
                            FlowRow(Modifier.fillMaxWidth().padding(start = 4.dp, bottom = 8.dp), horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                                for (hex in paletteColors) {
                                    Box(
                                        Modifier.size(36.dp).clip(CircleShape).background(noteFace(hex)).border(1.dp, MaterialTheme.colorScheme.outline, CircleShape)
                                            .clickable { onSetColor(tag, hex); paletteFor = null }.semantics { contentDescription = hex },
                                    )
                                }
                                TextButton({ onSetColor(tag, null); paletteFor = null }) { Text(stringResource(R.string.tag_colour_none)) }
                            }
                        }
                        }
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onDismiss) { Text(stringResource(R.string.history_close)) } },
    )
}
