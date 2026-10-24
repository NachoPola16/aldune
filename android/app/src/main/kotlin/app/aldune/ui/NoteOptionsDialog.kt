package app.aldune.ui

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Icon
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.drawBehind
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.domain.NoteService
import app.aldune.text.NoteThemes

/**
 * Color y etiquetas de una nota. Los colores son los del tema activo, claros y oscuros: nunca un hex suelto (regla del proyecto) salvo el que
 * el usuario elige con «Elegir otro color…»; el de la nota se añade si no es de esa paleta (un tema propio de Windows que llegó por sync, o
 * un color libre).
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun NoteOptionsDialog(initial: NotesViewModel.NoteOptions, viewModel: NotesViewModel, onClose: () -> Unit, onProtection: () -> Unit) {
    var color by remember { mutableStateOf(initial.color) }
    var tags by remember { mutableStateOf(initial.tags) }
    var draft by remember { mutableStateOf("") }

    val settings by viewModel.settings.collectAsState()
    val theme = NoteThemes.resolve(settings.noteThemeId, null)
    var customOpen by remember { mutableStateOf(false) }
    val palette = (theme.lightColors + theme.darkColors).let { if (color in it) it else listOf(color) + it }

    fun addTag() {
        val next = NoteService.normalizeTags(tags + draft)
        draft = ""
        if (next != tags) {
            tags = next
            viewModel.setNoteTags(initial.noteId, next)
        }
    }

    AlertDialog(
        onDismissRequest = onClose,
        title = { Text(stringResource(R.string.note_options)) },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text(stringResource(R.string.note_color), style = MaterialTheme.typography.labelLarge)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp)) {
                    for (hex in palette) {
                        Swatch(hex, selected = hex == color) {
                            color = hex
                            viewModel.setNoteColor(initial.noteId, hex)
                        }
                    }
                }
                TextButton(onClick = { customOpen = true }) { Text(stringResource(R.string.custom_color)) }
                Text(stringResource(R.string.note_tags), Modifier.padding(top = 8.dp), style = MaterialTheme.typography.labelLarge)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    for (tag in tags) {
                        InputChip(
                            selected = false, onClick = {
                                tags = tags - tag
                                viewModel.setNoteTags(initial.noteId, tags)
                            },
                            label = { Text(tag) },
                            trailingIcon = { Icon(Icons.Default.Close, contentDescription = stringResource(R.string.remove_tag, tag), Modifier.size(18.dp)) },
                        )
                    }
                }
                Row(verticalAlignment = Alignment.CenterVertically) {
                    OutlinedTextField(
                        value = draft, onValueChange = { draft = it }, singleLine = true, modifier = Modifier.weight(1f),
                        placeholder = { Text(stringResource(R.string.tag_hint)) },
                        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done), keyboardActions = KeyboardActions(onDone = { addTag() }),
                    )
                    TextButton(onClick = ::addTag, enabled = draft.isNotBlank()) { Text(stringResource(R.string.add_tag)) }
                }
                ReminderSection(initial.noteId, viewModel)
                TextButton(onClick = onProtection, Modifier.padding(top = 8.dp)) {
                    Text(stringResource(if (initial.isProtected) R.string.remove_protection else R.string.protect_note))
                }
            }
        },
        confirmButton = { TextButton(onClick = onClose) { Text(stringResource(R.string.done)) } },
    )

    if (customOpen) {
        CustomColorDialog(
            initial = color,
            onDismiss = { customOpen = false },
            onApply = { hex ->
                color = hex
                viewModel.setNoteColor(initial.noteId, hex)
                customOpen = false
            },
        )
    }
}

@Composable
private fun Swatch(hex: String, selected: Boolean, onClick: () -> Unit) {
    val ring = MaterialTheme.colorScheme.primary
    Row(
        Modifier
            .size(40.dp)
            .clip(CircleShape)
            .background(noteFace(hex))
            .then(if (selected) Modifier.drawBehind { drawCircle(ring, radius = size.minDimension / 2 - 2.dp.toPx(), style = androidx.compose.ui.graphics.drawscope.Stroke(4.dp.toPx())) } else Modifier)
            .clickable(onClick = onClick)
            .semantics { contentDescription = hex },
    ) {}
}
