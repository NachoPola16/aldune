package app.aldune.ui

import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.border
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.layout.offset
import androidx.compose.ui.layout.layout
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
import androidx.compose.foundation.layout.Spacer
import app.aldune.domain.ReminderLabel
import androidx.compose.runtime.produceState
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.ui.semantics.stateDescription
import androidx.compose.ui.semantics.Role
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material.icons.filled.KeyboardArrowUp
import androidx.compose.material.icons.filled.KeyboardArrowDown
import androidx.compose.foundation.layout.heightIn
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
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.selected
import app.aldune.text.ColorName
import app.aldune.text.ColorNames
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
@OptIn(ExperimentalLayoutApi::class, ExperimentalMaterial3Api::class)
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
    var pinned by remember { mutableStateOf(initial.pinnedToTop) }

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

    // Un panel inferior en vez de un diálogo de pantalla casi entera: se alcanza con el pulgar y se cierra deslizando. Con una docena de controles
    // en una sola ventana había demasiado que mirar, así que Etiquetas y Recordatorio van plegados (abiertos si la nota ya tiene algo).
    ModalBottomSheet(onDismissRequest = onClose, sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true)) {
            Column(
                Modifier.verticalScroll(rememberScrollState()).padding(horizontal = 24.dp).padding(bottom = 24.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Text(stringResource(R.string.note_options), style = MaterialTheme.typography.headlineSmall, modifier = Modifier.semantics { heading() })
                Text(stringResource(R.string.note_color), style = MaterialTheme.typography.labelLarge)
                // Balanced rows (8 colours = 4 + 4, not 7 + 1 orphan); at most 7 per row, which is what fits on a phone.
                val perRow = if (palette.isEmpty()) 1 else (palette.size + (palette.size + 6) / 7 - 1) / ((palette.size + 6) / 7)
                FlowRow(horizontalArrangement = Arrangement.spacedBy(10.dp), verticalArrangement = Arrangement.spacedBy(10.dp), maxItemsInEachRow = perRow) {
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
                TextButton(onClick = { customOpen = true }, Modifier.alignWithHeadings()) { Text(stringResource(R.string.custom_color)) }
                CollapsibleSection(stringResource(R.string.note_tags), startOpen = tags.isNotEmpty(), summary = tags.joinToString(", ")) {
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
                // Las etiquetas que ya existen en otras notas, a un toque: antes solo se podía escribir una nueva.
                val known = (viewModel.state.collectAsState().value as? UiState.Ready)?.allTags.orEmpty()
                    .filter { candidate -> tags.none { it.equals(candidate, ignoreCase = true) } }
                if (known.isNotEmpty()) {
                    Text(stringResource(R.string.existing_tags), style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    FlowRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        for (tag in known) {
                            androidx.compose.material3.SuggestionChip(
                                onClick = {
                                    tags = NoteService.normalizeTags(tags + tag)
                                    viewModel.setNoteTags(initial.noteId, tags)
                                },
                                label = { Text(tag) },
                            )
                        }
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
                }
                // El resumen se relee al plegar: dentro de la sección el recordatorio se pone y se quita, y plegada tiene que decir lo que hay ahora.
                var reminderTick by remember { mutableIntStateOf(0) }
                val due by produceState<java.time.Instant?>(null, reminderTick) { value = viewModel.reminderFor(initial.noteId) }
                val reminderSummary = due?.let { reminderText(ReminderLabel.of(it, java.time.Instant.now(), java.time.ZoneId.systemDefault())) }
                CollapsibleSection(stringResource(R.string.reminder_title), startOpen = false, summary = reminderSummary, onToggle = { reminderTick++ }) {
                    ReminderSection(initial.noteId, viewModel)
                }
                TextButton(onClick = { pinned = !pinned; viewModel.setPinned(initial.noteId, pinned) }, Modifier.alignWithHeadings()) {
                    Text(stringResource(if (pinned) R.string.note_unpin else R.string.note_pin))
                }
                if (exportText != null) {
                    TextButton(onClick = { exporter.launch(MarkdownExport.suggestedFileName(exportText, newNote)) }, Modifier.alignWithHeadings()) {
                        Text(stringResource(R.string.export_markdown))
                    }
                }
                if (initial.isProtected) {
                    TextButton(onClick = { nameOpen = true }, Modifier.padding(top = 8.dp).alignWithHeadings()) { Text(stringResource(R.string.visible_name_menu)) }
                }
                // Desde la lista, con la nota abierta al lado, proteger o quitar la protección dejaría el editor con el estado viejo: ahí se hace
                // desde el propio editor.
                if (showProtection) {
                    TextButton(onClick = onProtection, Modifier.padding(top = 8.dp).alignWithHeadings()) {
                        Text(stringResource(if (initial.isProtected) R.string.remove_protection else R.string.protect_note))
                    }
                }
            }
    }

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

/** Inner padding of the text buttons and of the section headers of the options sheet; headings and buttons are aligned by removing it again. */
private val SHEET_TEXT_INSET = 12.dp

/** Moves a text button left by its own inner padding, so its text starts where the headings of the sheet start. */
private fun Modifier.alignWithHeadings(): Modifier = this.offset(x = -SHEET_TEXT_INSET)

/** Makes the element [by] wider on each side without changing the space it takes in its parent. */
private fun Modifier.widenBy(by: androidx.compose.ui.unit.Dp): Modifier = this.layout { measurable, constraints ->
    val extra = by.roundToPx()
    val width = constraints.maxWidth + 2 * extra
    val placeable = measurable.measure(constraints.copy(minWidth = width, maxWidth = width))
    layout(constraints.maxWidth, placeable.height) { placeable.place(-extra, 0) }
}

/** Una sección que se pliega: el título es un botón de ancho completo (48 dp) con la flecha que dice si está abierta. */
@Composable
private fun CollapsibleSection(
    title: String, startOpen: Boolean, summary: String? = null, onToggle: () -> Unit = {}, content: @Composable () -> Unit,
) {
    var open by rememberSaveable(title) { mutableStateOf(startOpen) }
    Row(
        // The highlight (pressed / focused) runs 12 dp beyond the text on each side, so the title is not flush against the band's edge, and
        // the row is widened by the same amount so the title still lines up with the other headings.
        Modifier.widenBy(SHEET_TEXT_INSET).heightIn(min = 48.dp).clip(RoundedCornerShape(8.dp)).clickable(role = Role.Button) { open = !open; onToggle() }
            .padding(horizontal = SHEET_TEXT_INSET)
            .semantics { stateDescription = if (open) "▲" else "▼" },
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Text(title, style = MaterialTheme.typography.labelLarge)
        // Plegada, dice lo que contiene (el recordatorio puesto, las etiquetas): sin abrirla no había forma de saberlo.
        if (!open && !summary.isNullOrEmpty()) {
            Text(summary, Modifier.weight(1f).padding(horizontal = 12.dp), style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, maxLines = 1, overflow = androidx.compose.ui.text.style.TextOverflow.Ellipsis)
        } else {
            Spacer(Modifier.weight(1f))
        }
        Icon(if (open) Icons.Default.KeyboardArrowUp else Icons.Default.KeyboardArrowDown, contentDescription = null)
    }
    if (open) content()
}

@Composable
private fun Swatch(hex: String, selected: Boolean, onClick: () -> Unit) {
    val ring = MaterialTheme.colorScheme.primary
    // El nombre del color («Amarillo»), no su código: con TalkBack «#EBD38B» no dice nada.
    val name = ColorNames.of(hex)?.let { stringResource(colorNameRes(it)) } ?: hex
    Row(
        Modifier
            .size(40.dp)
            .clip(CircleShape)
            .background(noteFace(hex))
            // A thin outline: in the dark themes several note colours are almost the same grey and nearly the sheet's own colour.
            .border(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.6f), CircleShape)
            .then(if (selected) Modifier.drawBehind { drawCircle(ring, radius = size.minDimension / 2 - 2.dp.toPx(), style = androidx.compose.ui.graphics.drawscope.Stroke(4.dp.toPx())) } else Modifier)
            .clickable(onClick = onClick)
            .semantics {
                contentDescription = name
                this.selected = selected
                role = Role.RadioButton
            },
    ) {}
}

private fun colorNameRes(name: ColorName): Int = when (name) {
    ColorName.RED -> R.string.color_name_red
    ColorName.ORANGE -> R.string.color_name_orange
    ColorName.YELLOW -> R.string.color_name_yellow
    ColorName.GREEN -> R.string.color_name_green
    ColorName.TURQUOISE -> R.string.color_name_turquoise
    ColorName.BLUE -> R.string.color_name_blue
    ColorName.PURPLE -> R.string.color_name_purple
    ColorName.PINK -> R.string.color_name_pink
    ColorName.BROWN -> R.string.color_name_brown
    ColorName.GRAY -> R.string.color_name_gray
    ColorName.WHITE -> R.string.color_name_white
    ColorName.BLACK -> R.string.color_name_black
}
