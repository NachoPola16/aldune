package app.aldune.ui

import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.runtime.mutableStateListOf
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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.unit.dp
import app.aldune.R
import app.aldune.domain.NoteService
import app.aldune.text.MarkdownExport
import app.aldune.text.NoteColorChoices
import app.aldune.text.NoteThemes

/**
 * Color y etiquetas de una nota. Los colores son los del tema activo, claros y oscuros: nunca un hex suelto (regla del proyecto) salvo el que
 * el usuario elige con «Elegir otro color…»; el de la nota se añade si no es de esa paleta (un tema propio de Windows que llegó por sync, o
 * un color libre).
 */
@OptIn(ExperimentalLayoutApi::class)
@Composable
fun NoteOptionsDialog(
    initial: NotesViewModel.NoteOptions, viewModel: NotesViewModel, onClose: () -> Unit, onProtection: () -> Unit,
    exportText: String? = null, showProtection: Boolean = true,
) {
    var color by remember { mutableStateOf(initial.color) }
    var tags by remember { mutableStateOf(initial.tags) }
    var draft by remember { mutableStateOf("") }

    val settings by viewModel.settings.collectAsState()
    val theme = NoteThemes.resolve(settings.noteThemeId, null)
    var customOpen by remember { mutableStateOf(false) }
    var nameOpen by remember { mutableStateOf(false) }
    var visibleName by remember { mutableStateOf(initial.protectedTitle) }

    // «Exportar a Markdown» guarda un .md con el selector de ficheros del sistema (el «Guardar como» de Windows).
    val context = LocalContext.current
    val newNote = stringResource(R.string.new_note_title)
    val exportFailed = stringResource(R.string.export_failed)
    val exporter = rememberLauncherForActivityResult(ActivityResultContracts.CreateDocument("text/markdown")) { uri ->
        if (uri != null && exportText != null) {
            val ok = runCatching {
                context.contentResolver.openOutputStream(uri, "wt")!!.use { it.write(MarkdownExport.toMarkdown(exportText, newNote).toByteArray(Charsets.UTF_8)) }
            }.isSuccess
            if (!ok) Toast.makeText(context, exportFailed, Toast.LENGTH_LONG).show()
        }
    }
    // El color con el que se abrió y los que se vayan eligiendo se quedan en la paleta aunque no sean del tema (ver NoteColorChoices).
    val seenColors = remember { mutableStateListOf(initial.color) }
    if (color !in seenColors) seenColors += color
    val palette = NoteColorChoices.palette(theme.lightColors + theme.darkColors, seenColors)

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
        title = { Text(stringResource(R.string.note_options), Modifier.semantics { heading() }) },
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
                // Con «mismo color en todas las notas» el color de cada nota se guarda y se sincroniza, pero no se ve: sin este aviso
                // parecería que el selector no hace nada (Windows avisa igual con SkinColorNotice).
                if (settings.uniformNoteColor != null) {
                    Text(stringResource(R.string.color_uniform_notice), style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
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
                if (exportText != null) {
                    TextButton(onClick = { exporter.launch(MarkdownExport.suggestedFileName(exportText, newNote)) }) {
                        Text(stringResource(R.string.export_markdown))
                    }
                }
                if (initial.isProtected) {
                    TextButton(onClick = { nameOpen = true }, Modifier.padding(top = 8.dp)) { Text(stringResource(R.string.visible_name_menu)) }
                }
                // Desde la lista, con la nota abierta al lado, proteger o quitar la protección dejaría el editor con el estado viejo: ahí se hace
                // desde el propio editor.
                if (showProtection) {
                    TextButton(onClick = onProtection, Modifier.padding(top = 8.dp)) {
                        Text(stringResource(if (initial.isProtected) R.string.remove_protection else R.string.protect_note))
                    }
                }
            }
        },
        confirmButton = { TextButton(onClick = onClose) { Text(stringResource(R.string.done)) } },
    )

    if (nameOpen) {
        VisibleNameDialog(visibleName, onDismiss = { nameOpen = false }) { name ->
            visibleName = name
            viewModel.setVisibleName(initial.noteId, name)
        }
    }

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
