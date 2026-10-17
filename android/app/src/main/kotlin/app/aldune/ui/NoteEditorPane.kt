package app.aldune.ui

import androidx.compose.foundation.gestures.awaitEachGesture
import androidx.compose.foundation.gestures.awaitFirstDown
import androidx.compose.foundation.gestures.waitForUpOrCancellation
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.safeDrawingPadding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.input.InputTransformation
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.runtime.snapshotFlow
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.DropdownMenu
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.withFrameNanos
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.input.key.Key
import androidx.compose.ui.input.key.KeyEventType
import androidx.compose.ui.input.key.isAltPressed
import androidx.compose.ui.input.key.isCtrlPressed
import androidx.compose.ui.input.key.isShiftPressed
import androidx.compose.ui.input.key.key
import androidx.compose.ui.input.key.onPreviewKeyEvent
import androidx.compose.ui.input.key.type
import androidx.compose.ui.input.pointer.PointerEventPass
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalViewConfiguration
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.text.TextRange
import androidx.compose.ui.text.coerceIn
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LifecycleEventEffect
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import app.aldune.R
import app.aldune.text.BulletLines
import app.aldune.text.LineDirection
import app.aldune.text.LineMovement
import app.aldune.text.ListIndent
import app.aldune.text.ListKeys
import app.aldune.text.TaskLines
import app.aldune.text.TaskLists
import app.aldune.text.TextAndCaret
import app.aldune.text.TextSelection
import kotlinx.coroutines.launch

/**
 * Editor de una nota: texto con ayudas de lista (casillas, viñetas, sangría) y guardado automático (lo hace el `ViewModel`, ver
 * `openDraft`). [key] es el id de la nota, o "" para una nota nueva.
 */
/**
 * Enter y Retroceso del teclado virtual. Se corrigen dentro de la propia edición del teclado (no después, en `onValueChange`): así la región
 * de composición de teclados como AnySoftKeyboard queda coherente con el texto que reescribimos, y no vuelve a escribir la palabra a medias.
 * Las ediciones hechas por código (barra, atajos) no pasan por aquí.
 */
private val ListKeysTransformation = InputTransformation {
    val old = TextSelection(originalText.toString(), originalSelection.min, originalSelection.max)
    val typed = TextAndCaret(asCharSequence().toString(), selection.start)
    if (old.text == typed.text) return@InputTransformation
    val fixed = ListKeys.resolve(old, typed) ?: return@InputTransformation
    replace(0, length, fixed.text)
    selection = TextRange(fixed.caret)
}

@Composable
fun NoteEditorPane(key: String, viewModel: NotesViewModel, onBack: () -> Unit, showBack: Boolean) {
    val state = remember(key) { TextFieldState() }
    var loaded by remember(key) { mutableStateOf(false) }
    var options by remember(key) { mutableStateOf<NotesViewModel.NoteOptions?>(null) }
    val scope = rememberCoroutineScope()
    var layout by remember(key) { mutableStateOf<(() -> TextLayoutResult?)?>(null) }
    // Toque pendiente sobre el texto. No se aplica en el mismo evento: el campo, al recibir ese mismo toque, emite el texto
    // anterior con el cursor nuevo y pisaría el cambio; se espera al siguiente fotograma (ver LaunchedEffect de abajo).
    var tap by remember(key) { mutableStateOf<Offset?>(null) }

    LaunchedEffect(key) {
        val text = viewModel.openDraft(key)
        state.edit {
            replace(0, length, text)
            selection = TextRange(0)
        }
        loaded = true
    }
    // Único punto por el que el texto llega al guardado: así ningún camino (teclado, barra, atajo, toque) se lo salta.
    LaunchedEffect(key) {
        snapshotFlow { state.text.toString() }.collect { if (loaded) viewModel.updateDraft(key, it) }
    }
    // Al salir de la nota (otra nota, atrás, el panel se destruye) y al pasar la app a segundo plano.
    DisposableEffect(key) { onDispose { viewModel.flushDraft(key) } }
    LifecycleEventEffect(Lifecycle.Event.ON_STOP) { viewModel.flushAll() }

    fun text() = state.text.toString()

    fun commit(text: String, selection: TextRange) {
        state.edit {
            replace(0, length, text)
            this.selection = selection.coerceIn(0, text.length)
        }
    }

    fun commit(edit: TextAndCaret) = commit(edit.text, TextRange(edit.caret))

    fun caret() = state.selection.min

    // Con una selección, la sangría afecta a todas las líneas que toca y la selección se conserva.
    fun shift(lines: (String, Int, Int) -> app.aldune.text.SelectionEdit, single: (String, Int) -> TextAndCaret) {
        val s = state.selection
        if (s.collapsed) return commit(single(text(), s.start))
        val e = lines(text(), s.min, s.length)
        commit(e.text, TextRange(e.selectionStart, e.selectionStart + e.selectionLength))
    }

    fun indent() = shift(ListIndent::indentLines, ListIndent::indent)

    fun outdent() = shift(ListIndent::outdentLines, ListIndent::outdent)

    fun move(direction: LineDirection) {
        LineMovement.move(text(), caret(), direction)?.let { commit(it) }
    }

    val slack = with(LocalDensity.current) { 12.dp.toPx() }

    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val settleChecked = settings.reorderOnCheck

    /**
     * Marca o desmarca la casilla de [glyph] y, con el ajuste «mover abajo las tareas hechas», la recoloca en su lista. Al tocar, el cursor
     * sigue a su línea; con Ctrl+Enter se queda en el mismo sitio de la pantalla, como en Windows, para poder marcar una lista seguida.
     */
    fun toggleTask(glyph: Int, keepCaretPosition: Boolean) {
        val toggled = TaskLines.toggleCheckboxAt(text(), glyph) ?: return
        val sel = state.selection
        if (!settleChecked) return commit(toggled, sel)
        val edit = TaskLists.settleToggled(toggled, glyph)
        val moved = if (keepCaretPosition) sel else TextRange(edit.mapIndex(sel.start), edit.mapIndex(sel.end))
        commit(edit.text, moved)
    }

    LaunchedEffect(tap) {
        val position = tap ?: return@LaunchedEffect
        withFrameNanos { }
        toggleCheckboxAtTap(text(), layout?.invoke(), position, slack)?.let { toggleTask(it, keepCaretPosition = false) }
        tap = null
    }

    val touchSlop = LocalViewConfiguration.current.touchSlop
    val longPress = LocalViewConfiguration.current.longPressTimeoutMillis

    Column(Modifier.fillMaxSize().safeDrawingPadding().imePadding()) {
        Row(Modifier.fillMaxWidth(), verticalAlignment = Alignment.CenterVertically) {
            if (showBack) {
                IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = stringResource(R.string.back)) }
            }
            Spacer(Modifier.weight(1f))
            IconButton(onClick = { scope.launch { options = viewModel.noteOptions(key) } }, enabled = loaded) {
                Icon(Icons.Default.MoreVert, contentDescription = stringResource(R.string.note_options))
            }
        }
        options?.let { current -> NoteOptionsDialog(current, viewModel, onClose = { options = null }) }
        BasicTextField(
            state = state,
            inputTransformation = ListKeysTransformation,
            onTextLayout = { getResult -> layout = getResult },
            modifier = Modifier
                .weight(1f)
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 8.dp)
                // Tocar una casilla la marca. Se mira en la pasada Initial y sin consumir nada: el campo sigue recibiendo
                // el toque (cursor, teclado) como siempre.
                .pointerInput(key) {
                    awaitEachGesture {
                        val down = awaitFirstDown(requireUnconsumed = false, pass = PointerEventPass.Initial)
                        val up = waitForUpOrCancellation(PointerEventPass.Initial) ?: return@awaitEachGesture
                        if ((up.position - down.position).getDistance() > touchSlop) return@awaitEachGesture
                        if (up.uptimeMillis - down.uptimeMillis >= longPress) return@awaitEachGesture
                        tap = up.position
                    }
                }
                .onPreviewKeyEvent { event ->
                    if (event.type != KeyEventType.KeyDown) return@onPreviewKeyEvent false
                    when {
                        event.key == Key.Tab -> {
                            if (event.isShiftPressed) outdent() else indent()
                            true
                        }
                        event.isAltPressed && (event.key == Key.DirectionUp || event.key == Key.DirectionDown) -> {
                            move(if (event.key == Key.DirectionUp) LineDirection.Up else LineDirection.Down)
                            true
                        }
                        // Ctrl+Enter marca la tarea de la línea; en una línea que no lo es, Enter normal.
                        event.isCtrlPressed && (event.key == Key.Enter || event.key == Key.NumPadEnter) -> {
                            val glyph = TaskLists.checkboxOnLine(text(), caret())
                            if (glyph != null) toggleTask(glyph, keepCaretPosition = true)
                            glyph != null
                        }
                        else -> false
                    }
                },
            textStyle = MaterialTheme.typography.bodyLarge.copy(color = MaterialTheme.colorScheme.onBackground),
            cursorBrush = SolidColor(MaterialTheme.colorScheme.primary),
        )
        HorizontalDivider()
        EditBar(
            text = state.text.toString(),
            onCheckbox = { commit(TaskLines.toggleTaskLineAt(text(), caret())) },
            onBullet = { commit(BulletLines.toggleBulletLineAt(text(), caret())) },
            onOutdent = ::outdent,
            onIndent = ::indent,
            onMoveUp = { move(LineDirection.Up) },
            onMoveDown = { move(LineDirection.Down) },
            onUncheckAll = { commit(TaskLists.uncheckAll(text()).text, state.selection) },
            onRemoveChecked = {
                val edit = TaskLists.removeChecked(text())
                commit(edit.text, TextRange(edit.mapIndex(state.selection.start), edit.mapIndex(state.selection.end)))
            },
        )
    }
}

/**
 * La posición de la casilla tocada, o `null` si el toque no cayó sobre una. `getOffsetForPosition` da el límite de carácter más
 * cercano, así que se prueban el carácter anterior y el siguiente; y se exige que el toque esté a menos de [slack] del glifo, para que
 * tocar el texto de la tarea no la marque.
 */
private fun toggleCheckboxAtTap(text: String, layout: TextLayoutResult?, position: Offset, slack: Float): Int? {
    layout ?: return null
    if (text.isEmpty()) return null
    val offset = layout.getOffsetForPosition(position)
    for (candidate in intArrayOf(offset, offset - 1)) {
        if (candidate !in text.indices) continue
        if (TaskLines.toggleCheckboxAt(text, candidate) == null) continue
        val box = layout.getBoundingBox(candidate)
        if (position.y in box.top..box.bottom && position.x in (box.left - slack)..(box.right + slack)) return candidate
    }
    return null
}

/** Barra sobre el teclado: casilla, viñeta, sangría, mover línea y el menú «⋯» de la lista de tareas. */
@Composable
private fun EditBar(
    text: String,
    onCheckbox: () -> Unit,
    onBullet: () -> Unit,
    onOutdent: () -> Unit,
    onIndent: () -> Unit,
    onMoveUp: () -> Unit,
    onMoveDown: () -> Unit,
    onUncheckAll: () -> Unit,
    onRemoveChecked: () -> Unit,
) {
    var menuOpen by remember { mutableStateOf(false) }
    Row(Modifier.fillMaxWidth().horizontalScroll(rememberScrollState())) {
        BarButton("☐", stringResource(R.string.edit_checkbox), onCheckbox)
        BarButton("→", stringResource(R.string.edit_bullet), onBullet)
        BarButton("⇤", stringResource(R.string.edit_outdent), onOutdent)
        BarButton("⇥", stringResource(R.string.edit_indent), onIndent)
        BarButton("↑", stringResource(R.string.edit_move_up), onMoveUp)
        BarButton("↓", stringResource(R.string.edit_move_down), onMoveDown)
        Box {
            BarButton("⋯", stringResource(R.string.edit_more)) { menuOpen = true }
            DropdownMenu(expanded = menuOpen, onDismissRequest = { menuOpen = false }) {
                DropdownMenuItem(
                    text = { Text(stringResource(R.string.edit_uncheck_all)) },
                    enabled = TaskLists.hasChecked(text),
                    onClick = { menuOpen = false; onUncheckAll() },
                )
                DropdownMenuItem(
                    text = { Text(stringResource(R.string.edit_remove_checked)) },
                    enabled = TaskLists.hasChecked(text),
                    onClick = { menuOpen = false; onRemoveChecked() },
                )
            }
        }
    }
}

@Composable
private fun BarButton(label: String, description: String, onClick: () -> Unit) {
    IconButton(onClick = onClick, modifier = Modifier.semantics { contentDescription = description }) {
        Text(label, style = MaterialTheme.typography.titleLarge, color = MaterialTheme.colorScheme.onBackground)
    }
}
